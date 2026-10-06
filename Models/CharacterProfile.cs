using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoWpf.Models;

/// <summary>
/// Hồ sơ tạo hình nhân vật độc nhất (Character Profile / Casting Bible).
/// Đảm bảo các nhân vật trong video có ngoại hình, trang phục, phong cách riêng biệt, không bị trùng lặp.
/// </summary>
public partial class CharacterProfile : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("id")]
    private string _id = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("name")]
    private string _name = string.Empty; // e.g. "Lan (Nạn nhân)"

    [ObservableProperty]
    [JsonPropertyName("role")]
    private string _role = string.Empty; // e.g. "Nạn nhân", "Nghi phạm / Hung thủ", "Điều tra viên", "Nhân chứng"

    [ObservableProperty]
    [JsonPropertyName("gender_age")]
    private string _genderAge = string.Empty; // e.g. "Nữ, 22 tuổi"

    [ObservableProperty]
    [JsonPropertyName("appearance_vn")]
    private string _appearanceVn = string.Empty; // Mô tả vóc dáng, khuôn mặt, kiểu tóc bằng tiếng Việt

    [ObservableProperty]
    [JsonPropertyName("outfit_vn")]
    private string _outfitVn = string.Empty; // Trang phục & màu sắc nhận diện đặc trưng bằng tiếng Việt

    [ObservableProperty]
    [JsonPropertyName("visual_prompt")]
    private string _visualPrompt = string.Empty; // Đoạn mô tả tiếng Anh chuẩn Imagen 3 / Gemini Image

    [ObservableProperty]
    [JsonPropertyName("distinguishing_marker")]
    private string _distinguishingMarker = string.Empty; // Đặc điểm nhận diện cốt lõi (áo màu gì, phụ kiện gì...)

    public string DisplayTitle => string.IsNullOrWhiteSpace(Role) ? Name : $"{Name} • {Role}";

    public string DisplaySummary => $"{GenderAge} • {OutfitVn}";
}
