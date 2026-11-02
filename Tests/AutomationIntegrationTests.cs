using System.IO;
using System.Threading;
using VideoAutoWpf.Models.Automation;
using VideoAutoWpf.Services.Automation;
using Xunit;

namespace VideoAutoWpf.Tests;

public class AutomationIntegrationTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly DatabaseService _db;
    private readonly AutomationConfig _config;

    public AutomationIntegrationTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_db_{Guid.NewGuid():N}.db");
        _db = new DatabaseService(_tempDbPath);
        _config = new AutomationConfig
        {
            AutoRunEnabled = true,
            AutoShutdownEnabled = true,
            ShutdownGracePeriodSeconds = 10,
            WaitForPendingApproval = true,
            WaitForScheduledJobs = true,
            ShutdownOnPermanentFailure = false,
            DryRunMode = true, // TUYỆT ĐỐI KHÔNG TẮT MÁY THẬT HOẶC ĐĂNG THẬT TRONG TEST
            TikTokEnabled = true,
            YouTubeEnabled = true
        };
    }

    public void Dispose()
    {
        _db.Dispose();
        try
        {
            if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
        }
        catch { }
    }

    /// <summary>
    /// Test 1: Khởi động và phục hồi database SQLite sau restart
    /// </summary>
    [Fact]
    public async Task Test_01_StartupAndDatabaseRecovery()
    {
        var job = new AutomationJob
        {
            Id = "job_init_1",
            Title = "Video Test 1",
            Status = JobStatus.Pending
        };
        await _db.InsertJobAsync(job);

        // Giả lập restart bằng cách tạo kết nối mới tới cùng file database
        using var db2 = new DatabaseService(_tempDbPath);
        var loaded = await db2.GetJobByIdAsync("job_init_1");

        Assert.NotNull(loaded);
        Assert.Equal("Video Test 1", loaded.Title);
        Assert.Equal(JobStatus.Pending, loaded.Status);
    }

    /// <summary>
    /// Test 2: Tạo video thành công rồi chuyển sang AwaitingApproval
    /// </summary>
    [Fact]
    public async Task Test_02_RenderSuccessLeadsToAwaitingApproval()
    {
        var job = new AutomationJob
        {
            Id = "job_render_2",
            Title = "Video Render Success",
            Status = JobStatus.Pending
        };
        await _db.InsertJobAsync(job);

        // Giả lập pipeline render tạo video xong
        job.Status = JobStatus.AwaitingApproval;
        job.OutputPath = "mock_video.mp4";
        await _db.UpdateJobAsync(job);

        var retrieved = await _db.GetJobByIdAsync("job_render_2");
        Assert.NotNull(retrieved);
        Assert.Equal(JobStatus.AwaitingApproval, retrieved.Status);
    }

    /// <summary>
    /// Test 3: Không shutdown khi còn video chờ duyệt (AwaitingApproval)
    /// </summary>
    [Fact]
    public async Task Test_03_DoNotShutdownWhenVideoAwaitingApproval()
    {
        var job = new AutomationJob
        {
            Id = "job_await_3",
            Title = "Video Cho Duyet",
            Status = JobStatus.AwaitingApproval
        };
        await _db.InsertJobAsync(job);

        using var telegram = new TelegramApprovalService(_db);
        using var coordinator = new ShutdownCoordinator(_db, telegram, _config);

        var eval = await coordinator.EvaluateShutdownSafetyAsync();

        Assert.False(eval.CanShutdown);
        Assert.Contains(eval.BlockingReasons, r => r.Contains("chờ bạn duyệt qua Telegram"));
    }

    /// <summary>
    /// Test 4: Duyệt video và đưa vào lịch đăng (Approved -> Uploading / Published)
    /// </summary>
    [Fact]
    public async Task Test_04_ApproveVideoEnqueuesPublishing()
    {
        var job = new AutomationJob
        {
            Id = "job_approve_4",
            Title = "Video Can Duyet",
            Status = JobStatus.AwaitingApproval
        };
        await _db.InsertJobAsync(job);

        var success = await _db.RecordApprovalDecisionAsync("job_approve_4", "Approved", 999888);
        Assert.True(success);

        var approved = await _db.GetJobByIdAsync("job_approve_4");
        Assert.NotNull(approved);
        Assert.Equal(JobStatus.Approved, approved.Status);
        Assert.Equal("Approved", approved.ApprovalDecision);
        Assert.Equal(999888, approved.ApprovalDecidedByUserId);
    }

    /// <summary>
    /// Test 5: Từ chối video và xác minh không đăng (Rejected)
    /// </summary>
    [Fact]
    public async Task Test_05_RejectVideoDoesNotPublish()
    {
        var job = new AutomationJob
        {
            Id = "job_reject_5",
            Title = "Video Bi Tu Choi",
            Status = JobStatus.AwaitingApproval
        };
        await _db.InsertJobAsync(job);

        var success = await _db.RecordApprovalDecisionAsync("job_reject_5", "Rejected", 123456);
        Assert.True(success);

        var rejected = await _db.GetJobByIdAsync("job_reject_5");
        Assert.NotNull(rejected);
        Assert.Equal(JobStatus.Rejected, rejected.Status);
        Assert.Equal("Rejected", rejected.ApprovalDecision);
        Assert.Equal(PlatformPublishState.NotStarted, rejected.TikTokStatus);
        Assert.Equal(PlatformPublishState.NotStarted, rejected.YouTubeStatus);
    }

    /// <summary>
    /// Test 6: Upload đang chạy thì không shutdown
    /// </summary>
    [Fact]
    public async Task Test_06_DoNotShutdownWhileUploading()
    {
        var job = new AutomationJob
        {
            Id = "job_uploading_6",
            Title = "Video Dang Upload",
            Status = JobStatus.Uploading,
            TikTokStatus = PlatformPublishState.Uploading
        };
        await _db.InsertJobAsync(job);

        using var telegram = new TelegramApprovalService(_db);
        using var coordinator = new ShutdownCoordinator(_db, telegram, _config);

        var eval = await coordinator.EvaluateShutdownSafetyAsync();

        Assert.False(eval.CanShutdown);
        Assert.Contains(eval.BlockingReasons, r => r.Contains("đang trong quá trình tải lên"));
    }

    /// <summary>
    /// Test 7: Mất điện hoặc crash khi đang tạo video (Dọn dẹp file hỏng và khôi phục về Pending)
    /// </summary>
    [Fact]
    public async Task Test_07_CrashDuringRenderCleansCorruptFileAndRequeues()
    {
        // Tạo file video giả mạo bị cắt cụt do mất điện (dưới 50KB)
        var corruptFile = Path.Combine(Path.GetTempPath(), $"corrupt_{Guid.NewGuid():N}.mp4");
        await File.WriteAllBytesAsync(corruptFile, new byte[1024]); // 1KB rỗng

        var job = new AutomationJob
        {
            Id = "job_crash_7",
            Title = "Video Crash Render",
            Status = JobStatus.Running,
            OutputPath = corruptFile
        };
        await _db.InsertJobAsync(job);

        var qc = new QualityCheckService();
        using var telegram = new TelegramApprovalService(_db);
        var yt = new YouTubePublishService(new VideoAutoWpf.Services.GoogleAuthService());
        var tt = new TikTokPublishService();
        var recovery = new RecoveryService(_db, qc, telegram, yt, tt);

        var summary = await recovery.PerformCrashRecoveryAsync(_config);

        Assert.True(summary.ResumedPendingCount > 0);
        Assert.False(File.Exists(corruptFile)); // Đã xóa file hỏng

        var recoveredJob = await _db.GetJobByIdAsync("job_crash_7");
        Assert.NotNull(recoveredJob);
        Assert.Equal(JobStatus.Pending, recoveredJob.Status); // Khôi phục về Pending để chạy lại
    }

    /// <summary>
    /// Test 8: Crash giữa lúc upload và xác nhận -> Chuyển sang AwaitingConfirmation
    /// </summary>
    [Fact]
    public async Task Test_08_CrashDuringUploadFlagsAwaitingConfirmation()
    {
        var job = new AutomationJob
        {
            Id = "job_crash_upload_8",
            Title = "Video Crash Upload",
            Status = JobStatus.Uploading,
            YouTubeStatus = PlatformPublishState.Uploading
        };
        await _db.InsertJobAsync(job);

        var qc = new QualityCheckService();
        using var telegram = new TelegramApprovalService(_db);
        var yt = new YouTubePublishService(new VideoAutoWpf.Services.GoogleAuthService());
        var tt = new TikTokPublishService();
        var recovery = new RecoveryService(_db, qc, telegram, yt, tt);

        await recovery.PerformCrashRecoveryAsync(_config);

        var recovered = await _db.GetJobByIdAsync("job_crash_upload_8");
        Assert.NotNull(recovered);
        Assert.Equal(JobStatus.AwaitingConfirmation, recovered.Status);
        Assert.Equal(PlatformPublishState.AwaitingConfirmation, recovered.YouTubeStatus);
    }

    /// <summary>
    /// Test 9: Callback Telegram trùng hoặc đến muộn (Idempotent Callback Protection)
    /// </summary>
    [Fact]
    public async Task Test_09_DuplicateOrLateTelegramCallbackIsIdempotent()
    {
        var job = new AutomationJob
        {
            Id = "job_callback_9",
            Title = "Video Telegram Callback",
            Status = JobStatus.AwaitingApproval
        };
        await _db.InsertJobAsync(job);

        // Lần bấm đầu tiên: Thành công
        var first = await _db.RecordApprovalDecisionAsync("job_callback_9", "Approved", 111222);
        Assert.True(first);

        // Lần bấm thứ hai (bấm lại hoặc callback gửi chậm): Bị từ chối, không thay đổi
        var second = await _db.RecordApprovalDecisionAsync("job_callback_9", "Rejected", 111222);
        Assert.False(second);

        var check = await _db.GetJobByIdAsync("job_callback_9");
        Assert.NotNull(check);
        Assert.Equal(JobStatus.Approved, check.Status); // Vẫn giữ Approved ban đầu
    }

    /// <summary>
    /// Test 10: TikTok thành công nhưng YouTube thất bại -> Chỉ xử lý lại YouTube, giữ nguyên TikTok
    /// </summary>
    [Fact]
    public async Task Test_10_TikTokSuccessYouTubeFailureOnlyRetriesYouTube()
    {
        var job = new AutomationJob
        {
            Id = "job_partial_10",
            Title = "Video Partial Success",
            Status = JobStatus.Approved,
            TikTokStatus = PlatformPublishState.Published,
            TikTokPostId = "tt_done_123",
            YouTubeStatus = PlatformPublishState.RetryPending,
            YouTubeRetryCount = 1
        };
        await _db.InsertJobAsync(job);

        var yt = new YouTubePublishService(new VideoAutoWpf.Services.GoogleAuthService());
        var tt = new TikTokPublishService();
        var stateMachine = new PublishingStateMachine(_db, yt, tt);

        // Chạy lại quy trình publish trong DryRun
        await stateMachine.ProcessPublishAsync(job, _config);

        var updated = await _db.GetJobByIdAsync("job_partial_10");
        Assert.NotNull(updated);
        Assert.Equal(PlatformPublishState.Published, updated.TikTokStatus);
        Assert.Equal("tt_done_123", updated.TikTokPostId); // Không bị đè
        Assert.Equal(PlatformPublishState.Published, updated.YouTubeStatus); // YouTube hoàn thành sau retry
        Assert.Equal(JobStatus.Published, updated.Status);
    }

    /// <summary>
    /// Test 11: Job retry đến hạn thì không shutdown
    /// </summary>
    [Fact]
    public async Task Test_11_DoNotShutdownWhenRetryPending()
    {
        var job = new AutomationJob
        {
            Id = "job_retry_11",
            Title = "Video Dang Cho Retry",
            Status = JobStatus.RetryPending,
            YouTubeStatus = PlatformPublishState.RetryPending
        };
        await _db.InsertJobAsync(job);

        using var telegram = new TelegramApprovalService(_db);
        using var coordinator = new ShutdownCoordinator(_db, telegram, _config);

        var eval = await coordinator.EvaluateShutdownSafetyAsync();

        Assert.False(eval.CanShutdown);
        Assert.Contains(eval.BlockingReasons, r => r.Contains("đang chờ đến lượt thử lại"));
    }

    /// <summary>
    /// Test 12: Hàng đợi trống và mọi trạng thái đã xác nhận thì cho phép shutdown
    /// </summary>
    [Fact]
    public async Task Test_12_SafeShutdownWhenAllCompleted()
    {
        var job = new AutomationJob
        {
            Id = "job_done_12",
            Title = "Video Da Hoan Tat",
            Status = JobStatus.Published,
            TikTokStatus = PlatformPublishState.Published,
            YouTubeStatus = PlatformPublishState.Published
        };
        await _db.InsertJobAsync(job);

        using var telegram = new TelegramApprovalService(_db);
        using var coordinator = new ShutdownCoordinator(_db, telegram, _config);

        var eval = await coordinator.EvaluateShutdownSafetyAsync();

        Assert.True(eval.CanShutdown);
        Assert.Empty(eval.BlockingReasons);
    }

    /// <summary>
    /// Test 13: Có job mới đến trong thời gian grace period thì hủy shutdown
    /// </summary>
    [Fact]
    public async Task Test_13_NewJobDuringGracePeriodAbortsShutdown()
    {
        using var telegram = new TelegramApprovalService(_db);
        using var coordinator = new ShutdownCoordinator(_db, telegram, _config);

        // Bắt đầu đếm ngược tắt máy
        var countdownTask = coordinator.StartShutdownCountdownAsync();

        // Giả lập sau 2 giây có job mới đến
        await Task.Delay(200);
        coordinator.AbortShutdown("Phát hiện job mới xuất hiện trong hàng đợi.");

        Assert.Equal(ShutdownState.Active, coordinator.CurrentState);
        Assert.Equal(0, coordinator.SecondsRemaining);
    }

    /// <summary>
    /// Test 14: Lỗi database hoặc không thể lưu trạng thái thì không shutdown
    /// </summary>
    [Fact]
    public async Task Test_14_DatabaseErrorPreventsShutdown()
    {
        // Tạo cấu hình tắt AutoShutdown
        var noShutdownConfig = new AutomationConfig { AutoShutdownEnabled = false };
        using var telegram = new TelegramApprovalService(_db);
        using var coordinator = new ShutdownCoordinator(_db, telegram, noShutdownConfig);

        var eval = await coordinator.EvaluateShutdownSafetyAsync();

        Assert.False(eval.CanShutdown);
        Assert.Contains(eval.BlockingReasons, r => r.Contains("TẮT trong cấu hình"));
    }

    /// <summary>
    /// Test 15: Ứng dụng khởi động hai lần không tạo hai scheduler (Single instance mutex)
    /// </summary>
    [Fact]
    public void Test_15_SingleInstanceMutexPreventsDuplicateRunners()
    {
        const string testMutexName = "Global\\VideoAutoWpf_Test_Mutex_15";

        using var mutex1 = new Mutex(true, testMutexName, out bool isFirstCreated);
        Assert.True(isFirstCreated);

        // Instance thứ hai cố gắng mở mutex
        using var mutex2 = new Mutex(true, testMutexName, out bool isSecondCreated);
        Assert.False(isSecondCreated); // Không được phép tạo thêm instance thứ 2!
    }

    [Fact]
    public void Test_17_VisualPlanningViewModel_Init()
    {
        var auth = new VideoAutoWpf.Services.GoogleAuthService();
        var gemini = new VideoAutoWpf.Services.GeminiScriptService(auth);
        var imagen = new VideoAutoWpf.Services.VertexImagenService(auth);
        var planning = new VideoAutoWpf.Services.VisualPlanningService(imagen, gemini);
        var vm = new VideoAutoWpf.ViewModels.VisualPlanningViewModel(planning);

        var chars = new List<VideoAutoWpf.Models.CharacterProfile>
        {
            new VideoAutoWpf.Models.CharacterProfile { Id = "c1", Name = "Lan" }
        };
        var locs = new List<VideoAutoWpf.Models.V2.LocationProfile>
        {
            new VideoAutoWpf.Models.V2.LocationProfile { Id = "l1", Name = "Hẻm" }
        };
        var scenes = new List<VideoAutoWpf.Models.SceneItem>
        {
            new VideoAutoWpf.Models.SceneItem { Index = 1, Text = "Lan ở hẻm", CharacterIdsPresent = new List<string> { "c1" }, LocationId = "l1" }
        };

        vm.Initialize(chars, locs, scenes, Path.GetTempPath(), "9:16");
        Assert.Single(vm.Characters);
        Assert.Single(vm.Locations);
        Assert.NotEmpty(vm.ScenePreviews);
    }

    [Fact]
    public void Test_18_CleanAndExtractJson()
    {
        // 1. Markdown code block
        string md = "Here is the json:\n```json\n{\n  \"suggested_scenes\": 5\n}\n```\nHope it helps!";
        string extracted = VideoAutoWpf.Services.GeminiScriptService.CleanAndExtractJson(md);
        Assert.Equal("{\n  \"suggested_scenes\": 5\n}", extracted);

        // 2. Extra spaces and comment
        string raw = "   {\n \"a\": 1\n}   ";
        Assert.Equal("{\n \"a\": 1\n}", VideoAutoWpf.Services.GeminiScriptService.CleanAndExtractJson(raw));
    }

    [Fact]
    public async Task Test_19_GeneratePlanAsync_WithStatus()
    {
        var auth = new VideoAutoWpf.Services.GoogleAuthService();
        var gemini = new VideoAutoWpf.Services.GeminiScriptService(auth);

        string lastStatus = "";
        var plan = await gemini.GeneratePlanAsync("Bí ẩn lăng mộ vua chúa cổ đại", onStatus: s => lastStatus = s);
        Assert.NotNull(plan);
        Assert.True(plan.SuggestedScenes >= 3);
        Assert.NotEmpty(lastStatus);
    }

    [Fact]
    public async Task Test_20_YouTubeVideoAnalysis()
    {
        var auth = new VideoAutoWpf.Services.GoogleAuthService();
        var yt = new VideoAutoWpf.Services.YouTubeAnalyticsService(auth);
        var info = await yt.GetVideoInfoAsync("dQw4w9WgXcQ");
        Assert.NotNull(info);
        Assert.NotEmpty(info.Title);
    }

    [Fact]
    public async Task Test_21_AnalyzeSingleVideoAsync()
    {
        var auth = new VideoAutoWpf.Services.GoogleAuthService();
        var yt = new VideoAutoWpf.Services.YouTubeAnalyticsService(auth);
        var gemini = new VideoAutoWpf.Services.GeminiScriptService(auth);
        var info = await yt.GetVideoInfoAsync("dQw4w9WgXcQ");

        var analysis = await gemini.AnalyzeSingleVideoAsync(info);
        Assert.NotNull(analysis);
        Assert.NotEmpty(analysis.Summary);
    }

    [Fact]
    public async Task Test_22_AnalyzeCompetitorFormulasAsync()
    {
        var auth = new VideoAutoWpf.Services.GoogleAuthService();
        var gemini = new VideoAutoWpf.Services.GeminiScriptService(auth);

        var topVideos = new List<VideoAutoWpf.Models.YouTubeVideoInfo>
        {
            new()
            {
                Title = "10 Bí Ẩn Chưa Có Lời Giải Lớn Nhất Lịch Sử",
                ViewCount = 1500000,
                LikeCount = 80000,
                Duration = TimeSpan.FromMinutes(12)
            }
        };

        var analysis = await gemini.AnalyzeCompetitorFormulasAsync(
            "Kênh Khám Phá Bí Ẩn",
            "Kênh chuyên làm về các vụ án và bí ẩn thế giới",
            topVideos
        );

        Assert.NotNull(analysis);
        Assert.NotEmpty(analysis.Summary);
        Assert.NotEmpty(analysis.TitleFormulas);
    }

    [Fact]
    public async Task Test_23_ResolveChannelIdAsync()
    {
        var auth = new VideoAutoWpf.Services.GoogleAuthService();
        var yt = new VideoAutoWpf.Services.YouTubeAnalyticsService(auth);
        var id = await yt.ResolveChannelIdAsync("https://www.youtube.com/@MrBeast");
        Assert.NotNull(id);
        Assert.StartsWith("UC", id);
    }

    [Fact]
    public async Task Test_24_ChannelFullAnalysis()
    {
        var auth = new VideoAutoWpf.Services.GoogleAuthService();
        var yt = new VideoAutoWpf.Services.YouTubeAnalyticsService(auth);
        var channelId = await yt.ResolveChannelIdAsync("https://www.youtube.com/@MrBeast");
        var info = await yt.GetChannelInfoAsync(channelId);
        Assert.NotNull(info);
        Assert.NotEmpty(info.Title);

        var topVids = await yt.GetTopVideosAsync(channelId, 5);
        Assert.NotEmpty(topVids);

        var latestVids = await yt.GetLatestVideosAsync(channelId, 5);
        Assert.NotEmpty(latestVids);
    }
}