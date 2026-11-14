using System.IO;
using VideoAutoWpf.Models;
using VideoAutoWpf.Services;
using Xunit;

namespace VideoAutoWpf.Tests;

public class ShortVideoExportTests : IDisposable
{
    private readonly string _testWorkspaceDir;
    private readonly FFmpegService _ffmpegService;

    public ShortVideoExportTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"video_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testWorkspaceDir);
        _ffmpegService = new FFmpegService();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testWorkspaceDir))
            {
                Directory.Delete(_testWorkspaceDir, true);
            }
        }
        catch { }
    }

    /// <summary>
    /// Test 1: Xuất video ngắn chuẩn dọc 9:16 (Shorts/TikTok/Reels) gồm 2 phân cảnh có phụ đề tiếng Việt.
    /// </summary>
    [Fact]
    public async Task Test_01_ExportShortVideo_9x16_Vertical_TikTok_Shorts()
    {
        // 1. Chuẩn bị tài nguyên mẫu: 2 cảnh (ảnh 1080x1920 + audio 1.5s và 2.0s)
        var img1 = Path.Combine(_testWorkspaceDir, "scene1.png");
        var img2 = Path.Combine(_testWorkspaceDir, "scene2.png");
        var aud1 = Path.Combine(_testWorkspaceDir, "scene1.aac");
        var aud2 = Path.Combine(_testWorkspaceDir, "scene2.aac");

        await TestMediaHelper.CreateTestImageAsync(img1, 1080, 1920, "blue");
        await TestMediaHelper.CreateTestImageAsync(img2, 1080, 1920, "darkred");
        await TestMediaHelper.CreateTestAudioAsync(aud1, 1.5, 440);
        await TestMediaHelper.CreateTestAudioAsync(aud2, 2.0, 554);

        var scenes = new List<SceneItem>
        {
            new()
            {
                Index = 1,
                ImagePath = img1,
                AudioPath = aud1,
                DurationSeconds = 1.5,
                Text = "Chào mừng bạn đến với kênh Ký Án Bí Ẩn",
                MotionEffect = "zoom_in"
            },
            new()
            {
                Index = 2,
                ImagePath = img2,
                AudioPath = aud2,
                DurationSeconds = 2.0,
                Text = "Vụ án chấn động năm ấy rốt cuộc đã xảy ra như thế nào?",
                MotionEffect = "zoom_out"
            }
        };

        var config = new VideoConfig
        {
            AspectRatio = "9:16",
            OutputDirectory = Path.Combine(_testWorkspaceDir, "Output"),
            OutputFileName = "test_short_9x16.mp4",
            EnableSubtitles = true,
            SubtitleStyle = "cinematic",
            EnableFadeTransition = true,
            PublishInfo = new VideoPublishInfo
            {
                Title = "Bí Ẩn Vụ Án Chấn Động",
                Description = "Tóm tắt vụ án bí ẩn rùng rợn nhất lịch sử",
                Hashtags = "#shorts #kyan #bian #xuhuong"
            }
        };

        // 2. Chạy xuất video
        var generatedVideoPath = await _ffmpegService.GenerateVideoAsync(scenes, config);

        // 3. Xác thực kết quả xuất
        Assert.True(File.Exists(generatedVideoPath), "File video xuất ra phải tồn tại trên đĩa.");
        Assert.True(new FileInfo(generatedVideoPath).Length > 10_000, "Dung lượng video phải lớn hơn 10KB.");

        // Kiểm tra cấu trúc thư mục project xuất bản
        var projectDir = Path.GetDirectoryName(Path.GetDirectoryName(generatedVideoPath))!;
        Assert.True(Directory.Exists(Path.Combine(projectDir, "Images")), "Thư mục Images phải được tạo.");
        Assert.True(Directory.Exists(Path.Combine(projectDir, "Audio")), "Thư mục Audio phải được tạo.");
        Assert.True(File.Exists(Path.Combine(projectDir, "DANG_BAI_METADATA.txt")), "File DANG_BAI_METADATA.txt phải được tạo.");

        // Trạng thái các cảnh phải chuyển thành 'Hoàn thành'
        Assert.All(scenes, s => Assert.Equal("Hoàn thành", s.Status));

        // 4. Dùng ffprobe phân tích luồng và độ phân giải
        var probe = await TestMediaHelper.ProbeVideoAsync(generatedVideoPath);
        Assert.True(probe.HasVideoStream, "Video phải có luồng video.");
        Assert.True(probe.HasAudioStream, "Video phải có luồng âm thanh.");
        Assert.Equal(1080, probe.Width);
        Assert.Equal(1920, probe.Height);
        Assert.InRange(probe.Duration, 3.0, 4.0); // Tổng thời lượng 1.5s + 2.0s = 3.5s
    }

    /// <summary>
    /// Test 2: Xuất video ngắn có trộn nhạc nền (Background Music - BGM) và điều chỉnh âm lượng.
    /// </summary>
    [Fact]
    public async Task Test_02_ExportShortVideo_WithBackgroundMusic_BGM()
    {
        var img = Path.Combine(_testWorkspaceDir, "bgm_scene.png");
        var aud = Path.Combine(_testWorkspaceDir, "bgm_voice.aac");
        var bgm = Path.Combine(_testWorkspaceDir, "dramatic_bgm.aac");

        await TestMediaHelper.CreateTestImageAsync(img, 1080, 1920, "#1e1e2e");
        await TestMediaHelper.CreateTestAudioAsync(aud, 2.0, 440);
        await TestMediaHelper.CreateTestAudioAsync(bgm, 5.0, 220); // Nhạc nền 5s dài hơn voice

        var scenes = new List<SceneItem>
        {
            new() { Index = 1, ImagePath = img, AudioPath = aud, DurationSeconds = 2.0, Text = "Video thử nghiệm nhạc nền BGM" }
        };

        var config = new VideoConfig
        {
            AspectRatio = "9:16",
            OutputDirectory = Path.Combine(_testWorkspaceDir, "OutputBgm"),
            OutputFileName = "short_bgm.mp4",
            EnableBackgroundMusic = true,
            BackgroundMusicPath = bgm,
            BackgroundMusicVolume = 0.15
        };

        var generatedVideoPath = await _ffmpegService.GenerateVideoAsync(scenes, config);

        Assert.True(File.Exists(generatedVideoPath));
        var probe = await TestMediaHelper.ProbeVideoAsync(generatedVideoPath);
        Assert.True(probe.HasVideoStream);
        Assert.True(probe.HasAudioStream);
        Assert.InRange(probe.Duration, 1.8, 2.5);

        // Kiểm tra file BGM được sao lưu vào thư mục Audio/ của dự án
        var projectDir = Path.GetDirectoryName(Path.GetDirectoryName(generatedVideoPath))!;
        var bgmInProject = Path.Combine(projectDir, "Audio", $"bgm_{Path.GetFileName(bgm)}");
        Assert.True(File.Exists(bgmInProject), "File nhạc nền phải được lưu vào thư mục Audio của project.");
    }

    /// <summary>
    /// Test 3: Xuất video ngắn có gắn ảnh bìa Master Poster (Metadata attached_pic + POSTER_THUMBNAIL.png).
    /// </summary>
    [Fact]
    public async Task Test_03_ExportShortVideo_WithMasterPosterMetadata_AttachedPic()
    {
        var img = Path.Combine(_testWorkspaceDir, "poster_scene.png");
        var aud = Path.Combine(_testWorkspaceDir, "poster_voice.aac");
        var poster = Path.Combine(_testWorkspaceDir, "master_poster.png");

        await TestMediaHelper.CreateTestImageAsync(img, 1080, 1920, "green");
        await TestMediaHelper.CreateTestImageAsync(poster, 1080, 1920, "purple");
        await TestMediaHelper.CreateTestAudioAsync(aud, 1.5, 440);

        var scenes = new List<SceneItem>
        {
            new() { Index = 1, ImagePath = img, AudioPath = aud, DurationSeconds = 1.5, Text = "Thử nghiệm nhúng bìa poster metadata" }
        };

        var config = new VideoConfig
        {
            AspectRatio = "9:16",
            OutputDirectory = Path.Combine(_testWorkspaceDir, "OutputPoster"),
            OutputFileName = "short_with_poster.mp4",
            CoverPosterImagePath = poster
        };

        var generatedVideoPath = await _ffmpegService.GenerateVideoAsync(scenes, config);

        Assert.True(File.Exists(generatedVideoPath));

        // Kiểm tra file POSTER_THUMBNAIL.png xuất hiện bên cạnh thư mục dự án
        var projectDir = Path.GetDirectoryName(Path.GetDirectoryName(generatedVideoPath))!;
        var posterThumbnailFile = Path.Combine(projectDir, "POSTER_THUMBNAIL.png");
        Assert.True(File.Exists(posterThumbnailFile), "File POSTER_THUMBNAIL.png phải được tạo.");

        // Kiểm tra stream attached_pic được nhúng vào container MP4
        var probe = await TestMediaHelper.ProbeVideoAsync(generatedVideoPath);
        Assert.True(probe.HasAttachedPic, "MP4 metadata phải có stream attached_pic làm ảnh bìa khi chưa click.");
    }

    /// <summary>
    /// Test 4: Xuất video ngắn ở các tỷ lệ khung hình khác nhau (9:16 dọc, 16:9 ngang, 1:1 vuông).
    /// </summary>
    [Theory]
    [InlineData("9:16", 1080, 1920)]
    [InlineData("16:9", 1920, 1080)]
    [InlineData("1:1", 1080, 1080)]
    public async Task Test_04_ExportShortVideo_DifferentAspectRatios(string ratio, int expectedWidth, int expectedHeight)
    {
        var img = Path.Combine(_testWorkspaceDir, $"scene_{ratio.Replace(':', '_')}.png");
        var aud = Path.Combine(_testWorkspaceDir, $"voice_{ratio.Replace(':', '_')}.aac");

        await TestMediaHelper.CreateTestImageAsync(img, expectedWidth, expectedHeight, "navy");
        await TestMediaHelper.CreateTestAudioAsync(aud, 1.2, 500);

        var scenes = new List<SceneItem>
        {
            new() { Index = 1, ImagePath = img, AudioPath = aud, DurationSeconds = 1.2, Text = $"Video test tỷ lệ {ratio}" }
        };

        var config = new VideoConfig
        {
            AspectRatio = ratio,
            OutputDirectory = Path.Combine(_testWorkspaceDir, $"Output_{ratio.Replace(':', '_')}"),
            OutputFileName = $"video_{ratio.Replace(':', '_')}.mp4",
            EnableSubtitles = true
        };

        var generatedVideoPath = await _ffmpegService.GenerateVideoAsync(scenes, config);

        var probe = await TestMediaHelper.ProbeVideoAsync(generatedVideoPath);
        Assert.Equal(expectedWidth, probe.Width);
        Assert.Equal(expectedHeight, probe.Height);
    }

    /// <summary>
    /// Test 5: Xuất video ngắn với nhiều phong cách phụ đề khác nhau (cinematic, viral_yellow, boxed, neon, gold, ticker).
    /// </summary>
    [Theory]
    [InlineData("cinematic")]
    [InlineData("viral_yellow")]
    [InlineData("boxed")]
    [InlineData("neon")]
    [InlineData("gold")]
    [InlineData("ticker")]
    public async Task Test_05_ExportShortVideo_SubtitleStyles(string style)
    {
        var img = Path.Combine(_testWorkspaceDir, $"style_{style}.png");
        var aud = Path.Combine(_testWorkspaceDir, $"style_{style}.aac");

        await TestMediaHelper.CreateTestImageAsync(img, 1080, 1920, "#11111b");
        await TestMediaHelper.CreateTestAudioAsync(aud, 1.2, 480);

        var scenes = new List<SceneItem>
        {
            new() { Index = 1, ImagePath = img, AudioPath = aud, DurationSeconds = 1.2, Text = $"Phong cách chữ phụ đề: {style.ToUpper()}" }
        };

        var config = new VideoConfig
        {
            AspectRatio = "9:16",
            OutputDirectory = Path.Combine(_testWorkspaceDir, $"Output_Style_{style}"),
            OutputFileName = $"video_{style}.mp4",
            EnableSubtitles = true,
            SubtitleStyle = style,
            EnableFadeTransition = true,
            EnableVignette = true
        };

        var generatedVideoPath = await _ffmpegService.GenerateVideoAsync(scenes, config);

        Assert.True(File.Exists(generatedVideoPath));
        var probe = await TestMediaHelper.ProbeVideoAsync(generatedVideoPath);
        Assert.True(probe.HasVideoStream);
        Assert.Equal(1080, probe.Width);
        Assert.Equal(1920, probe.Height);
    }

    /// <summary>
    /// Test 6: Xuất video ngắn với hiệu ứng chuyển động Ken Burns khác nhau (zoom_in, zoom_out, pan_left_right, random, none).
    /// </summary>
    [Theory]
    [InlineData("zoom_in")]
    [InlineData("zoom_out")]
    [InlineData("pan_left_right")]
    [InlineData("pan_up")]
    [InlineData("random")]
    [InlineData("none")]
    public async Task Test_06_ExportShortVideo_MotionEffects(string motionEffect)
    {
        var img = Path.Combine(_testWorkspaceDir, $"motion_{motionEffect}.png");
        var aud = Path.Combine(_testWorkspaceDir, $"motion_{motionEffect}.aac");

        await TestMediaHelper.CreateTestImageAsync(img, 1080, 1920, "teal");
        await TestMediaHelper.CreateTestAudioAsync(aud, 1.0, 520);

        var scenes = new List<SceneItem>
        {
            new() { Index = 1, ImagePath = img, AudioPath = aud, DurationSeconds = 1.0, Text = $"Hiệu ứng camera {motionEffect}", MotionEffect = motionEffect }
        };

        var config = new VideoConfig
        {
            AspectRatio = "9:16",
            OutputDirectory = Path.Combine(_testWorkspaceDir, $"Output_Motion_{motionEffect}"),
            OutputFileName = $"video_motion_{motionEffect}.mp4",
            MotionEffect = motionEffect
        };

        var generatedVideoPath = await _ffmpegService.GenerateVideoAsync(scenes, config);

        Assert.True(File.Exists(generatedVideoPath));
        var probe = await TestMediaHelper.ProbeVideoAsync(generatedVideoPath);
        Assert.True(probe.HasVideoStream);
    }

    /// <summary>
    /// Test 7: Xuất video ngắn có phụ đề Karaoke nhảy từng chữ (Word-level timestamps).
    /// </summary>
    [Fact]
    public async Task Test_07_ExportShortVideo_KaraokeSubtitles()
    {
        var img = Path.Combine(_testWorkspaceDir, "karaoke_scene.png");
        var aud = Path.Combine(_testWorkspaceDir, "karaoke_voice.aac");

        await TestMediaHelper.CreateTestImageAsync(img, 1080, 1920, "#181825");
        await TestMediaHelper.CreateTestAudioAsync(aud, 2.5, 440);

        var wordTimestamps = new List<WordTimestamp>
        {
            new() { Word = "Hôm", StartTime = 0.1, EndTime = 0.4 },
            new() { Word = "nay", StartTime = 0.4, EndTime = 0.7 },
            new() { Word = "chúng", StartTime = 0.7, EndTime = 1.0 },
            new() { Word = "ta", StartTime = 1.0, EndTime = 1.3 },
            new() { Word = "khám", StartTime = 1.3, EndTime = 1.7 },
            new() { Word = "phá", StartTime = 1.7, EndTime = 2.1 }
        };

        var scenes = new List<SceneItem>
        {
            new()
            {
                Index = 1,
                ImagePath = img,
                AudioPath = aud,
                DurationSeconds = 2.5,
                Text = "Hôm nay chúng ta khám phá",
                WordTimestamps = wordTimestamps
            }
        };

        var config = new VideoConfig
        {
            AspectRatio = "9:16",
            OutputDirectory = Path.Combine(_testWorkspaceDir, "OutputKaraoke"),
            OutputFileName = "short_karaoke.mp4",
            EnableSubtitles = true,
            EnableKaraokeSubtitles = true,
            KaraokeHighlightColor = "#ffd43b"
        };

        var generatedVideoPath = await _ffmpegService.GenerateVideoAsync(scenes, config);

        Assert.True(File.Exists(generatedVideoPath));
        var probe = await TestMediaHelper.ProbeVideoAsync(generatedVideoPath);
        Assert.True(probe.HasVideoStream);
        Assert.Equal(1080, probe.Width);
        Assert.Equal(1920, probe.Height);
    }

    /// <summary>
    /// Test 8: Kiểm tra bắt lỗi validation khi danh sách phân cảnh rỗng.
    /// </summary>
    [Fact]
    public async Task Test_08_Validation_EmptyScenesList_ThrowsArgumentException()
    {
        var config = new VideoConfig();
        var emptyScenes = new List<SceneItem>();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _ffmpegService.GenerateVideoAsync(emptyScenes, config));
    }

    /// <summary>
    /// Test 9: Kiểm tra bắt lỗi khi thiếu file ảnh hoặc file âm thanh của phân cảnh.
    /// </summary>
    [Fact]
    public async Task Test_09_Validation_MissingAssetFiles_ThrowsFileNotFoundException()
    {
        var validImg = Path.Combine(_testWorkspaceDir, "valid.png");
        await TestMediaHelper.CreateTestImageAsync(validImg, 1080, 1920, "blue");

        // Thiếu file âm thanh
        var sceneMissingAudio = new List<SceneItem>
        {
            new() { Index = 1, ImagePath = validImg, AudioPath = "non_existent_audio.aac" }
        };

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _ffmpegService.GenerateVideoAsync(sceneMissingAudio, new VideoConfig()));

        // Thiếu file ảnh
        var sceneMissingImage = new List<SceneItem>
        {
            new() { Index = 1, ImagePath = "non_existent_image.png", AudioPath = validImg }
        };

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _ffmpegService.GenerateVideoAsync(sceneMissingImage, new VideoConfig()));
    }

    /// <summary>
    /// Test 10: Kiểm tra hủy bỏ tiến trình xuất video thông qua CancellationToken.
    /// </summary>
    [Fact]
    public async Task Test_10_Cancellation_AbortsExportPromptly()
    {
        var img = Path.Combine(_testWorkspaceDir, "cancel_scene.png");
        var aud = Path.Combine(_testWorkspaceDir, "cancel_audio.aac");

        await TestMediaHelper.CreateTestImageAsync(img, 1080, 1920, "black");
        await TestMediaHelper.CreateTestAudioAsync(aud, 3.0, 440);

        var scenes = new List<SceneItem>
        {
            new() { Index = 1, ImagePath = img, AudioPath = aud, DurationSeconds = 3.0 }
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Hủy ngay trước khi render

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _ffmpegService.GenerateVideoAsync(scenes, new VideoConfig(), ct: cts.Token));
    }
}
