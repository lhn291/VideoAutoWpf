using VideoAutoWpf.Models;
using VideoAutoWpf.Models.V2;

namespace VideoAutoWpf.Services;

/// <summary>
/// Trình xây dựng Timeline (Timeline Builder).
/// Chịu trách nhiệm sắp xếp các Shot, Audio của Scene, Nhạc nền và Phụ đề vào Timeline đa rãnh.
/// Bảo đảm thời lượng Video Track và Audio Track khớp nhau tuyệt đối đến từng mili-giây.
/// </summary>
public static class TimelineBuilder
{
    /// <summary>
    /// Xây dựng Timeline hoàn chỉnh từ VideoProject V2.
    /// </summary>
    public static Timeline BuildTimeline(
        VideoProject project,
        string? subtitleAssPath = null)
    {
        return BuildTimeline(project.Scenes.ToList(), project.RenderSettings, subtitleAssPath);
    }

    /// <summary>
    /// Xây dựng Timeline hoàn chỉnh từ danh sách các Scene V2.
    /// </summary>
    public static Timeline BuildTimeline(
        IReadOnlyList<Scene> scenes,
        VideoConfig config,
        string? subtitleAssPath = null)
    {
        var timeline = new Timeline
        {
            SubtitleFilePath = subtitleAssPath,
            SubtitleStyle = config.SubtitleStyle
        };

        double currentTimelineTime = 0.0;

        foreach (var scene in scenes)
        {
            double sceneDuration = scene.AudioDurationSeconds;
            if (sceneDuration <= 0.1) sceneDuration = 5.0;

            // 1. AUDIO TRACK: Đưa file âm thanh của Scene vào timeline
            if (!string.IsNullOrEmpty(scene.AudioPath))
            {
                timeline.AudioClips.Add(new TimelineAudioClip
                {
                    SceneId = scene.Id,
                    AudioPath = scene.AudioPath,
                    TimelineStartSeconds = currentTimelineTime,
                    DurationSeconds = sceneDuration,
                    Volume = 1.0
                });
            }

            // 2. VIDEO TRACK: Đưa các Shot con vào timeline
            if (scene.Shots.Count == 0)
            {
                // Fallback nếu scene chưa có shot: tạo 1 shot mặc định kéo dài hết scene
                timeline.VideoClips.Add(new TimelineClip
                {
                    ShotId = Guid.NewGuid().ToString("N"),
                    MediaPath = string.Empty,
                    TimelineStartSeconds = currentTimelineTime,
                    DurationSeconds = sceneDuration,
                    MotionEffect = "zoom_in",
                    ShotCode = "A"
                });
            }
            else
            {
                // Chuẩn hóa thời lượng các shot con để tổng bằng đúng sceneDuration
                double shotStartOffset = 0.0;
                for (int sIdx = 0; sIdx < scene.Shots.Count; sIdx++)
                {
                    var shot = scene.Shots[sIdx];
                    bool isLast = (sIdx == scene.Shots.Count - 1);

                    double shotDur = isLast
                        ? Math.Max(0.5, Math.Round(sceneDuration - shotStartOffset, 3))
                        : Math.Round(shot.DurationSeconds, 3);

                    timeline.VideoClips.Add(new TimelineClip
                    {
                        ShotId = shot.Id,
                        MediaPath = shot.ImagePath ?? string.Empty,
                        TimelineStartSeconds = currentTimelineTime + shotStartOffset,
                        DurationSeconds = shotDur,
                        MotionEffect = !string.IsNullOrEmpty(shot.CameraMovement) ? shot.CameraMovement : "zoom_in",
                        ShotCode = shot.ShotCode
                    });

                    shotStartOffset += shotDur;
                }
            }

            currentTimelineTime += sceneDuration;
        }

        timeline.TotalDurationSeconds = currentTimelineTime;

        // 3. BGM TRACK: Thêm nhạc nền lặp nếu có cấu hình
        if (config.EnableBackgroundMusic && !string.IsNullOrEmpty(config.BackgroundMusicPath))
        {
            timeline.BgmClip = new TimelineAudioClip
            {
                AudioPath = config.BackgroundMusicPath,
                TimelineStartSeconds = 0,
                DurationSeconds = currentTimelineTime,
                Volume = Math.Clamp(config.BackgroundMusicVolume, 0.0, 1.0)
            };
        }

        return timeline;
    }
}
