using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoWpf.Services;

/// <summary>
/// Đại diện cho một tác vụ sinh tài nguyên trong hàng đợi (Generation Job).
/// </summary>
public partial class GenerationJob : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _jobType = "image"; // image, tts, stt, render

    [ObservableProperty]
    private string _status = "Pending"; // Pending, Running, Completed, Failed, Cancelled

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private int _retryCount = 0;

    public int MaxRetries { get; set; } = 3;

    public Func<CancellationToken, Task>? WorkAction { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}

/// <summary>
/// Hàng đợi sinh tài nguyên trong bộ nhớ (In-Memory Generation Queue).
/// Cho phép điều tiết tốc độ gọi API an toàn, tránh lỗi 429 Quota/Rate Limit và hỗ trợ Retry/Cancel.
/// </summary>
public class GenerationQueue
{
    private readonly ConcurrentQueue<GenerationJob> _pendingQueue = new();
    private CancellationTokenSource? _queueCts;
    private bool _isProcessing;

    public ObservableCollection<GenerationJob> AllJobs { get; } = new();

    public bool IsProcessing => _isProcessing;

    public event Action<GenerationJob>? JobCompleted;
    public event Action<GenerationJob, Exception>? JobFailed;

    /// <summary>
    /// Thêm một tác vụ mới vào hàng đợi.
    /// </summary>
    public void Enqueue(GenerationJob job)
    {
        if (job == null) return;
        job.Status = "Pending";
        AllJobs.Add(job);
        _pendingQueue.Enqueue(job);
    }

    /// <summary>
    /// Bắt đầu xử lý hàng đợi tuần tự với khoảng nghỉ an toàn (DelayBetweenJobsMs) để tuân thủ Quota RPM.
    /// </summary>
    public async Task ProcessQueueAsync(int delayBetweenJobsMs = 2500, CancellationToken ct = default)
    {
        if (_isProcessing) return;
        _isProcessing = true;
        _queueCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _queueCts.Token;

        try
        {
            while (!token.IsCancellationRequested && _pendingQueue.TryDequeue(out var job))
            {
                if (job.Status == "Cancelled") continue;

                job.Status = "Running";
                job.StartedAt = DateTime.Now;

                bool success = false;

                try
                {
                    if (job.WorkAction != null)
                    {
                        await job.WorkAction(token);
                    }
                    job.Status = "Completed";
                    job.FinishedAt = DateTime.Now;
                    success = true;
                    JobCompleted?.Invoke(job);
                }
                catch (OperationCanceledException)
                {
                    job.Status = "Cancelled";
                    break;
                }
                catch (Exception ex)
                {
                    job.ErrorMessage = ex.Message;
                    job.RetryCount++;

                    if (job.RetryCount < job.MaxRetries)
                    {
                        job.Status = "Pending";
                        _pendingQueue.Enqueue(job); // Đưa vào thử lại
                    }
                    else
                    {
                        job.Status = "Failed";
                        job.FinishedAt = DateTime.Now;
                        JobFailed?.Invoke(job, ex);
                    }
                }

                // Nghỉ giữa các job để bảo vệ hạn mức Quota Google Cloud
                if (success && delayBetweenJobsMs > 0 && !_pendingQueue.IsEmpty)
                {
                    await Task.Delay(delayBetweenJobsMs, token);
                }
            }
        }
        finally
        {
            _isProcessing = false;
        }
    }

    /// <summary>
    /// Thử lại một job cụ thể đã bị Failed.
    /// </summary>
    public void RetryJob(GenerationJob job)
    {
        if (job == null) return;
        job.Status = "Pending";
        job.ErrorMessage = null;
        job.RetryCount = 0;
        _pendingQueue.Enqueue(job);
    }

    /// <summary>
    /// Hủy toàn bộ hàng đợi đang chạy.
    /// </summary>
    public void CancelAll()
    {
        _queueCts?.Cancel();
        while (_pendingQueue.TryDequeue(out var job))
        {
            job.Status = "Cancelled";
        }
        _isProcessing = false;
    }
}
