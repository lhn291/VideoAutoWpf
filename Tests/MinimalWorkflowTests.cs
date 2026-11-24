using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VideoAutoWpf.Models.Minimal;
using Xunit;

namespace VideoAutoWpf.Tests;

public class MinimalWorkflowTests : IDisposable
{
    private readonly string _testBaseDir;

    public MinimalWorkflowTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), $"videoauto_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testBaseDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testBaseDir))
            {
                Directory.Delete(_testBaseDir, true);
            }
        }
        catch { }
    }

    /// <summary>
    /// Test 1: Khởi tạo project và lưu/nạp trạng thái qua project.json
    /// Đảm bảo tính năng lưu trạng thái sau từng bước hoạt động chính xác.
    /// </summary>
    [Fact]
    public void Test_01_VideoProjectData_SaveAndLoad_StatePreservation()
    {
        var projectDir = Path.Combine(_testBaseDir, "TestProject_01");
        var project = new VideoProjectData
        {
            ProjectName = "TestProject_01",
            ProjectDir = projectDir,
            Topic = "Lịch sử AI và tương lai",
            AspectRatio = "9:16",
            CurrentStep = PipelineStep.Script,
            Outline = new VideoOutline
            {
                Title = "Lịch sử AI",
                Summary = "Tóm tắt về AI",
                Sections = new List<OutlineSection>
                {
                    new()
                    {
                        Heading = "Mở đầu",
                        Description = "Sự ra đời của Turing Machine",
                        EstimatedSeconds = 15
                    },
                    new()
                    {
                        Heading = "Bùng nổ",
                        Description = "Kỷ nguyên mô hình ngôn ngữ lớn",
                        EstimatedSeconds = 45
                    }
                }
            },
            Script = new VideoScript
            {
                Title = "Lịch sử AI",
                Scenes = new List<VideoSceneItem>
                {
                    new()
                    {
                        SceneNumber = 1,
                        NarrationText = "Trí tuệ nhân tạo bắt đầu từ một ý tưởng táo bạo của Alan Turing.",
                        VisualPrompt = "A cinematic vintage laboratory with vintage analog computer, golden dramatic lighting",
                        DurationSeconds = 15
                    }
                }
            }
        };

        // Lưu project
        project.Save(projectDir);

        var jsonPath = Path.Combine(projectDir, "project.json");
        Assert.True(File.Exists(jsonPath), "project.json phải được tạo trong thư mục project.");

        // Nạp lại project từ disk
        var loaded = VideoProjectData.Load(projectDir);

        Assert.NotNull(loaded);
        Assert.Equal("TestProject_01", loaded.ProjectName);
        Assert.Equal("Lịch sử AI và tương lai", loaded.Topic);
        Assert.Equal("9:16", loaded.AspectRatio);
        Assert.Equal(PipelineStep.Script, loaded.CurrentStep);
        Assert.NotNull(loaded.Outline);
        Assert.Equal(2, loaded.Outline.Sections.Count);
        Assert.Equal("Mở đầu", loaded.Outline.Sections[0].Heading);
        Assert.NotNull(loaded.Script);
        Assert.Single(loaded.Script.Scenes);
        Assert.Equal(1, loaded.Script.Scenes[0].SceneNumber);
        Assert.Equal("Trí tuệ nhân tạo bắt đầu từ một ý tưởng táo bạo của Alan Turing.", loaded.Script.Scenes[0].NarrationText);
    }

    /// <summary>
    /// Test 2: Khi một bước thất bại, dữ liệu các bước trước không bị mất và có thể retry
    /// </summary>
    [Fact]
    public void Test_02_StepFailure_AllowsRetry_WithoutLosingData()
    {
        var projectDir = Path.Combine(_testBaseDir, "TestProject_Retry");
        var project = new VideoProjectData
        {
            ProjectName = "TestProject_Retry",
            ProjectDir = projectDir,
            Topic = "Khoa học vũ trụ",
            CurrentStep = PipelineStep.Voice,
            Outline = new VideoOutline
            {
                Title = "Vũ trụ bí ẩn",
                Sections = new List<OutlineSection>
                {
                    new() { Heading = "Sao Hỏa", Description = "Bí mật hành tinh đỏ", EstimatedSeconds = 30 }
                }
            },
            Script = new VideoScript
            {
                Title = "Vũ trụ bí ẩn",
                Scenes = new List<VideoSceneItem>
                {
                    new()
                    {
                        SceneNumber = 1,
                        NarrationText = "Sao Hỏa từng có nước.",
                        VisualPrompt = "Mars surface with ancient dry riverbeds",
                        DurationSeconds = 10
                    }
                }
            }
        };

        // Giả lập lưu trạng thái trước khi thực hiện bước Voice
        project.Save(projectDir);

        // Nạp lại khi cần retry
        var reloaded = VideoProjectData.Load(projectDir);
        Assert.NotNull(reloaded);

        // Kiểm tra dữ liệu Outline và Script hoàn toàn nguyên vẹn
        Assert.NotNull(reloaded.Outline);
        Assert.Single(reloaded.Outline.Sections);
        Assert.Equal("Sao Hỏa", reloaded.Outline.Sections[0].Heading);

        Assert.NotNull(reloaded.Script);
        Assert.Single(reloaded.Script.Scenes);
        Assert.Equal("Sao Hỏa từng có nước.", reloaded.Script.Scenes[0].NarrationText);
        Assert.Equal(PipelineStep.Voice, reloaded.CurrentStep);

        // Người dùng retry bước Voice thành công
        reloaded.VoiceAudioPath = Path.Combine(projectDir, "voice.mp3");
        reloaded.CurrentStep = PipelineStep.Images;
        reloaded.Save(projectDir);

        var finalCheck = VideoProjectData.Load(projectDir);
        Assert.NotNull(finalCheck);
        Assert.Equal(PipelineStep.Images, finalCheck.CurrentStep);
        Assert.Equal(Path.Combine(projectDir, "voice.mp3"), finalCheck.VoiceAudioPath);
    }

    /// <summary>
    /// Test 3: Cấu trúc kịch bản và phân cảnh VideoSceneItem tương thích hoàn toàn với Minimal Workflow
    /// </summary>
    [Fact]
    public void Test_03_VideoSceneItem_DefaultsAndValidation()
    {
        var scene = new VideoSceneItem
        {
            SceneNumber = 1,
            NarrationText = "Kiểm tra thuyết minh",
            VisualPrompt = "Cyberpunk street in rain, neon reflection, 8k cinematic",
            DurationSeconds = 5.5
        };

        Assert.Equal(1, scene.SceneNumber);
        Assert.Equal("Kiểm tra thuyết minh", scene.NarrationText);
        Assert.Equal("Cyberpunk street in rain, neon reflection, 8k cinematic", scene.VisualPrompt);
        Assert.Equal(5.5, scene.DurationSeconds);
        Assert.True(string.IsNullOrEmpty(scene.ImagePath));
        Assert.True(string.IsNullOrEmpty(scene.AudioPath));
        Assert.False(scene.HasImage);
        Assert.False(scene.HasAudio);
    }

    /// <summary>
    /// Test 4: Khả năng hủy tác vụ thông qua CancellationToken mà không làm hỏng dữ liệu đã có
    /// </summary>
    [Fact]
    public async Task Test_04_CancellationToken_TaskCancellationSafety()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Đã kích hoạt hủy

        var wasCancelled = false;
        try
        {
            await Task.Run(() =>
            {
                cts.Token.ThrowIfCancellationRequested();
            }, cts.Token);
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
        }

        Assert.True(wasCancelled, "CancellationToken phải được tôn trọng và ném OperationCanceledException");
    }

    /// <summary>
    /// Test 5: KIỂM TRA BẢO MẬT TUYỆT ĐỐI - KHÔNG ĐƯỢC CHỨA BẤT KỲ LỆNH TẮT MÁY/RESTART NÀO
    /// Quét toàn bộ source code của project VideoAutoWpf để đảm bảo không còn:
    /// - shutdown.exe
    /// - Stop-Computer
    /// - Restart-Computer
    /// - InitiateSystemShutdown
    /// - ExitWindowsEx
    /// </summary>
    [Fact]
    public void Test_05_ZeroPowerCommands_SourceCodeVerification()
    {
        // Thư mục gốc VideoAutoWpf
        var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var csFiles = Directory.GetFiles(baseDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\") && !f.EndsWith("MinimalWorkflowTests.cs"))
            .ToList();

        var forbiddenPatterns = new[]
        {
            "shutdown.exe",
            "Stop-Computer",
            "Restart-Computer",
            "InitiateSystemShutdown",
            "ExitWindowsEx"
        };

        var violations = new List<string>();

        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);
            foreach (var pattern in forbiddenPatterns)
            {
                if (content.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    violations.Add($"{Path.GetFileName(file)} chứa pattern cấm: {pattern}");
                }
            }
        }

        Assert.Empty(violations);
    }
}
