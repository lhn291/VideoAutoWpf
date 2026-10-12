using System.Collections.ObjectModel;
using VideoAutoWpf.Models;

namespace VideoAutoWpf.Models.V2;

/// <summary>
/// Adapter chuyển đổi 2 chiều giữa Data Model V1 (SceneItem) và Data Model V2 (Scene / Shot / VideoProject).
/// Đảm bảo tính tương thích ngược 100%, cho phép code cũ tiếp tục hoạt động mà không bị crash.
/// </summary>
public static class SceneItemAdapter
{
    /// <summary>
    /// Chuyển đổi từ SceneItem cũ sang Scene V2 (1 SceneItem tương ứng 1 Scene với 1 Shot khởi tạo nếu chưa có Shot).
    /// </summary>
    public static Scene ToV2Scene(this SceneItem item)
    {
        // Xác định EstimatedDurationSeconds:
        // Ưu tiên: thời lượng audio thực tế (DurationSeconds) > gợi ý từ AI (EstimatedDurationHint)
        var estimatedDuration = item.DurationSeconds > 0.5
            ? item.DurationSeconds
            : (item.EstimatedDurationHint > 0.5 ? item.EstimatedDurationHint : 0);

        var scene = new Scene
        {
            Index = item.Index,
            NarrationText = item.Text ?? string.Empty,
            AudioPath = item.AudioPath,
            AudioDurationSeconds = item.DurationSeconds,
            EstimatedDurationSeconds = estimatedDuration,
            WordTimestamps = item.WordTimestamps,
            CharactersPresentText = item.CharactersPresentText,
            CharacterIdsPresent = item.CharacterIdsPresent?.Count > 0
                ? new List<string>(item.CharacterIdsPresent)
                : new List<string>(),
            LocationId = item.LocationId,
            LocationName = item.LocationName,
            // Story Beat fields (Phase B): drive shot count and pacing weight selection
            Purpose = string.IsNullOrEmpty(item.StoryBeat) ? "Setup" : item.StoryBeat,
            PacingType = string.IsNullOrEmpty(item.PacingType) ? "Normal" : item.PacingType,
            Status = item.Status
        };


        if (item.Shots != null && item.Shots.Count > 0)
        {
            foreach (var s in item.Shots)
            {
                if (string.IsNullOrEmpty(s.LocationId) && !string.IsNullOrEmpty(item.LocationId))
                {
                    s.LocationId = item.LocationId;
                }
                scene.Shots.Add(s);
            }
        }
        else
        {
            var shot = new Shot
            {
                SceneId = scene.Id,
                ShotCode = "A",
                LocationId = item.LocationId,
                StartOffsetSeconds = 0,
                DurationSeconds = item.DurationSeconds > 0 ? item.DurationSeconds : 5.0,
                CompiledImagePrompt = item.ImagePrompt ?? string.Empty,
                ImagePath = item.ImagePath,
                CameraMovement = string.IsNullOrEmpty(item.MotionEffect) ? "zoom_in" : item.MotionEffect,
                Status = item.HasImage ? "Ready" : (item.Status == "Lỗi sinh ảnh" ? "Failed" : "Pending")
            };
            scene.Shots.Add(shot);
        }

        return scene;
    }

    /// <summary>
    /// Chuyển đổi từ Scene V2 về SceneItem V1 để binding lên các thành phần giao diện cũ.
    /// Lấy ảnh đại diện từ Shot đầu tiên hoặc Shot sẵn sàng.
    /// </summary>
    public static SceneItem ToV1SceneItem(this Scene scene)
    {
        var primaryShot = scene.Shots.FirstOrDefault(s => s.HasImage) ?? scene.Shots.FirstOrDefault();

        var item = new SceneItem
        {
            Index = scene.Index,
            Text = scene.NarrationText,
            AudioPath = scene.AudioPath,
            DurationSeconds = scene.EffectiveDurationSeconds,
            WordTimestamps = scene.WordTimestamps,
            CharactersPresentText = scene.CharactersPresentText,
            LocationId = scene.LocationId,
            LocationName = scene.LocationName,
            Status = scene.Status,
            ImagePath = primaryShot?.ImagePath,
            ImagePrompt = primaryShot?.CompiledImagePrompt,
            MotionEffect = primaryShot?.CameraMovement ?? "zoom_in"
        };

        foreach (var s in scene.Shots)
        {
            item.Shots.Add(s);
        }

        return item;
    }

    /// <summary>
    /// Chuyển đổi danh sách SceneItem V1 sang VideoProject V2.
    /// </summary>
    public static VideoProject ToV2Project(
        IEnumerable<SceneItem> items, 
        VideoConfig? config = null, 
        VideoPublishInfo? publishInfo = null,
        IEnumerable<CharacterProfile>? characters = null,
        IEnumerable<LocationProfile>? locations = null,
        StoryBlueprint? blueprint = null,
        ContentAnalysis? analysis = null)
    {
        var project = new VideoProject
        {
            RenderSettings = config ?? new VideoConfig(),
            PublishInfo = publishInfo ?? new VideoPublishInfo(),
            AspectRatio = config?.AspectRatio ?? "9:16",
            StoryBlueprint = blueprint,
            ContentAnalysis = analysis
        };

        if (characters != null)
        {
            foreach (var ch in characters)
                project.Characters.Add(ch);
        }

        if (locations != null)
        {
            foreach (var loc in locations)
                project.Locations.Add(loc);
        }

        foreach (var item in items)
        {
            project.Scenes.Add(item.ToV2Scene());
        }

        return project;
    }

    /// <summary>
    /// Chuyển đổi ngược từ VideoProject V2 sang ObservableCollection SceneItem cho UI V1.
    /// </summary>
    public static ObservableCollection<SceneItem> ToV1SceneItems(this VideoProject project)
    {
        var collection = new ObservableCollection<SceneItem>();
        foreach (var scene in project.Scenes)
        {
            collection.Add(scene.ToV1SceneItem());
        }
        return collection;
    }
}
