namespace VideoAutoWpf.Models;

public class VoiceCard
{
    public string Key { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Câu mẫu để nghe thử giọng đọc
    /// </summary>
    public string SampleText { get; set; } = "Xin chào, tôi sẽ là người kể chuyện cho video của bạn.";

    public static List<VoiceCard> GetDefaultVoices() => new()
    {
        new VoiceCard
        {
            Key = "vi-VN-Wavenet-B",
            Icon = "🎙️",
            Title = "Nam Wavenet B",
            Gender = "Nam",
            Quality = "Wavenet",
            Description = "Giọng nam trầm ấm, phù hợp kinh dị, bí ẩn, kể chuyện",
            SampleText = "Trong bóng tối, một tiếng thì thầm vang lên từ phía xa..."
        },
        new VoiceCard
        {
            Key = "vi-VN-Wavenet-A",
            Icon = "🎤",
            Title = "Nữ Wavenet A",
            Gender = "Nữ",
            Quality = "Wavenet",
            Description = "Giọng nữ nhẹ nhàng, phù hợp tình cảm, cổ tích, du lịch",
            SampleText = "Hãy cùng khám phá những điều kỳ diệu đang chờ đợi phía trước..."
        },
        new VoiceCard
        {
            Key = "vi-VN-Wavenet-C",
            Icon = "🎤",
            Title = "Nữ Wavenet C",
            Gender = "Nữ",
            Quality = "Wavenet",
            Description = "Giọng nữ sáng, trẻ trung, phù hợp giáo dục, ẩm thực",
            SampleText = "Bạn có biết rằng bộ não con người có thể lưu trữ hàng triệu thông tin?"
        },
        new VoiceCard
        {
            Key = "vi-VN-Wavenet-D",
            Icon = "🎙️",
            Title = "Nam Wavenet D",
            Gender = "Nam",
            Quality = "Wavenet",
            Description = "Giọng nam năng động, phù hợp hài hước, gaming, công nghệ",
            SampleText = "Chào mừng các bạn đến với video mới nhất hôm nay!"
        },
        new VoiceCard
        {
            Key = "vi-VN-Neural2-A",
            Icon = "✨",
            Title = "Nữ Neural2 A",
            Gender = "Nữ",
            Quality = "Neural2 ⭐",
            Description = "Giọng nữ cao cấp, tự nhiên nhất, phù hợp mọi thể loại",
            SampleText = "Đây là giọng nữ chất lượng cao nhất, nghe rất tự nhiên và truyền cảm."
        },
        new VoiceCard
        {
            Key = "vi-VN-Neural2-D",
            Icon = "✨",
            Title = "Nam Neural2 D",
            Gender = "Nam",
            Quality = "Neural2 ⭐",
            Description = "Giọng nam cao cấp, tự nhiên nhất, phù hợp mọi thể loại",
            SampleText = "Đây là giọng nam chất lượng cao nhất, rõ ràng và chuyên nghiệp."
        }
    };
}
