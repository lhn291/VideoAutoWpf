namespace VideoAutoWpf.Models;

public class MotionEffectOption
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;

    public static List<MotionEffectOption> AllOptions => GetAllOptions();
    public static List<MotionEffectOption> AllSceneMotionOptions => GetAllSceneMotionOptions();

    public static List<MotionEffectOption> GetAllOptions() => new()
    {
        new MotionEffectOption
        {
            Id = "auto",
            Icon = "🤖",
            Name = "AI Tự động phân tích từng cảnh (Đề xuất)",
            Description = "AI tự động phân tích hành động & góc máy của từng phân cảnh để gán hiệu ứng tối ưu"
        },
        new MotionEffectOption
        {
            Id = "random",
            Icon = "🎲",
            Name = "Ngẫu nhiên luân phiên",
            Description = "Tự động đổi hiệu ứng qua từng phân cảnh giúp video luôn sống động"
        },
        new MotionEffectOption
        {
            Id = "zoom_in",
            Icon = "🔍",
            Name = "Ken Burns (Zoom In)",
            Description = "Phóng to từ từ vào tâm ảnh (1.0x → 1.15x)"
        },
        new MotionEffectOption
        {
            Id = "zoom_out",
            Icon = "🔎",
            Name = "Zoom Out (Toàn cảnh)",
            Description = "Thu nhỏ dần từ cận cảnh 1.15x ra toàn cảnh 1.0x"
        },
        new MotionEffectOption
        {
            Id = "pan_left_right",
            Icon = "➡️",
            Name = "Lia Trái sang Phải",
            Description = "Quét góc máy quay từ trái sang phải chuẩn điện ảnh"
        },
        new MotionEffectOption
        {
            Id = "pan_right_left",
            Icon = "⬅️",
            Name = "Lia Phải sang Trái",
            Description = "Quét góc máy quay từ phải sang trái"
        },
        new MotionEffectOption
        {
            Id = "pan_up",
            Icon = "⬆️",
            Name = "Lia từ Dưới lên (Tilt Up)",
            Description = "Lia từ dưới lên, cực đẹp cho video dọc 9:16 & ảnh nhân vật"
        },
        new MotionEffectOption
        {
            Id = "pan_down",
            Icon = "⬇️",
            Name = "Lia từ Trên xuống (Tilt Down)",
            Description = "Lia máy từ trên xuống dưới"
        },
        new MotionEffectOption
        {
            Id = "zoom_pan_right",
            Icon = "↗️",
            Name = "Zoom In + Lia Phải",
            Description = "Vừa phóng to vừa lia góc phải tạo điểm nhấn kịch tính"
        },
        new MotionEffectOption
        {
            Id = "zoom_pan_left",
            Icon = "↖️",
            Name = "Zoom In + Lia Trái",
            Description = "Vừa phóng to vừa lia góc trái"
        },
        new MotionEffectOption
        {
            Id = "none",
            Icon = "⏹️",
            Name = "Ảnh Tĩnh (Không hiệu ứng)",
            Description = "Giữ nguyên ảnh gốc, xuất video nhanh nhất"
        }
    };

    public static List<MotionEffectOption> GetAllSceneMotionOptions() => new()
    {
        new MotionEffectOption
        {
            Id = "zoom_in",
            Icon = "🔍",
            Name = "Zoom In (Ken Burns)",
            Description = "Phóng to từ từ vào tâm ảnh"
        },
        new MotionEffectOption
        {
            Id = "zoom_out",
            Icon = "🔎",
            Name = "Zoom Out (Toàn cảnh)",
            Description = "Thu nhỏ từ cận cảnh ra toàn cảnh"
        },
        new MotionEffectOption
        {
            Id = "pan_left_right",
            Icon = "➡️",
            Name = "Lia Trái → Phải",
            Description = "Quét góc máy từ trái sang phải"
        },
        new MotionEffectOption
        {
            Id = "pan_right_left",
            Icon = "⬅️",
            Name = "Lia Phải → Trái",
            Description = "Quét góc máy từ phải sang trái"
        },
        new MotionEffectOption
        {
            Id = "pan_up",
            Icon = "⬆️",
            Name = "Lia Dưới ↑ Lên",
            Description = "Lia máy từ dưới lên trên"
        },
        new MotionEffectOption
        {
            Id = "pan_down",
            Icon = "⬇️",
            Name = "Lia Trên ↓ Xuống",
            Description = "Lia máy từ trên xuống dưới"
        },
        new MotionEffectOption
        {
            Id = "zoom_pan_right",
            Icon = "↗️",
            Name = "Zoom + Lia Phải",
            Description = "Phóng to kết hợp lia góc phải"
        },
        new MotionEffectOption
        {
            Id = "zoom_pan_left",
            Icon = "↖️",
            Name = "Zoom + Lia Trái",
            Description = "Phóng to kết hợp lia góc trái"
        },
        new MotionEffectOption
        {
            Id = "none",
            Icon = "⏹️",
            Name = "Ảnh Tĩnh",
            Description = "Không hiệu ứng chuyển động"
        }
    };

    public override string ToString() => $"{Icon} {Name}";
}
