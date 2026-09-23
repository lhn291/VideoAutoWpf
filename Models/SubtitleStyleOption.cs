namespace VideoAutoWpf.Models;

public class SubtitleStyleOption
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string DefaultFont { get; set; } = "Segoe UI Bold";
    public string SampleColor { get; set; } = "#ffffff";
    public string SampleBackground { get; set; } = "Transparent";
    public string SampleBorder { get; set; } = "Transparent";

    public static List<SubtitleStyleOption> GetAllOptions() => new()
    {
        new SubtitleStyleOption
        {
            Id = "cinematic",
            Name = "Điện Ảnh Cổ Điển (Cinematic)",
            Description = "Chữ trắng viền đen đổ bóng, chuẩn phim tài liệu & điện ảnh",
            Icon = "🎬",
            DefaultFont = "Segoe UI Bold",
            SampleColor = "#ffffff"
        },
        new SubtitleStyleOption
        {
            Id = "boxed",
            Name = "Hộp Nổi Bật (Viral Shorts/TikTok)",
            Description = "Chữ nằm trên thẻ nền đen mờ bo góc, độ tương phản cao trên mọi ảnh",
            Icon = "📱",
            DefaultFont = "Arial Bold",
            SampleColor = "#ffffff",
            SampleBackground = "#99000000",
            SampleBorder = "#313244"
        },
        new SubtitleStyleOption
        {
            Id = "viral_yellow",
            Name = "Vàng Viền Đậm (Viral Hook)",
            Description = "Chữ vàng viền đen dày dặn, bắt mắt kích thích giữ chân người xem",
            Icon = "⚡",
            DefaultFont = "Impact",
            SampleColor = "#ffd43b"
        },
        new SubtitleStyleOption
        {
            Id = "neon",
            Name = "Neon Phát Sáng (Cyberpunk)",
            Description = "Chữ màu xanh Cyan phát sáng viền tối, hiện đại phong cách công nghệ",
            Icon = "🔮",
            DefaultFont = "Consolas",
            SampleColor = "#89dceb",
            SampleBackground = "#3311111b",
            SampleBorder = "#89dceb"
        },
        new SubtitleStyleOption
        {
            Id = "gold",
            Name = "Hoàng Gia Sang Trọng (Elegant Gold)",
            Description = "Chữ vàng champagne quý phái, phù hợp kể chuyện lịch sử, podcast",
            Icon = "📜",
            DefaultFont = "Times New Roman",
            SampleColor = "#f9e2af"
        },
        new SubtitleStyleOption
        {
            Id = "ticker",
            Name = "Chữ Chạy Ngang (News Ticker Crawl)",
            Description = "Dòng chữ chạy từ phải sang trái như bản tin thời sự truyền hình",
            Icon = "📰",
            DefaultFont = "Segoe UI Bold",
            SampleColor = "#ffffff",
            SampleBackground = "#cc11111b",
            SampleBorder = "#89b4fa"
        }
    };
}

public class SubtitleFontOption
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FontFileName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public static List<SubtitleFontOption> GetAllFonts() => new()
    {
        new SubtitleFontOption { Id = "Segoe UI Bold", Name = "Segoe UI Bold", FontFileName = "segoeuib.ttf", Description = "Hiện đại, sắc nét, chuẩn giao diện Fluent" },
        new SubtitleFontOption { Id = "Arial Bold", Name = "Arial Bold", FontFileName = "arialbd.ttf", Description = "Phổ biến toàn cầu, độ tương thích cao" },
        new SubtitleFontOption { Id = "Impact", Name = "Impact", FontFileName = "impact.ttf", Description = "Chữ đậm, phong cách meme và giật tít Shorts" },
        new SubtitleFontOption { Id = "Tahoma Bold", Name = "Tahoma Bold", FontFileName = "tahomabd.ttf", Description = "Gọn gàng, khoảng cách ký tự thoáng, dễ đọc" },
        new SubtitleFontOption { Id = "Consolas", Name = "Consolas", FontFileName = "consola.ttf", Description = "Chữ đơn cách monospace, vibe công nghệ & code" },
        new SubtitleFontOption { Id = "Times New Roman", Name = "Times New Roman", FontFileName = "times.ttf", Description = "Chữ có chân cổ điển, trang trọng, văn học" }
    };
}
