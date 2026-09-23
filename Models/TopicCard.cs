namespace VideoAutoWpf.Models;

public class TopicCard
{
    public string Icon { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Key phong cách vẽ gợi ý cho genre này (khớp với GeminiScriptService.StylePrompts)
    /// </summary>
    public string SuggestedStyleKey { get; set; } = string.Empty;

    /// <summary>
    /// Tone giọng đọc gợi ý cho genre này
    /// </summary>
    public string SuggestedTone { get; set; } = string.Empty;

    /// <summary>
    /// Voice gợi ý (mặc định vi-VN-Wavenet-B)
    /// </summary>
    public string SuggestedVoice { get; set; } = "vi-VN-Wavenet-B";

    public static List<TopicCard> GetDefaultTopics() => new()
    {
        new TopicCard
        {
            Icon = "👻",
            Title = "Kinh dị / Horror",
            Description = "Câu chuyện rùng rợn, ma quái, ám ảnh",
            SuggestedStyleKey = "dark-anime",
            SuggestedTone = "Rùng rợn, ám ảnh",
            SuggestedVoice = "vi-VN-Wavenet-B"
        },
        new TopicCard
        {
            Icon = "📚",
            Title = "Giáo dục / Tri thức",
            Description = "Kiến thức khoa học, lịch sử, tổng hợp",
            SuggestedStyleKey = "cartoon-3d",
            SuggestedTone = "Trang nghiêm, truyền cảm",
            SuggestedVoice = "vi-VN-Neural2-D"
        },
        new TopicCard
        {
            Icon = "💕",
            Title = "Tình yêu / Lãng mạn",
            Description = "Câu chuyện tình cảm, romance cảm xúc",
            SuggestedStyleKey = "watercolor",
            SuggestedTone = "Nhẹ nhàng, cảm xúc",
            SuggestedVoice = "vi-VN-Neural2-A"
        },
        new TopicCard
        {
            Icon = "😂",
            Title = "Hài hước / Comedy",
            Description = "Nội dung giải trí vui nhộn, gây cười",
            SuggestedStyleKey = "cartoon-2d",
            SuggestedTone = "Vui nhộn, hài hước",
            SuggestedVoice = "vi-VN-Wavenet-D"
        },
        new TopicCard
        {
            Icon = "🔮",
            Title = "Tâm linh / Bí ẩn",
            Description = "Huyền bí, siêu nhiên, tâm linh",
            SuggestedStyleKey = "cinematic-horror",
            SuggestedTone = "Bí ẩn, hồi hộp",
            SuggestedVoice = "vi-VN-Wavenet-B"
        },
        new TopicCard
        {
            Icon = "⚔️",
            Title = "Hành động / Phiêu lưu",
            Description = "Chiến đấu, mạo hiểm, phiêu lưu ký",
            SuggestedStyleKey = "dark-anime",
            SuggestedTone = "Hào hùng, mạnh mẽ",
            SuggestedVoice = "vi-VN-Neural2-D"
        },
        new TopicCard
        {
            Icon = "🧬",
            Title = "Khoa học viễn tưởng",
            Description = "Công nghệ tương lai, AI, vũ trụ",
            SuggestedStyleKey = "cyberpunk",
            SuggestedTone = "Bí ẩn, tương lai",
            SuggestedVoice = "vi-VN-Neural2-D"
        },
        new TopicCard
        {
            Icon = "🎭",
            Title = "Cổ tích / Truyền thuyết",
            Description = "Truyện dân gian, thần thoại Việt Nam",
            SuggestedStyleKey = "oil-painting",
            SuggestedTone = "Trang nghiêm, cổ kính",
            SuggestedVoice = "vi-VN-Wavenet-A"
        },
        new TopicCard
        {
            Icon = "💰",
            Title = "Kinh doanh / Động lực",
            Description = "Bài học kinh doanh, motivation, thành công",
            SuggestedStyleKey = "realistic-photo",
            SuggestedTone = "Truyền cảm hứng, mạnh mẽ",
            SuggestedVoice = "vi-VN-Neural2-D"
        },
        new TopicCard
        {
            Icon = "🌍",
            Title = "Du lịch / Khám phá",
            Description = "Khám phá địa danh, văn hóa, thiên nhiên",
            SuggestedStyleKey = "realistic-photo",
            SuggestedTone = "Nhẹ nhàng, khám phá",
            SuggestedVoice = "vi-VN-Wavenet-A"
        },
        new TopicCard
        {
            Icon = "🍳",
            Title = "Ẩm thực / Nấu ăn",
            Description = "Công thức, review ẩm thực Việt và thế giới",
            SuggestedStyleKey = "realistic-photo",
            SuggestedTone = "Vui tươi, sôi động",
            SuggestedVoice = "vi-VN-Wavenet-A"
        },
        new TopicCard
        {
            Icon = "🎮",
            Title = "Gaming / Công nghệ",
            Description = "Review game, xu hướng tech, tin công nghệ",
            SuggestedStyleKey = "pixel-art",
            SuggestedTone = "Sôi động, năng lượng",
            SuggestedVoice = "vi-VN-Wavenet-D"
        }
    };
}
