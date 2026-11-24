using VideoAutoWpf.Models;

namespace VideoAutoWpf.Services;

public interface IFFmpegService
{
    string? LocateFfmpeg(string? customPath = null);
    string? LocateFfprobe(string? customPath = null);
    Task<double> GetAudioDurationAsync(string audioPath, string? customFfmpeg = null, CancellationToken ct = default);
    Task<string> GenerateVideoAsync(
        IReadOnlyList<SceneItem> scenes,
        VideoConfig config,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken ct = default);
}
