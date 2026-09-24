namespace VideoAutoWpf.Models;

public class StyleCard
{
    public string Key { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Đường dẫn tương đối đến ảnh preview phong cách vẽ (trong thư mục Assets/StylePreviews).
    /// </summary>
    public string PreviewImagePath { get; set; } = string.Empty;

    /// <summary>
    /// Đường dẫn tuyệt đối đến ảnh preview, được tính từ thư mục chạy của ứng dụng.
    /// </summary>
    public string PreviewImageFullPath
    {
        get
        {
            if (string.IsNullOrEmpty(PreviewImagePath)) return string.Empty;
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            return System.IO.Path.Combine(basePath, PreviewImagePath);
        }
    }

    public static List<StyleCard> GetDefaultStyles() => new()
    {
        new StyleCard { Key = "dark-anime", Icon = "🌑", Title = "Dark Anime", Description = "2D anime tối, cel shading, line art Nhật Bản, bầu không khí rùng rợn", PreviewImagePath = @"Assets\StylePreviews\dark-anime.jpg" },
        new StyleCard { Key = "cinematic-horror", Icon = "🎬", Title = "Cinematic Horror", Description = "Phong cách phim kinh dị, 3D siêu thực, sương mù, bóng tối kịch tính", PreviewImagePath = @"Assets\StylePreviews\cinematic-horror.jpg" },
        new StyleCard { Key = "cartoon-3d", Icon = "🧸", Title = "Cartoon 3D", Description = "3D hoạt hình Pixar/Disney, màu sắc tươi sáng, nhân vật dễ thương", PreviewImagePath = @"Assets\StylePreviews\cartoon-3d.jpg" },
        new StyleCard { Key = "cyberpunk", Icon = "🌆", Title = "Cyberpunk", Description = "Anime cyberpunk 2D, neon rực rỡ, tương lai, công nghệ cao", PreviewImagePath = @"Assets\StylePreviews\cyberpunk.jpg" },
        new StyleCard { Key = "horror-cartoon", Icon = "🎃", Title = "Horror Cartoon", Description = "Hoạt hình kinh dị Tim Burton, nét vẽ sketchy, kỳ dị ma quái", PreviewImagePath = @"Assets\StylePreviews\horror-cartoon.jpg" },
        new StyleCard { Key = "oil-painting", Icon = "🖼️", Title = "Oil Painting", Description = "Tranh sơn dầu cổ điển, nét cọ dày, chiaroscuro, ánh sáng kịch tính", PreviewImagePath = @"Assets\StylePreviews\oil-painting.jpg" },
        new StyleCard { Key = "watercolor", Icon = "💧", Title = "Watercolor", Description = "Tranh màu nước, tông pastel mềm mại, viền mực nhẹ, mơ mộng", PreviewImagePath = @"Assets\StylePreviews\watercolor.jpg" },
        new StyleCard { Key = "cartoon-2d", Icon = "✏️", Title = "Cartoon 2D", Description = "Hoạt hình 2D retro, nét đen sạch, màu phẳng, phong cách cổ điển", PreviewImagePath = @"Assets\StylePreviews\cartoon-2d.jpg" },
        new StyleCard { Key = "realistic-photo", Icon = "📷", Title = "Realistic Photo", Description = "Ảnh siêu thực, chất lượng DSLR, ánh sáng tự nhiên, 8K", PreviewImagePath = @"Assets\StylePreviews\realistic-photo.jpg" },
        new StyleCard { Key = "pixel-art", Icon = "👾", Title = "Pixel Art", Description = "Pixel art 16-bit retro, bảng màu hạn chế, phong cách game cổ điển", PreviewImagePath = @"Assets\StylePreviews\pixel-art.jpg" }
    };
}
