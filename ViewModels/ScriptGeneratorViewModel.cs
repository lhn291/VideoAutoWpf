using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using VideoAutoWpf.Models;
using VideoAutoWpf.Services;

namespace VideoAutoWpf.ViewModels;

public partial class ScriptGeneratorViewModel : ObservableObject
{
    private readonly GeminiScriptService _geminiService;

    public Action<bool>? RequestClose { get; set; }

    [ObservableProperty]
    private string _topic = string.Empty;

    [ObservableProperty]
    private string _selectedStyle = "dark-anime";

    [ObservableProperty]
    private int _selectedNumScenes = 5;

    [ObservableProperty]
    private string _characterRules = string.Empty;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _rawJson = string.Empty;

    public ObservableCollection<SceneItem> GeneratedScenes { get; } = new();

    public ScriptWorkspace? ResultWorkspace { get; private set; }

    public ScriptGeneratorViewModel(GeminiScriptService geminiService)
    {
        _geminiService = geminiService;
    }

    [RelayCommand]
    private async Task GenerateScript()
    {
        if (string.IsNullOrWhiteSpace(Topic))
        {
            MessageBox.Show("Vui lòng nhập chủ đề hoặc nội dung thô của câu chuyện!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsGenerating = true;
        StatusText = "⏳ Đang gọi Gemini AI viết và phân cảnh kịch bản...";

        try
        {
            var workspace = await _geminiService.GenerateScriptAsync(Topic.Trim(), SelectedStyle, SelectedNumScenes, CharacterRules.Trim());
            GeneratedScenes.Clear();
            int idx = 1;
            foreach (var sc in workspace.Scenes)
            {
                GeneratedScenes.Add(new SceneItem
                {
                    Index = idx++,
                    Text = sc.Text,
                    ImagePrompt = sc.ImagePrompt,
                    Status = "Đã sinh kịch bản"
                });
            }

            StatusText = $"✅ Đã tạo thành công {GeneratedScenes.Count} phân cảnh!";
            ResultWorkspace = workspace;
        }
        catch (Exception ex)
        {
            StatusText = "❌ Lỗi khi gọi Gemini AI.";
            MessageBox.Show($"Lỗi sinh kịch bản:\n{ex.Message}", "Lỗi Gemini", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task BrowseJsonFile()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Chọn file JSON kịch bản",
            Filter = "File JSON (*.json)|*.json|Tất cả tệp (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                RawJson = await File.ReadAllTextAsync(ofd.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể đọc file JSON:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void Apply()
    {
        // 1. Nếu có nội dung ở tab JSON
        var trimmedJson = RawJson?.Trim();
        if (!string.IsNullOrEmpty(trimmedJson))
        {
            try
            {
                var ws = JsonSerializer.Deserialize<ScriptWorkspace>(trimmedJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (ws?.Scenes != null && ws.Scenes.Count > 0)
                {
                    ResultWorkspace = ws;
                    RequestClose?.Invoke(true);
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nội dung JSON không hợp lệ:\n{ex.Message}", "Lỗi cú pháp JSON", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        // 2. Nếu lấy từ danh sách phân cảnh AI sinh ra
        if (GeneratedScenes.Count > 0)
        {
            ResultWorkspace = new ScriptWorkspace
            {
                Metadata = ResultWorkspace?.Metadata ?? new ScriptMetadata(),
                Scenes = GeneratedScenes.Select(s => new ScriptScene
                {
                    Text = s.Text ?? string.Empty,
                    ImagePrompt = s.ImagePrompt ?? string.Empty
                }).ToList()
            };

            RequestClose?.Invoke(true);
            return;
        }

        MessageBox.Show("Chưa có phân cảnh nào được tạo hoặc nạp.", "Chưa có dữ liệu", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
