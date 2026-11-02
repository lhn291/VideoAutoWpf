using System.Data;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using VideoAutoWpf.Models.Automation;

namespace VideoAutoWpf.Services.Automation;

/// <summary>
/// Quản lý cơ sở dữ liệu SQLite cục bộ cho Persistent Job Scheduler, lưu trữ trạng thái bền vững
/// và đảm bảo khôi phục 100% dữ liệu sau restart, crash hoặc mất điện.
/// </summary>
public class DatabaseService : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly SemaphoreSlim _dbLock = new(1, 1);
    private bool _isDisposed;

    public string DatabasePath => _dbPath;

    public DatabaseService(string? customDbPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customDbPath))
        {
            _dbPath = customDbPath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(appData, "VideoAutoWpf");
            Directory.CreateDirectory(folder);
            _dbPath = Path.Combine(folder, "automation.db");
        }

        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        InitializeDatabase();
    }

    /// <summary>
    /// Khởi tạo database và chạy migrations tạo bảng
    /// </summary>
    private void InitializeDatabase()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        // Bật Write-Ahead Logging (WAL) để tăng tốc độ ghi và tránh khóa database
        cmd.CommandText = @"
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;

            CREATE TABLE IF NOT EXISTS automation_jobs (
                id TEXT PRIMARY KEY,
                job_type TEXT NOT NULL,
                status TEXT NOT NULL,
                channel_id TEXT NOT NULL,
                video_id TEXT NOT NULL,
                title TEXT NOT NULL,
                output_path TEXT,
                thumbnail_path TEXT,
                duration_seconds REAL NOT NULL DEFAULT 0,
                caption TEXT,
                hashtags TEXT,
                payload_json TEXT NOT NULL,
                created_at TEXT NOT NULL,
                started_at TEXT,
                updated_at TEXT NOT NULL,
                finished_at TEXT,
                scheduled_publish_at TEXT,
                retry_count INTEGER NOT NULL DEFAULT 0,
                max_retries INTEGER NOT NULL DEFAULT 3,
                last_error_message TEXT,
                heartbeat_at TEXT,
                lease_expires_at TEXT,
                telegram_message_id INTEGER,
                approval_decision TEXT,
                approval_decided_at TEXT,
                approval_decided_by_user_id INTEGER,
                tiktok_status TEXT NOT NULL DEFAULT 'NotStarted',
                tiktok_post_id TEXT,
                tiktok_post_url TEXT,
                tiktok_error_message TEXT,
                tiktok_published_at TEXT,
                tiktok_retry_count INTEGER NOT NULL DEFAULT 0,
                youtube_status TEXT NOT NULL DEFAULT 'NotStarted',
                youtube_post_id TEXT,
                youtube_post_url TEXT,
                youtube_error_message TEXT,
                youtube_published_at TEXT,
                youtube_retry_count INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_jobs_status ON automation_jobs(status);
            CREATE INDEX IF NOT EXISTS idx_jobs_type ON automation_jobs(job_type);
            CREATE INDEX IF NOT EXISTS idx_jobs_lease ON automation_jobs(status, lease_expires_at);

            CREATE TABLE IF NOT EXISTS app_settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS audit_logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                action TEXT NOT NULL,
                details TEXT,
                job_id TEXT
            );
        ";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Thêm một công việc mới vào database
    /// </summary>
    public async Task InsertJobAsync(AutomationJob job)
    {
        await _dbLock.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO automation_jobs (
                    id, job_type, status, channel_id, video_id, title, output_path, thumbnail_path,
                    duration_seconds, caption, hashtags, payload_json, created_at, started_at,
                    updated_at, finished_at, scheduled_publish_at, retry_count, max_retries,
                    last_error_message, heartbeat_at, lease_expires_at, telegram_message_id,
                    approval_decision, approval_decided_at, approval_decided_by_user_id,
                    tiktok_status, tiktok_post_id, tiktok_post_url, tiktok_error_message,
                    tiktok_published_at, tiktok_retry_count, youtube_status, youtube_post_id,
                    youtube_post_url, youtube_error_message, youtube_published_at, youtube_retry_count
                ) VALUES (
                    @id, @job_type, @status, @channel_id, @video_id, @title, @output_path, @thumbnail_path,
                    @duration_seconds, @caption, @hashtags, @payload_json, @created_at, @started_at,
                    @updated_at, @finished_at, @scheduled_publish_at, @retry_count, @max_retries,
                    @last_error_message, @heartbeat_at, @lease_expires_at, @telegram_message_id,
                    @approval_decision, @approval_decided_at, @approval_decided_by_user_id,
                    @tiktok_status, @tiktok_post_id, @tiktok_post_url, @tiktok_error_message,
                    @tiktok_published_at, @tiktok_retry_count, @youtube_status, @youtube_post_id,
                    @youtube_post_url, @youtube_error_message, @youtube_published_at, @youtube_retry_count
                );
            ";

            BindJobParameters(cmd, job);
            await cmd.ExecuteNonQueryAsync();

            await LogAuditInternalAsync(conn, "INSERT_JOB", $"Tạo job {job.Id} ({job.JobType} - {job.Title})", job.Id);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Cập nhật trạng thái và thông tin của công việc hiện có
    /// </summary>
    public async Task UpdateJobAsync(AutomationJob job)
    {
        await _dbLock.WaitAsync();
        try
        {
            job.UpdatedAt = DateTime.UtcNow;
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE automation_jobs SET
                    job_type = @job_type,
                    status = @status,
                    channel_id = @channel_id,
                    video_id = @video_id,
                    title = @title,
                    output_path = @output_path,
                    thumbnail_path = @thumbnail_path,
                    duration_seconds = @duration_seconds,
                    caption = @caption,
                    hashtags = @hashtags,
                    payload_json = @payload_json,
                    started_at = @started_at,
                    updated_at = @updated_at,
                    finished_at = @finished_at,
                    scheduled_publish_at = @scheduled_publish_at,
                    retry_count = @retry_count,
                    max_retries = @max_retries,
                    last_error_message = @last_error_message,
                    heartbeat_at = @heartbeat_at,
                    lease_expires_at = @lease_expires_at,
                    telegram_message_id = @telegram_message_id,
                    approval_decision = @approval_decision,
                    approval_decided_at = @approval_decided_at,
                    approval_decided_by_user_id = @approval_decided_by_user_id,
                    tiktok_status = @tiktok_status,
                    tiktok_post_id = @tiktok_post_id,
                    tiktok_post_url = @tiktok_post_url,
                    tiktok_error_message = @tiktok_error_message,
                    tiktok_published_at = @tiktok_published_at,
                    tiktok_retry_count = @tiktok_retry_count,
                    youtube_status = @youtube_status,
                    youtube_post_id = @youtube_post_id,
                    youtube_post_url = @youtube_post_url,
                    youtube_error_message = @youtube_error_message,
                    youtube_published_at = @youtube_published_at,
                    youtube_retry_count = @youtube_retry_count
                WHERE id = @id;
            ";

            BindJobParameters(cmd, job);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Lấy job theo ID
    /// </summary>
    public async Task<AutomationJob?> GetJobByIdAsync(string id)
    {
        await _dbLock.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM automation_jobs WHERE id = @id LIMIT 1;";
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapJobFromReader(reader);
            }
            return null;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Lấy danh sách job theo tập hợp các trạng thái
    /// </summary>
    public async Task<List<AutomationJob>> GetJobsByStatusAsync(params JobStatus[] statuses)
    {
        await _dbLock.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            if (statuses.Length == 0)
            {
                cmd.CommandText = "SELECT * FROM automation_jobs ORDER BY created_at DESC;";
            }
            else
            {
                var placeholders = string.Join(",", statuses.Select((_, i) => $"@s{i}"));
                cmd.CommandText = $"SELECT * FROM automation_jobs WHERE status IN ({placeholders}) ORDER BY created_at ASC;";
                for (int i = 0; i < statuses.Length; i++)
                {
                    cmd.Parameters.AddWithValue($"@s{i}", statuses[i].ToString());
                }
            }

            var list = new List<AutomationJob>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapJobFromReader(reader));
            }
            return list;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Lấy toàn bộ danh sách job
    /// </summary>
    public async Task<List<AutomationJob>> GetAllJobsAsync()
    {
        return await GetJobsByStatusAsync();
    }

    /// <summary>
    /// Lấy các job đang chạy nhưng bị đơ / quá hạn lease (phát hiện sau crash hoặc mất điện)
    /// </summary>
    public async Task<List<AutomationJob>> GetStaleRunningJobsAsync(DateTime thresholdUtc)
    {
        await _dbLock.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT * FROM automation_jobs 
                WHERE status = 'Running' 
                  AND (lease_expires_at IS NULL OR lease_expires_at < @threshold)
                ORDER BY created_at ASC;
            ";
            cmd.Parameters.AddWithValue("@threshold", thresholdUtc.ToString("o"));

            var list = new List<AutomationJob>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapJobFromReader(reader));
            }
            return list;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Cập nhật nhịp tim heartbeat và gia hạn lease cho job đang chạy
    /// </summary>
    public async Task UpdateHeartbeatAsync(string jobId, DateTime leaseExpiresAtUtc)
    {
        await _dbLock.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE automation_jobs SET
                    heartbeat_at = @heartbeat,
                    lease_expires_at = @lease,
                    updated_at = @updated
                WHERE id = @id;
            ";
            var now = DateTime.UtcNow.ToString("o");
            cmd.Parameters.AddWithValue("@id", jobId);
            cmd.Parameters.AddWithValue("@heartbeat", now);
            cmd.Parameters.AddWithValue("@lease", leaseExpiresAtUtc.ToString("o"));
            cmd.Parameters.AddWithValue("@updated", now);

            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Lưu quyết định duyệt hoặc từ chối từ Telegram trong một Atomic Transaction
    /// Đảm bảo không thay đổi nếu job đã được xử lý trước đó (Idempotent Callback Protection).
    /// </summary>
    public async Task<bool> RecordApprovalDecisionAsync(string jobId, string decision, long userId)
    {
        await _dbLock.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var trans = conn.BeginTransaction();
            using var checkCmd = conn.CreateCommand();
            checkCmd.Transaction = trans;
            checkCmd.CommandText = "SELECT status, approval_decision FROM automation_jobs WHERE id = @id LIMIT 1;";
            checkCmd.Parameters.AddWithValue("@id", jobId);

            using var reader = await checkCmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return false; // Job không tồn tại
            }

            var currentStatus = reader.GetString(0);
            var existingDecision = reader.IsDBNull(1) ? null : reader.GetString(1);
            reader.Close();

            // Nếu không còn ở AwaitingApproval hoặc đã có quyết định -> Từ chối thay đổi (Idempotent)
            if (currentStatus != nameof(JobStatus.AwaitingApproval) || !string.IsNullOrEmpty(existingDecision))
            {
                return false;
            }

            var newStatus = decision.Equals("Approved", StringComparison.OrdinalIgnoreCase)
                ? JobStatus.Approved
                : JobStatus.Rejected;

            using var updateCmd = conn.CreateCommand();
            updateCmd.Transaction = trans;
            updateCmd.CommandText = @"
                UPDATE automation_jobs SET
                    status = @status,
                    approval_decision = @decision,
                    approval_decided_at = @decided_at,
                    approval_decided_by_user_id = @user_id,
                    updated_at = @updated_at
                WHERE id = @id;
            ";
            var now = DateTime.UtcNow.ToString("o");
            updateCmd.Parameters.AddWithValue("@id", jobId);
            updateCmd.Parameters.AddWithValue("@status", newStatus.ToString());
            updateCmd.Parameters.AddWithValue("@decision", decision);
            updateCmd.Parameters.AddWithValue("@decided_at", now);
            updateCmd.Parameters.AddWithValue("@user_id", userId);
            updateCmd.Parameters.AddWithValue("@updated_at", now);

            await updateCmd.ExecuteNonQueryAsync();

            await LogAuditInternalAsync(conn, "RECORD_APPROVAL", $"Telegram User {userId} đã {decision} job {jobId}", jobId, trans);
            trans.Commit();
            return true;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Lưu cấu hình tự động hóa vào bảng app_settings
    /// </summary>
    public async Task SaveConfigAsync(AutomationConfig config)
    {
        await _dbLock.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO app_settings (key, value, updated_at)
                VALUES ('automation_config', @value, @updated_at);
            ";
            cmd.Parameters.AddWithValue("@value", json);
            cmd.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("o"));

            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Tải cấu hình tự động hóa từ database, nếu chưa có trả về cấu hình mặc định an toàn
    /// </summary>
    public async Task<AutomationConfig> LoadConfigAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM app_settings WHERE key = 'automation_config' LIMIT 1;";

            var result = await cmd.ExecuteScalarAsync();
            if (result is string json && !string.IsNullOrWhiteSpace(json))
            {
                var cfg = JsonSerializer.Deserialize<AutomationConfig>(json);
                if (cfg != null) return cfg;
            }

            return new AutomationConfig();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Ghi log kiểm toán (audit log) vào database
    /// </summary>
    public async Task LogAuditAsync(string action, string details, string? jobId = null)
    {
        await _dbLock.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            await LogAuditInternalAsync(conn, action, details, jobId);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private static async Task LogAuditInternalAsync(SqliteConnection conn, string action, string details, string? jobId = null, SqliteTransaction? trans = null)
    {
        using var cmd = conn.CreateCommand();
        if (trans != null) cmd.Transaction = trans;
        cmd.CommandText = @"
            INSERT INTO audit_logs (timestamp, action, details, job_id)
            VALUES (@ts, @act, @det, @jid);
        ";
        cmd.Parameters.AddWithValue("@ts", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@act", action);
        cmd.Parameters.AddWithValue("@det", (object?)details ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@jid", (object?)jobId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static void BindJobParameters(SqliteCommand cmd, AutomationJob job)
    {
        cmd.Parameters.AddWithValue("@id", job.Id);
        cmd.Parameters.AddWithValue("@job_type", job.JobType.ToString());
        cmd.Parameters.AddWithValue("@status", job.Status.ToString());
        cmd.Parameters.AddWithValue("@channel_id", job.ChannelId);
        cmd.Parameters.AddWithValue("@video_id", job.VideoId);
        cmd.Parameters.AddWithValue("@title", job.Title);
        cmd.Parameters.AddWithValue("@output_path", (object?)job.OutputPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@thumbnail_path", (object?)job.ThumbnailPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@duration_seconds", job.DurationSeconds);
        cmd.Parameters.AddWithValue("@caption", (object?)job.Caption ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@hashtags", (object?)job.Hashtags ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@payload_json", job.PayloadJson);
        cmd.Parameters.AddWithValue("@created_at", job.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@started_at", (object?)job.StartedAt?.ToString("o") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@updated_at", job.UpdatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@finished_at", (object?)job.FinishedAt?.ToString("o") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@scheduled_publish_at", (object?)job.ScheduledPublishAt?.ToString("o") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@retry_count", job.RetryCount);
        cmd.Parameters.AddWithValue("@max_retries", job.MaxRetries);
        cmd.Parameters.AddWithValue("@last_error_message", (object?)job.LastErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@heartbeat_at", (object?)job.HeartbeatAt?.ToString("o") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@lease_expires_at", (object?)job.LeaseExpiresAt?.ToString("o") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@telegram_message_id", (object?)job.TelegramMessageId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@approval_decision", (object?)job.ApprovalDecision ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@approval_decided_at", (object?)job.ApprovalDecidedAt?.ToString("o") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@approval_decided_by_user_id", (object?)job.ApprovalDecidedByUserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@tiktok_status", job.TikTokStatus.ToString());
        cmd.Parameters.AddWithValue("@tiktok_post_id", (object?)job.TikTokPostId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@tiktok_post_url", (object?)job.TikTokPostUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@tiktok_error_message", (object?)job.TikTokErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@tiktok_published_at", (object?)job.TikTokPublishedAt?.ToString("o") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@tiktok_retry_count", job.TikTokRetryCount);
        cmd.Parameters.AddWithValue("@youtube_status", job.YouTubeStatus.ToString());
        cmd.Parameters.AddWithValue("@youtube_post_id", (object?)job.YouTubePostId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@youtube_post_url", (object?)job.YouTubePostUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@youtube_error_message", (object?)job.YouTubeErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@youtube_published_at", (object?)job.YouTubePublishedAt?.ToString("o") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@youtube_retry_count", job.YouTubeRetryCount);
    }

    private static AutomationJob MapJobFromReader(SqliteDataReader r)
    {
        var job = new AutomationJob
        {
            Id = r.GetString(r.GetOrdinal("id")),
            JobType = Enum.TryParse<JobType>(r.GetString(r.GetOrdinal("job_type")), out var jt) ? jt : JobType.RenderSingleVideo,
            Status = Enum.TryParse<JobStatus>(r.GetString(r.GetOrdinal("status")), out var js) ? js : JobStatus.Pending,
            ChannelId = r.GetString(r.GetOrdinal("channel_id")),
            VideoId = r.GetString(r.GetOrdinal("video_id")),
            Title = r.GetString(r.GetOrdinal("title")),
            OutputPath = r.IsDBNull(r.GetOrdinal("output_path")) ? null : r.GetString(r.GetOrdinal("output_path")),
            ThumbnailPath = r.IsDBNull(r.GetOrdinal("thumbnail_path")) ? null : r.GetString(r.GetOrdinal("thumbnail_path")),
            DurationSeconds = r.GetDouble(r.GetOrdinal("duration_seconds")),
            Caption = r.IsDBNull(r.GetOrdinal("caption")) ? null : r.GetString(r.GetOrdinal("caption")),
            Hashtags = r.IsDBNull(r.GetOrdinal("hashtags")) ? null : r.GetString(r.GetOrdinal("hashtags")),
            PayloadJson = r.GetString(r.GetOrdinal("payload_json")),
            CreatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("created_at")), null, System.Globalization.DateTimeStyles.RoundtripKind),
            UpdatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("updated_at")), null, System.Globalization.DateTimeStyles.RoundtripKind),
            RetryCount = r.GetInt32(r.GetOrdinal("retry_count")),
            MaxRetries = r.GetInt32(r.GetOrdinal("max_retries")),
            LastErrorMessage = r.IsDBNull(r.GetOrdinal("last_error_message")) ? null : r.GetString(r.GetOrdinal("last_error_message")),
            TelegramMessageId = r.IsDBNull(r.GetOrdinal("telegram_message_id")) ? null : r.GetInt32(r.GetOrdinal("telegram_message_id")),
            ApprovalDecision = r.IsDBNull(r.GetOrdinal("approval_decision")) ? null : r.GetString(r.GetOrdinal("approval_decision")),
            ApprovalDecidedByUserId = r.IsDBNull(r.GetOrdinal("approval_decided_by_user_id")) ? null : r.GetInt64(r.GetOrdinal("approval_decided_by_user_id")),
            TikTokStatus = Enum.TryParse<PlatformPublishState>(r.GetString(r.GetOrdinal("tiktok_status")), out var tts) ? tts : PlatformPublishState.NotStarted,
            TikTokPostId = r.IsDBNull(r.GetOrdinal("tiktok_post_id")) ? null : r.GetString(r.GetOrdinal("tiktok_post_id")),
            TikTokPostUrl = r.IsDBNull(r.GetOrdinal("tiktok_post_url")) ? null : r.GetString(r.GetOrdinal("tiktok_post_url")),
            TikTokErrorMessage = r.IsDBNull(r.GetOrdinal("tiktok_error_message")) ? null : r.GetString(r.GetOrdinal("tiktok_error_message")),
            TikTokRetryCount = r.GetInt32(r.GetOrdinal("tiktok_retry_count")),
            YouTubeStatus = Enum.TryParse<PlatformPublishState>(r.GetString(r.GetOrdinal("youtube_status")), out var yts) ? yts : PlatformPublishState.NotStarted,
            YouTubePostId = r.IsDBNull(r.GetOrdinal("youtube_post_id")) ? null : r.GetString(r.GetOrdinal("youtube_post_id")),
            YouTubePostUrl = r.IsDBNull(r.GetOrdinal("youtube_post_url")) ? null : r.GetString(r.GetOrdinal("youtube_post_url")),
            YouTubeErrorMessage = r.IsDBNull(r.GetOrdinal("youtube_error_message")) ? null : r.GetString(r.GetOrdinal("youtube_error_message")),
            YouTubeRetryCount = r.GetInt32(r.GetOrdinal("youtube_retry_count"))
        };

        if (!r.IsDBNull(r.GetOrdinal("started_at")))
            job.StartedAt = DateTime.Parse(r.GetString(r.GetOrdinal("started_at")), null, System.Globalization.DateTimeStyles.RoundtripKind);

        if (!r.IsDBNull(r.GetOrdinal("finished_at")))
            job.FinishedAt = DateTime.Parse(r.GetString(r.GetOrdinal("finished_at")), null, System.Globalization.DateTimeStyles.RoundtripKind);

        if (!r.IsDBNull(r.GetOrdinal("scheduled_publish_at")))
            job.ScheduledPublishAt = DateTime.Parse(r.GetString(r.GetOrdinal("scheduled_publish_at")), null, System.Globalization.DateTimeStyles.RoundtripKind);

        if (!r.IsDBNull(r.GetOrdinal("heartbeat_at")))
            job.HeartbeatAt = DateTime.Parse(r.GetString(r.GetOrdinal("heartbeat_at")), null, System.Globalization.DateTimeStyles.RoundtripKind);

        if (!r.IsDBNull(r.GetOrdinal("lease_expires_at")))
            job.LeaseExpiresAt = DateTime.Parse(r.GetString(r.GetOrdinal("lease_expires_at")), null, System.Globalization.DateTimeStyles.RoundtripKind);

        if (!r.IsDBNull(r.GetOrdinal("approval_decided_at")))
            job.ApprovalDecidedAt = DateTime.Parse(r.GetString(r.GetOrdinal("approval_decided_at")), null, System.Globalization.DateTimeStyles.RoundtripKind);

        if (!r.IsDBNull(r.GetOrdinal("tiktok_published_at")))
            job.TikTokPublishedAt = DateTime.Parse(r.GetString(r.GetOrdinal("tiktok_published_at")), null, System.Globalization.DateTimeStyles.RoundtripKind);

        if (!r.IsDBNull(r.GetOrdinal("youtube_published_at")))
            job.YouTubePublishedAt = DateTime.Parse(r.GetString(r.GetOrdinal("youtube_published_at")), null, System.Globalization.DateTimeStyles.RoundtripKind);

        return job;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _dbLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
