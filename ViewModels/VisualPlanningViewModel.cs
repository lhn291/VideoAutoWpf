using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoAutoWpf.Models;
using VideoAutoWpf.Models.V2;
using VideoAutoWpf.Services;

namespace VideoAutoWpf.ViewModels;

/// <summary>
/// ViewModel cho Visual Planning Studio — quản lý toàn bộ workflow duyệt ảnh
/// trước khi bắt đầu sản xuất hàng loạt.
/// Tích hợp trực tiếp vào SeriesCreatorViewModel (không phải window riêng).
/// </summary>
public partial class VisualPlanningViewModel : ObservableObject
{
    private readonly VisualPlanningService _planningService;
    private string _outputDir = string.Empty;
    private string _aspectRatio = "9:16";

    public VisualPlanningViewModel(VisualPlanningService planningService)
    {
        _planningService = planningService;
    }

    // ════════════════════════════════════════════════════════════════════
    // DATA COLLECTIONS
    // ════════════════════════════════════════════════════════════════════

    public ObservableCollection<CharacterProfile> Characters { get; } = new();
    public ObservableCollection<LocationProfile> Locations { get; } = new();
    public ObservableCollection<ScenePreviewItem> ScenePreviews { get; } = new();
    public ObservableCollection<AssetUsageSummary> UsageSummary { get; } = new();

    [ObservableProperty]
    private VisualStyleBible _styleBible = new();

