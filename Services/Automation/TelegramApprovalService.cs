using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using VideoAutoWpf.Models.Automation;

namespace VideoAutoWpf.Services.Automation;

/// <summary>
/// Dịch vụ quản lý tương tác Telegram Bot để duyệt video từ xa (iPhone/Android).
/// Hỗ trợ gửi thông báo video kèm ảnh thumbnail và inline keyboard,
/// lắng nghe callback duyệt/từ chối an toàn, bảo vệ chống callback trùng lặp và xác thực User ID.
/// </summary>
public class TelegramApprovalService : IDisposable
{
    private readonly DatabaseService _db;
    private readonly HttpClient _httpClient;
    private CancellationTokenSource? _pollingCts;
    private bool _isPolling;
    private long _lastUpdateId = 0;
    private bool _isDisposed;

    public event Action<string, string, long>? ApprovalDecisionRecorded; // (jobId, decision, userId)
    public event Action<string>? LogMessage;

    public TelegramApprovalService(DatabaseService db, HttpClient? httpClient = null)
    {
        _db = db;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>
    /// Bắt đầu lắng nghe các phản hồi callback từ Telegram qua Long Polling
    /// </summary>
    public void StartPolling(AutomationConfig config)
    {
        if (_isPolling) return;

        var token = config.ResolveTelegramBotToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            LogMessage?.Invoke("[Telegram] Chưa cấu hình TELEGRAM_BOT_TOKEN. Tính năng duyệt qua Telegram đang tạm dừng.");
            return;
        }

        _isPolling = true;
        _pollingCts = new CancellationTokenSource();
        var ct = _pollingCts.Token;

        Task.Run(async () =>
        {
            LogMessage?.Invoke("[Telegram] 🤖 Bắt đầu lắng nghe phản hồi duyệt video qua Telegram bot...");
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await PollUpdatesAsync(token, config, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"[Telegram Polling Lỗi] {ex.Message}. Thử lại sau 5 giây...");
                    try { await Task.Delay(5000, ct); } catch { }
                }
            }
            _isPolling = false;
            LogMessage?.Invoke("[Telegram] Đã dừng lắng nghe Telegram bot.");
        }, ct);
    }

    /// <summary>
    /// Dừng lắng nghe polling
    /// </summary>
    public void StopPolling()
    {
        _pollingCts?.Cancel();
        _isPolling = false;
    }

    /// <summary>
    /// Gửi thông báo phê duyệt video tới Telegram với ảnh thumbnail và inline keyboard
    /// </summary>
    public async Task<int?> SendApprovalRequestAsync(AutomationJob job, AutomationConfig config, CancellationToken ct = default)
    {
        var token = config.ResolveTelegramBotToken();
        var chatId = config.TelegramChatId;

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
        {
            LogMessage?.Invoke("[Telegram] Bỏ qua gửi thông báo do thiếu Bot Token hoặc Chat ID.");
            return null;
        }

        var captionBuilder = new StringBuilder();
        captionBuilder.AppendLine("🎬 <b>[XÁC NHẬN DUYỆT ĐĂNG VIDEO MỚI]</b>");
        captionBuilder.AppendLine();
        captionBuilder.AppendLine($"📌 <b>Tiêu đề:</b> {EscapeHtml(job.Title)}");
        captionBuilder.AppendLine($"🆔 <b>Video ID:</b> <code>{job.VideoId}</code>");
        captionBuilder.AppendLine($"📺 <b>Kênh:</b> <code>{job.ChannelId}</code>");
        if (job.DurationSeconds > 0)
        {
            var span = TimeSpan.FromSeconds(job.DurationSeconds);
            captionBuilder.AppendLine($"⏱️ <b>Thời lượng:</b> {span:mm\\:ss}");
        }
        if (!string.IsNullOrWhiteSpace(job.Caption))
        {
            var shortCaption = job.Caption.Length > 200 ? job.Caption[..200] + "..." : job.Caption;
            captionBuilder.AppendLine($"📝 <b>Mô tả:</b> <i>{EscapeHtml(shortCaption)}</i>");
        }
        if (!string.IsNullOrWhiteSpace(job.Hashtags))
        {
            captionBuilder.AppendLine($"🏷️ <b>Hashtags:</b> <code>{EscapeHtml(job.Hashtags)}</code>");
        }
        captionBuilder.AppendLine();
        captionBuilder.AppendLine("👉 Vui lòng chọn một trong các thao tác bên dưới để mini PC tiếp tục vận hành:");

        var replyMarkup = new
        {
            inline_keyboard = new[]
            {
                new[]
                {
                    new { text = "✅ DUYỆT ĐĂNG", callback_data = $"appr:{job.Id}" },
                    new { text = "❌ TỪ CHỐI", callback_data = $"rejt:{job.Id}" }
                },
                new[]
                {
                    new { text = "ℹ️ XEM CHI TIẾT", callback_data = $"info:{job.Id}" }
                }
            }
        };

        try
        {
            // Nếu có ảnh thumbnail, gửi sendPhoto; nếu không gửi sendMessage
            if (!string.IsNullOrEmpty(job.ThumbnailPath) && File.Exists(job.ThumbnailPath))
            {
                return await SendPhotoWithButtonsAsync(token, chatId, job.ThumbnailPath, captionBuilder.ToString(), replyMarkup, ct);
            }
            else
            {
                return await SendMessageWithButtonsAsync(token, chatId, captionBuilder.ToString(), replyMarkup, ct);
            }
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"[Telegram Lỗi Gửi Duyệt] {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gửi thông báo chung dạng text đến Telegram (dùng cho thông báo recovery, shutdown, lỗi)
    /// </summary>
    public async Task SendNotificationAsync(string message, AutomationConfig config, CancellationToken ct = default)
    {
        var token = config.ResolveTelegramBotToken();
        var chatId = config.TelegramChatId;
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId)) return;

        try
        {
            var url = $"https://api.telegram.org/bot{token}/sendMessage";
            var payload = new
            {
                chat_id = chatId,
                text = message,
                parse_mode = "HTML"
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"[Telegram Lỗi Gửi Thông Báo] {ex.Message}");
        }
    }

    private async Task PollUpdatesAsync(string token, AutomationConfig config, CancellationToken ct)
    {
        var url = $"https://api.telegram.org/bot{token}/getUpdates?offset={_lastUpdateId + 1}&timeout=20";
        var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            await Task.Delay(3000, ct);
            return;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("result", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var update in results.EnumerateArray())
        {
            if (update.TryGetProperty("update_id", out var updateIdProp))
            {
                var updateId = updateIdProp.GetInt64();
                if (updateId > _lastUpdateId) _lastUpdateId = updateId;
            }

            // Xử lý callback query từ nút bấm
            if (update.TryGetProperty("callback_query", out var cbQuery))
            {
                await HandleCallbackQueryAsync(token, cbQuery, config, ct);
            }
        }
    }

    private async Task HandleCallbackQueryAsync(string token, JsonElement cbQuery, AutomationConfig config, CancellationToken ct)
    {
        var callbackQueryId = cbQuery.GetProperty("id").GetString()!;
        var fromUser = cbQuery.GetProperty("from");
        var userId = fromUser.GetProperty("id").GetInt64();
        var userName = fromUser.TryGetProperty("first_name", out var fn) ? fn.GetString() : userId.ToString();
        var data = cbQuery.TryGetProperty("data", out var d) ? d.GetString() : string.Empty;

        // 1. Kiểm tra xác thực User ID
        var allowedUsers = config.ParsedAllowedUserIds;
        if (allowedUsers.Count > 0 && !allowedUsers.Contains(userId))
        {
            await AnswerCallbackAsync(token, callbackQueryId, "⛔ Bạn không có quyền duyệt video trên hệ thống này!", showAlert: true, ct);
            LogMessage?.Invoke($"[Telegram Cảnh Báo] Người dùng không được phép (ID: {userId}, Tên: {userName}) cố bấm duyệt!");
            return;
        }

        if (string.IsNullOrEmpty(data) || !data.Contains(':'))
        {
            await AnswerCallbackAsync(token, callbackQueryId, "Dữ liệu không hợp lệ", showAlert: false, ct);
            return;
        }

        var parts = data.Split(':', 2);
        var action = parts[0];
        var jobId = parts[1];

        // 2. Tra cứu job trong cơ sở dữ liệu SQLite
        var job = await _db.GetJobByIdAsync(jobId);
        if (job == null)
        {
            await AnswerCallbackAsync(token, callbackQueryId, "⚠️ Không tìm thấy công việc này trong hệ thống!", showAlert: true, ct);
            return;
        }

        // 3. Xử lý nút XEM CHI TIẾT
        if (action == "info")
        {
            var infoText = $"ℹ️ Chi tiết Video:\nTiêu đề: {job.Title}\nTrạng thái: {job.DisplayStatus}\nTikTok: {job.TikTokStatus}\nYouTube: {job.YouTubeStatus}\nTạo lúc: {job.CreatedAt:dd/MM/yyyy HH:mm}";
            await AnswerCallbackAsync(token, callbackQueryId, infoText, showAlert: true, ct);
            return;
        }

        // 4. Xử lý nút DUYỆT hoặc TỪ CHỐI
        var isApprove = action == "appr";
        var decision = isApprove ? "Approved" : "Rejected";

        // Ghi nhận quyết định vào database theo giao thức Idempotent Transaction
        var success = await _db.RecordApprovalDecisionAsync(jobId, decision, userId);

        if (!success)
        {
            // Đã được duyệt/từ chối từ trước hoặc không còn ở trạng thái AwaitingApproval
            var currentDecision = job.ApprovalDecision ?? job.Status.ToString();
            await AnswerCallbackAsync(token, callbackQueryId, $"⚠️ Video này đã được xử lý trước đó ({currentDecision})!", showAlert: true, ct);
            return;
        }

        // 5. Trả lời callback query ngay lập tức
        var alertMsg = isApprove ? "✅ Đã DUYỆT video thành công! Video sẽ được đưa vào hàng đợi đăng." : "❌ Đã TỪ CHỐI video. Video sẽ không được đăng.";
        await AnswerCallbackAsync(token, callbackQueryId, alertMsg, showAlert: false, ct);

        // 6. Cập nhật giao diện tin nhắn Telegram (Edit message caption/text) để phản ánh trạng thái đã duyệt
        if (cbQuery.TryGetProperty("message", out var msgElem))
        {
            var chatId = msgElem.GetProperty("chat").GetProperty("id").GetInt64().ToString();
            var messageId = msgElem.GetProperty("message_id").GetInt32();

            var statusBadge = isApprove ? $"✅ <b>ĐÃ DUYỆT BỞI {EscapeHtml(userName)}</b> vào lúc {DateTime.Now:HH:mm:ss dd/MM}" : $"❌ <b>ĐÃ TỪ CHỐI BỞI {EscapeHtml(userName)}</b> vào lúc {DateTime.Now:HH:mm:ss dd/MM}";
            var newCaption = $"🎬 <b>[{job.Title}]</b>\n\n{statusBadge}\nID: <code>{job.Id}</code>";

            await EditMessageCaptionAsync(token, chatId, messageId, newCaption, ct);
        }

        LogMessage?.Invoke($"[Telegram Phê Duyệt] Người dùng {userName} ({userId}) đã chọn '{decision}' cho video {job.Id} ({job.Title})");
        ApprovalDecisionRecorded?.Invoke(jobId, decision, userId);
    }

    private async Task AnswerCallbackAsync(string token, string callbackQueryId, string text, bool showAlert, CancellationToken ct)
    {
        try
        {
            var url = $"https://api.telegram.org/bot{token}/answerCallbackQuery";
            var payload = new
            {
                callback_query_id = callbackQueryId,
                text = text,
                show_alert = showAlert
            };
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            await _httpClient.PostAsync(url, content, ct);
        }
        catch { }
    }

    private async Task EditMessageCaptionAsync(string token, string chatId, int messageId, string newCaption, CancellationToken ct)
    {
        try
        {
            var url = $"https://api.telegram.org/bot{token}/editMessageCaption";
            var payload = new
            {
                chat_id = chatId,
                message_id = messageId,
                caption = newCaption,
                parse_mode = "HTML",
                reply_markup = new { inline_keyboard = Array.Empty<object[]>() } // Xóa các nút bấm sau khi đã duyệt
            };
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            await _httpClient.PostAsync(url, content, ct);
        }
        catch { }
    }

    private async Task<int?> SendPhotoWithButtonsAsync(string token, string chatId, string photoPath, string caption, object replyMarkup, CancellationToken ct)
    {
        var url = $"https://api.telegram.org/bot{token}/sendPhoto";
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(chatId), "chat_id");
        form.Add(new StringContent(caption), "caption");
        form.Add(new StringContent("HTML"), "parse_mode");
        form.Add(new StringContent(JsonSerializer.Serialize(replyMarkup)), "reply_markup");

        var fileBytes = await File.ReadAllBytesAsync(photoPath, ct);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "photo", Path.GetFileName(photoPath));

        var response = await _httpClient.PostAsync(url, form, ct);
        response.EnsureSuccessStatusCode();

        var resJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(resJson);
        if (doc.RootElement.TryGetProperty("result", out var resElem) && resElem.TryGetProperty("message_id", out var mid))
        {
            return mid.GetInt32();
        }
        return null;
    }

    private async Task<int?> SendMessageWithButtonsAsync(string token, string chatId, string text, object replyMarkup, CancellationToken ct)
    {
        var url = $"https://api.telegram.org/bot{token}/sendMessage";
        var payload = new
        {
            chat_id = chatId,
            text = text,
            parse_mode = "HTML",
            reply_markup = replyMarkup
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();

        var resJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(resJson);
        if (doc.RootElement.TryGetProperty("result", out var resElem) && resElem.TryGetProperty("message_id", out var mid))
        {
            return mid.GetInt32();
        }
        return null;
    }

    private static string EscapeHtml(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return input.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        StopPolling();
        _pollingCts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
