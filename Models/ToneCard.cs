namespace VideoAutoWpf.Models;

public class ToneCard
{
    public string Icon { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public static List<ToneCard> GetDefaultTones() => new()
    {
        new ToneCard { Icon = "😱", Title = "Rùng rợn, ám ảnh", Description = "Giọng kể chậm rãi, căng thẳng, tạo cảm giác sợ hãi" },
        new ToneCard { Icon = "🔮", Title = "Bí ẩn, hồi hộp", Description = "Giọng kể lôi cuốn, giữ suspense, tò mò" },
        new ToneCard { Icon = "😂", Title = "Vui nhộn, hài hước", Description = "Giọng kể sôi động, vui vẻ, gây cười" },
        new ToneCard { Icon = "💕", Title = "Nhẹ nhàng, cảm xúc", Description = "Giọng kể dịu dàng, lãng mạn, chạm đến trái tim" },
        new ToneCard { Icon = "📖", Title = "Trang nghiêm, truyền cảm", Description = "Giọng kể chuyên nghiệp, rõ ràng, truyền đạt kiến thức" },
        new ToneCard { Icon = "⚔️", Title = "Hào hùng, mạnh mẽ", Description = "Giọng kể đầy năng lượng, hùng tráng, gây phấn khích" },
        new ToneCard { Icon = "🌟", Title = "Truyền cảm hứng, mạnh mẽ", Description = "Giọng kể tạo động lực, khích lệ tinh thần" },
        new ToneCard { Icon = "🏛️", Title = "Trang nghiêm, cổ kính", Description = "Giọng kể trang trọng, phù hợp lịch sử, truyền thuyết" },
        new ToneCard { Icon = "🎉", Title = "Sôi động, năng lượng", Description = "Giọng kể nhanh, năng động, cuốn hút giới trẻ" },
        new ToneCard { Icon = "🌿", Title = "Nhẹ nhàng, khám phá", Description = "Giọng kể thư thái, tự nhiên, phù hợp du lịch, thiên nhiên" },
    };
}