    // ════════════════════════════════════════════════════════════════════
    // SELECTED ITEMS
    // ════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedCharacter))]
    private CharacterProfile? _selectedCharacter;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedLocation))]
    private LocationProfile? _selectedLocation;

    [ObservableProperty]
    private ScenePreviewItem? _selectedPreview;

    public bool HasSelectedCharacter => SelectedCharacter != null;
    public bool HasSelectedLocation => SelectedLocation != null;

    // ════════════════════════════════════════════════════════════════════
    // ACTIVE TAB
    // ════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private int _activeTabIndex = 0; // 0=Characters, 1=Locations, 2=Style, 3=Previews

    // ════════════════════════════════════════════════════════════════════
    // STATUS & PROGRESS
    // ════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _statusText = "Sẵn sàng — Bắt đầu tạo ảnh nhân vật và địa điểm.";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _progressText = string.Empty;

    private CancellationTokenSource? _cts;

    // ════════════════════════════════════════════════════════════════════
    // APPROVAL GATE STATE
    // ════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsApprovalGatePassed))]
    [NotifyPropertyChangedFor(nameof(ApprovalGateStatusText))]
    private List<string> _approvalGateErrors = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ApprovalGateStatusText))]
    private bool _userConfirmedProduction;

    public bool IsApprovalGatePassed =>
        ApprovalGateErrors.Count == 0 && UserConfirmedProduction;

    public string ApprovalGateStatusText => ApprovalGateErrors.Count == 0
        ? (UserConfirmedProduction ? "✅ Tất cả điều kiện đã đáp ứng — Sẵn sàng sản xuất!" : "⚠️ Hãy xác nhận bắt đầu sản xuất.")
        : $"⛔ Còn {ApprovalGateErrors.Count} điều kiện chưa đáp ứng.";

    // ════════════════════════════════════════════════════════════════════
    // COUNTERS (thống kê nhanh)
    // ════════════════════════════════════════════════════════════════════

    public int ApprovedCharacterCount => Characters.Count(c => c.IsApproved);
    public int TotalCharacterCount => Characters.Count;
    public int ApprovedLocationCount => Locations.Count(l => l.IsApproved);
    public int TotalLocationCount => Locations.Count;
    public int ApprovedPreviewCount => ScenePreviews.Count(p => p.IsApproved);
    public int TotalPreviewCount => ScenePreviews.Count;

    // ════════════════════════════════════════════════════════════════════
    // INIT
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Khởi tạo Visual Planning Studio từ dữ liệu đã có trong SeriesProject.
    /// </summary>
    public void Initialize(
        IEnumerable<CharacterProfile> characters,
        IEnumerable<LocationProfile> locations,
        IEnumerable<SceneItem> allScenes,
        string outputDir,
        string aspectRatio,
        VisualStyleBible? existingBible = null)
    {
        _outputDir = outputDir;
        _aspectRatio = aspectRatio;

        Characters.Clear();
        foreach (var ch in characters) Characters.Add(ch);

        Locations.Clear();
        foreach (var loc in locations) Locations.Add(loc);

        if (existingBible != null) StyleBible = existingBible;
        StyleBible.AspectRatio = aspectRatio;

        // Tính usage counts
        _planningService.CalculateAssetUsageFromScenes(Characters, Locations, allScenes);

        // Build usage summary
        RefreshUsageSummary(allScenes);

        // Tạo scene previews nếu chưa có
        if (ScenePreviews.Count == 0)
        {
            var previews = _planningService.SelectRepresentativeScenes(Characters, Locations, allScenes);
            foreach (var p in previews) ScenePreviews.Add(p);
        }

        if (Characters.Count > 0 && SelectedCharacter == null)
            SelectedCharacter = Characters[0];
        if (Locations.Count > 0 && SelectedLocation == null)
            SelectedLocation = Locations[0];
        if (ScenePreviews.Count > 0 && SelectedPreview == null)
            SelectedPreview = ScenePreviews[0];

        RefreshApprovalGate();
        RefreshCounters();
        StatusText = $"✅ Đã nạp {Characters.Count} nhân vật và {Locations.Count} địa điểm. Bắt đầu tạo ảnh mẫu!";
    }

    private void RefreshUsageSummary(IEnumerable<SceneItem> scenes)
    {
        UsageSummary.Clear();
        var summary = _planningService.BuildUsageSummary(Characters, Locations, scenes);
        foreach (var s in summary) UsageSummary.Add(s);
    }

    private void RefreshCounters()
    {
        OnPropertyChanged(nameof(ApprovedCharacterCount));
        OnPropertyChanged(nameof(TotalCharacterCount));
        OnPropertyChanged(nameof(ApprovedLocationCount));
        OnPropertyChanged(nameof(TotalLocationCount));
        OnPropertyChanged(nameof(ApprovedPreviewCount));
        OnPropertyChanged(nameof(TotalPreviewCount));
    }

    private void Log(string msg)
    {
        StatusText = msg;
    }

    // ════════════════════════════════════════════════════════════════════
    // CHARACTER COMMANDS
    // ════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task GenerateCharacterPortraitAsync(CharacterProfile? character)
    {
        var ch = character ?? SelectedCharacter;
        if (ch == null) return;
        if (IsBusy) { MessageBox.Show("Đang tạo ảnh khác, vui lòng đợi.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information); return; }

        IsBusy = true;
        _cts = new CancellationTokenSource();
        try
        {
            Log($"⏳ Đang tạo ảnh chân dung cho {ch.Name}...");
            await _planningService.GenerateCharacterPortraitAsync(
                ch, _outputDir, StyleBible.BuildPromptPrefix(), "1:1", Log, _cts.Token);
            ch.NotifyImageUpdated();
            RefreshCounters();
            RefreshApprovalGate();
            Log($"✅ Đã tạo ảnh chân dung {ch.Name}. Hãy xem và duyệt!");
        }
        catch (OperationCanceledException) { Log("⛔ Đã hủy tạo ảnh."); }
        catch (Exception ex) { Log($"❌ Lỗi: {ex.Message}"); MessageBox.Show(ex.Message, "Lỗi tạo ảnh", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task GenerateCharacterFullBodyAsync(CharacterProfile? character)
    {
        var ch = character ?? SelectedCharacter;
        if (ch == null) return;
        if (IsBusy) { MessageBox.Show("Đang tạo ảnh khác, vui lòng đợi.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information); return; }

        IsBusy = true;
        _cts = new CancellationTokenSource();
        try
        {
            Log($"⏳ Đang tạo ảnh toàn thân cho {ch.Name}...");
            await _planningService.GenerateCharacterFullBodyAsync(
                ch, _outputDir, StyleBible.BuildPromptPrefix(), "2:3", Log, _cts.Token);
            ch.NotifyImageUpdated();
            RefreshCounters();
            RefreshApprovalGate();
            Log($"✅ Đã tạo ảnh toàn thân {ch.Name}.");
        }
        catch (OperationCanceledException) { Log("⛔ Đã hủy."); }
        catch (Exception ex) { Log($"❌ Lỗi: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task GenerateAllCharacterPortraitsAsync()
    {
        if (IsBusy) return;
        if (Characters.Count == 0) { Log("Chưa có nhân vật nào để tạo ảnh."); return; }

        IsBusy = true;
        _cts = new CancellationTokenSource();
        int done = 0;
        try
        {
            Log($"⏳ Bắt đầu tạo ảnh cho {Characters.Count} nhân vật...");
            foreach (var ch in Characters)
            {
                if (_cts.Token.IsCancellationRequested) break;
                Log($"⏳ [{done + 1}/{Characters.Count}] Tạo ảnh chân dung: {ch.Name}...");
                ProgressValue = (done / (double)Characters.Count) * 100;
                ProgressText = $"{done + 1}/{Characters.Count}";

                await _planningService.GenerateCharacterPortraitAsync(
                    ch, _outputDir, StyleBible.BuildPromptPrefix(), "1:1", Log, _cts.Token);
                ch.NotifyImageUpdated();
                done++;
                await Task.Delay(500, _cts.Token); // đệm nhỏ giữa các request
            }
            ProgressValue = 100;
            RefreshCounters();
            RefreshApprovalGate();
            Log($"✅ Đã tạo xong ảnh cho {done}/{Characters.Count} nhân vật. Hãy duyệt từng ảnh!");
        }
        catch (OperationCanceledException) { Log($"⛔ Đã hủy — hoàn thành {done}/{Characters.Count}."); }
        catch (Exception ex) { Log($"❌ Lỗi tạo ảnh hàng loạt: {ex.Message}"); }
        finally { IsBusy = false; ProgressValue = 0; ProgressText = string.Empty; }
    }

    [RelayCommand]
    private void ApproveCharacterPortrait(CharacterProfile? character)
    {
        var ch = character ?? SelectedCharacter;
        if (ch == null) return;
        if (!ch.PortraitAsset.HasImage) { MessageBox.Show("Chưa có ảnh để duyệt.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information); return; }

        ch.PortraitAsset.ApproveLatest();
        ch.ApprovalStatus = AssetApprovalStatus.Approved;
        ch.ReferenceImagePath = ch.PortraitAsset.DisplayImagePath;
        ch.NotifyImageUpdated();
        RefreshCounters();
        RefreshApprovalGate();
        Log($"✅ Đã duyệt ảnh chân dung: {ch.Name}");
    }

    [RelayCommand]
    private void ApproveCharacterFullBody(CharacterProfile? character)
    {
        var ch = character ?? SelectedCharacter;
        if (ch == null) return;
        if (!ch.FullBodyAsset.HasImage) { MessageBox.Show("Chưa có ảnh toàn thân để duyệt.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information); return; }

        ch.FullBodyAsset.ApproveLatest();
        if (string.IsNullOrEmpty(ch.ReferenceImagePath))
            ch.ReferenceImagePath = ch.FullBodyAsset.DisplayImagePath;
        ch.NotifyImageUpdated();
        RefreshCounters();
        RefreshApprovalGate();
        Log($"✅ Đã duyệt ảnh toàn thân: {ch.Name}");
    }

    [RelayCommand]
    private void RejectCharacterAndRegenerate(CharacterProfile? character)
    {
        var ch = character ?? SelectedCharacter;
        if (ch == null) return;
        ch.ApprovalStatus = AssetApprovalStatus.Pending;
        ch.NotifyImageUpdated();
        Log($"🔄 Đã đặt lại trạng thái {ch.Name} — hãy chỉnh sửa mô tả và tạo lại ảnh.");
    }

    [RelayCommand]
    private void UnlockCharacterProfile(CharacterProfile? character)
    {
        var ch = character ?? SelectedCharacter;
        if (ch == null) return;
        var confirm = MessageBox.Show(
            $"Mở khóa hồ sơ '{ch.Name}'?\nCác scene phụ thuộc sẽ được đánh dấu cần xem lại.",
            "Xác nhận mở khóa", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm == MessageBoxResult.Yes)
        {
            ch.MarkDependentScenesForReview();
            ch.ApprovalStatus = AssetApprovalStatus.AwaitingApproval;
            ch.NotifyImageUpdated();
            Log($"⚠️ Đã mở khóa hồ sơ {ch.Name}. Hãy chỉnh sửa và duyệt lại.");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // LOCATION COMMANDS
    // ════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task GenerateLocationImageAsync(LocationProfile? location)
    {
        var loc = location ?? SelectedLocation;
        if (loc == null) return;
        if (IsBusy) { MessageBox.Show("Đang tạo ảnh khác.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information); return; }

        IsBusy = true;
        _cts = new CancellationTokenSource();
        try
        {
            Log($"⏳ Đang tạo ảnh địa điểm: {loc.Name}...");
            await _planningService.GenerateLocationImageAsync(
                loc, _outputDir, StyleBible.BuildPromptPrefix(), "16:9", Log, _cts.Token);
            loc.NotifyImageUpdated();
            RefreshCounters();
            RefreshApprovalGate();
            Log($"✅ Đã tạo ảnh địa điểm {loc.Name}. Hãy xem và duyệt!");
        }
        catch (OperationCanceledException) { Log("⛔ Đã hủy."); }
        catch (Exception ex) { Log($"❌ Lỗi: {ex.Message}"); MessageBox.Show(ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task GenerateAllLocationsAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        _cts = new CancellationTokenSource();
        int done = 0;
        try
        {
            foreach (var loc in Locations)
            {
                if (_cts.Token.IsCancellationRequested) break;
                Log($"⏳ [{done + 1}/{Locations.Count}] Tạo ảnh địa điểm: {loc.Name}...");
                ProgressValue = (done / (double)Locations.Count) * 100;
                await _planningService.GenerateLocationImageAsync(
                    loc, _outputDir, StyleBible.BuildPromptPrefix(), "16:9", Log, _cts.Token);
                loc.NotifyImageUpdated();
                done++;
                await Task.Delay(500, _cts.Token);
            }
            ProgressValue = 100;
            RefreshCounters();
            RefreshApprovalGate();
            Log($"✅ Đã tạo xong ảnh {done}/{Locations.Count} địa điểm!");
        }
        catch (OperationCanceledException) { Log($"⛔ Đã hủy."); }
        catch (Exception ex) { Log($"❌ Lỗi: {ex.Message}"); }
        finally { IsBusy = false; ProgressValue = 0; }
    }

    [RelayCommand]
    private void ApproveLocationImage(LocationProfile? location)
    {
        var loc = location ?? SelectedLocation;
        if (loc == null) return;
        if (!loc.HasImage) { MessageBox.Show("Chưa có ảnh để duyệt.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information); return; }

        loc.LocationAsset.ApproveLatest();
        loc.ApprovalStatus = AssetApprovalStatus.Approved;
        loc.ReferenceImagePath = loc.LocationAsset.DisplayImagePath;
        loc.NotifyImageUpdated();
        RefreshCounters();
        RefreshApprovalGate();
        Log($"✅ Đã duyệt ảnh địa điểm: {loc.Name}");
    }

    [RelayCommand]
    private void UnlockLocationProfile(LocationProfile? location)
    {
        var loc = location ?? SelectedLocation;
        if (loc == null) return;
        var confirm = MessageBox.Show(
            $"Mở khóa địa điểm '{loc.Name}'?\nCác scene phụ thuộc sẽ được đánh dấu cần xem lại.",
            "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm == MessageBoxResult.Yes)
        {
            loc.MarkDependentScenesForReview();
            loc.ApprovalStatus = AssetApprovalStatus.AwaitingApproval;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // VISUAL STYLE COMMANDS
    // ════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task GenerateStyleSampleAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        _cts = new CancellationTokenSource();
        try
        {
            Log("⏳ Đang tạo ảnh mẫu phong cách hình ảnh...");
            await _planningService.GenerateStyleSampleAsync(
                StyleBible, _outputDir, _aspectRatio, Log, _cts.Token);
            RefreshApprovalGate();
            Log("✅ Đã tạo ảnh mẫu phong cách. Hãy xem và duyệt!");
        }
        catch (OperationCanceledException) { Log("⛔ Đã hủy."); }
        catch (Exception ex) { Log($"❌ Lỗi: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ApproveStyleBible()
    {
        if (StyleBible.SampleAsset == null || !StyleBible.SampleAsset.HasImage)
        {
            MessageBox.Show("Hãy tạo ảnh mẫu phong cách trước khi duyệt.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        StyleBible.SampleAsset.ApproveLatest();
        StyleBible.SampleAsset.Status = AssetApprovalStatus.Approved;
        StyleBible.Status = AssetApprovalStatus.Approved;
        RefreshApprovalGate();
        Log("✅ Đã duyệt Visual Style Bible!");
    }

    // ════════════════════════════════════════════════════════════════════
    // SCENE PREVIEW COMMANDS
    // ════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task GenerateScenePreviewAsync(ScenePreviewItem? preview)
    {
        var p = preview ?? SelectedPreview;
        if (p == null) return;
        if (IsBusy) { MessageBox.Show("Đang tạo ảnh khác.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (!StyleBible.IsApproved)
        {
            MessageBox.Show("Hãy duyệt Visual Style Bible trước khi tạo scene preview.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        _cts = new CancellationTokenSource();
        try
        {
            Log($"⏳ Đang tạo ảnh preview Tập {p.EpisodeNumber} Cảnh {p.SceneIndex}...");
            await _planningService.GenerateScenePreviewAsync(
                p, Characters, Locations, StyleBible, _outputDir, _aspectRatio, Log, _cts.Token);
            RefreshCounters();
            RefreshApprovalGate();
            Log($"✅ Đã tạo ảnh preview. Hãy xem và duyệt!");
        }
        catch (OperationCanceledException) { Log("⛔ Đã hủy."); }
        catch (Exception ex) { Log($"❌ Lỗi: {ex.Message}"); MessageBox.Show(ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task GenerateAllPreviewsAsync()
    {
        if (IsBusy) return;
        if (!StyleBible.IsApproved)
        {
            MessageBox.Show("Hãy duyệt Visual Style Bible trước.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        _cts = new CancellationTokenSource();
        int done = 0;
        try
        {
            foreach (var p in ScenePreviews)
            {
                if (_cts.Token.IsCancellationRequested) break;
                Log($"⏳ [{done + 1}/{ScenePreviews.Count}] Scene preview Tập {p.EpisodeNumber} Cảnh {p.SceneIndex}...");
                ProgressValue = (done / (double)ScenePreviews.Count) * 100;
                await _planningService.GenerateScenePreviewAsync(
                    p, Characters, Locations, StyleBible, _outputDir, _aspectRatio, Log, _cts.Token);
                done++;
                await Task.Delay(500, _cts.Token);
            }
            ProgressValue = 100;
            RefreshCounters();
            RefreshApprovalGate();
            Log($"✅ Đã tạo {done}/{ScenePreviews.Count} ảnh preview!");
        }
        catch (OperationCanceledException) { Log("⛔ Đã hủy."); }
        catch (Exception ex) { Log($"❌ Lỗi: {ex.Message}"); }
        finally { IsBusy = false; ProgressValue = 0; }
    }

    [RelayCommand]
    private void ApproveScenePreview(ScenePreviewItem? preview)
    {
        var p = preview ?? SelectedPreview;
        if (p == null) return;
        if (!p.HasImage) { MessageBox.Show("Chưa có ảnh để duyệt.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information); return; }

        p.PreviewAsset.ApproveLatest();
        p.Status = AssetApprovalStatus.Approved;
        RefreshCounters();
        RefreshApprovalGate();
        Log($"✅ Đã duyệt scene preview Tập {p.EpisodeNumber} Cảnh {p.SceneIndex}");
    }

    [RelayCommand]
    private void EditPreviewPrompt(ScenePreviewItem? preview)
    {
        // Chỉ đánh dấu lại pending, không xóa ảnh cũ
        var p = preview ?? SelectedPreview;
        if (p == null) return;
        if (p.Status == AssetApprovalStatus.Approved)
            p.Status = AssetApprovalStatus.AwaitingApproval;
    }

    // ════════════════════════════════════════════════════════════════════
    // APPROVAL GATE
    // ════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void RefreshApprovalGate()
    {
        ApprovalGateErrors = _planningService.ValidateApprovalGate(
            Characters, Locations, StyleBible, ScenePreviews, minPreviewsRequired: 2);
        OnPropertyChanged(nameof(IsApprovalGatePassed));
        OnPropertyChanged(nameof(ApprovalGateStatusText));
    }

    [RelayCommand]
    private void ConfirmStartProduction()
    {
        RefreshApprovalGate();
        if (ApprovalGateErrors.Count > 0)
        {
            var errorList = string.Join("\n", ApprovalGateErrors);
            MessageBox.Show($"Chưa thể bắt đầu sản xuất. Vui lòng hoàn thành:\n\n{errorList}",
                "Approval Gate", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            "Tất cả tài sản quan trọng đã được duyệt.\nBắt đầu sản xuất ảnh hàng loạt?",
            "Xác nhận bắt đầu sản xuất", MessageBoxButton.YesNo, MessageBoxImage.Question);

        UserConfirmedProduction = confirm == MessageBoxResult.Yes;
        if (UserConfirmedProduction)
            Log("🚀 Đã xác nhận! Sẵn sàng sản xuất ảnh hàng loạt.");
    }

    [RelayCommand]
    private void CancelCurrentOperation()
    {
        _cts?.Cancel();
        Log("⛔ Đã yêu cầu hủy...");
    }

    // ════════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lấy tóm tắt trạng thái để hiển thị trên badge của nút mở Visual Planning Studio.
    /// </summary>
    public string GetSummaryBadge()
    {
        int approvedChars = Characters.Count(c => c.IsApproved);
        int approvedLocs = Locations.Count(l => l.IsApproved);
        int approvedPreviews = ScenePreviews.Count(p => p.IsApproved);
        bool styleOk = StyleBible.IsApproved;

        if (IsApprovalGatePassed) return "✅ Sẵn sàng sản xuất";
        return $"🎨 {approvedChars}/{TotalCharacterCount} nhân vật · {approvedLocs}/{TotalLocationCount} địa điểm · {(styleOk ? "Style ✓" : "Style ○")} · {approvedPreviews}/{TotalPreviewCount} preview";
    }
}
