using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoWpf.Models;

public partial class VideoPublishInfo : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("title")]
    private string _title = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("alternative_titles")]
    private string _alternativeTitles = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("description")]
    private string _description = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("hashtags")]
    private string _hashtags = string.Empty;

    /// <summary>
    /// Nội dung bài đăng đầy đủ (Tiêu đề + Mô tả + Kêu gọi hành động + Hashtags) để copy 1-click
    /// </summary>
    public string FullCaption
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Title))
                parts.Add($"🔥 {Title.Trim()}");

            if (!string.IsNullOrWhiteSpace(Description))
                parts.Add(Description.Trim());

            parts.Add("👉 Đừng quên bấm Theo Dõi (Follow) kênh để xem thêm nhiều video thú vị nhé! Hãy để lại bình luận chia sẻ cảm nghĩ của bạn bên dưới 👇");

            if (!string.IsNullOrWhiteSpace(Hashtags))
                parts.Add(Hashtags.Trim());

            return string.Join("\n\n", parts);
        }
    }

    /// <summary>
    /// Xuất định dạng văn bản chi tiết đẹp mắt để ghi vào file .txt trong thư mục video
    /// </summary>
    public string GenerateFormattedTxt(
        string? videoFileName = null,
        string? aspectRatio = null,
        string? voice = null,
        int sceneCount = 0,
        string? bgmName = null)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("======================================================================");
        sb.AppendLine("🎬 BỘ THÔNG TIN ĐĂNG BÀI VIDEO (TIKTOK / SHORTS / REELS / FACEBOOK)");
        sb.AppendLine("======================================================================");
        sb.AppendLine();

        sb.AppendLine("📌 1. TIÊU ĐỀ VIDEO CHÍNH (TITLE):");
        sb.AppendLine(string.IsNullOrWhiteSpace(Title) ? "(Chưa đặt tiêu đề)" : Title.Trim());
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(AlternativeTitles))
        {
            sb.AppendLine("💡 2. GỢI Ý TIÊU ĐỀ THAY THẾ (CHO A/B TESTING):");
            sb.AppendLine(AlternativeTitles.Trim());
            sb.AppendLine();
        }

        sb.AppendLine("📝 3. NỘI DUNG MÔ TẢ & LỜI DẪN (CAPTION / DESCRIPTION):");
        sb.AppendLine(string.IsNullOrWhiteSpace(Description) ? "(Chưa có mô tả)" : Description.Trim());
        sb.AppendLine();

        sb.AppendLine("🏷️ 4. BỘ HASHTAGS XU HƯỚNG:");
        sb.AppendLine(string.IsNullOrWhiteSpace(Hashtags) ? "#shorts #viral #xuhuong #fyp #tiktok" : Hashtags.Trim());
        sb.AppendLine();

        sb.AppendLine("======================================================================");
        sb.AppendLine("🚀 5. TOÀN BỘ BÀI ĐĂNG (CHỈ CẦN SAO CHÉP TOÀN BỘ PHẦN NÀY ĐỂ DÁN):");
        sb.AppendLine("======================================================================");
        sb.AppendLine(FullCaption);
        sb.AppendLine();

        sb.AppendLine("======================================================================");
        sb.AppendLine("ℹ️ THÔNG TIN KỸ THUẬT VIDEO:");
        if (!string.IsNullOrWhiteSpace(videoFileName))
            sb.AppendLine($"- Tên video: {videoFileName}");
        if (!string.IsNullOrWhiteSpace(aspectRatio))
            sb.AppendLine($"- Tỷ lệ khung hình: {aspectRatio}");
        if (sceneCount > 0)
            sb.AppendLine($"- Số phân cảnh: {sceneCount} cảnh");
        if (!string.IsNullOrWhiteSpace(voice))
            sb.AppendLine($"- Giọng đọc TTS: {voice}");
        if (!string.IsNullOrWhiteSpace(bgmName))
            sb.AppendLine($"- Nhạc nền: {bgmName}");
        sb.AppendLine($"- Thời gian tạo: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine("======================================================================");

        return sb.ToString();
    }
}
