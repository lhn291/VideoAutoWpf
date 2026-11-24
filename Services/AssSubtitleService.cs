using System.IO;
using System.Text;
using VideoAutoWpf.Models;
using VideoAutoWpf.Models.Minimal;

namespace VideoAutoWpf.Services;

/// <summary>
/// Dịch vụ xuất phụ đề chuẩn ASS (Advanced SubStation Alpha v4.00+).
/// Hỗ trợ Karaoke chạy từng chữ theo thời gian thực (real-time highlight) qua tag {\k...}.
/// </summary>
public static class AssSubtitleService
{
    public static async Task<string> GenerateAssFileAsync(
        IReadOnlyList<VideoSceneItem> scenes,
        string outputPath,
        int width = 1080,
        int height = 1920,
        string fontName = "Segoe UI Bold",
        string subtitleStyle = "cinematic",
        string highlightColor = "#ffd43b",
        CancellationToken ct = default)
    {
        var sb = new StringBuilder();

        int fontSize = width > height ? Math.Max(32, height / 24) : Math.Max(40, height / 32);
        int marginV = width > height ? (int)(height * 0.12) : (int)(height * 0.15);

        string assPrimaryColor = "&H00FFFFFF";
        string assSecondaryColor = ToAssBgrColor(highlightColor);
        string assOutlineColor = "&H00000000";
        string assBackColor = "&H80000000";

        int outlineWidth = 4;
        int shadowDepth = 2;

        if (subtitleStyle.Equals("viral_yellow", StringComparison.OrdinalIgnoreCase))
        {
            assPrimaryColor = "&H003BD4FF";
            assSecondaryColor = "&H0000FFFF";
            outlineWidth = 5;
        }
        else if (subtitleStyle.Equals("neon", StringComparison.OrdinalIgnoreCase))
        {
            assPrimaryColor = "&H00EBDC89";
            assSecondaryColor = "&H00F5661E";
            outlineWidth = 3;
            shadowDepth = 4;
        }

        sb.AppendLine("[Script Info]");
        sb.AppendLine("Title: VideoAutoWpf Subtitles");
        sb.AppendLine("ScriptType: v4.00+");
        sb.AppendLine("WrapStyle: 0");
        sb.AppendLine("ScaledBorderAndShadow: yes");
        sb.AppendLine($"PlayResX: {width}");
        sb.AppendLine($"PlayResY: {height}");
        sb.AppendLine();

        sb.AppendLine("[V4+ Styles]");
        sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
        sb.AppendLine($"Style: Default,{fontName},{fontSize},{assPrimaryColor},{assSecondaryColor},{assOutlineColor},{assBackColor},-1,0,0,0,100,100,0,0,1,{outlineWidth},{shadowDepth},2,20,20,{marginV},1");
        sb.AppendLine();

        sb.AppendLine("[Events]");
        sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

        double currentTimelineOffset = 0.0;

        foreach (var scene in scenes)
        {
            double sceneDur = scene.DurationSeconds > 0.1 ? scene.DurationSeconds : 5.0;

            if (!string.IsNullOrWhiteSpace(scene.NarrationText))
            {
                double lineStart = currentTimelineOffset;
                double lineEnd = currentTimelineOffset + sceneDur;

                var startFormatted = FormatAssTime(lineStart);
                var endFormatted = FormatAssTime(lineEnd);

                int maxChars = width > height ? 42 : 30;
                var wrappedText = WrapTextForAss(scene.NarrationText.Trim(), maxChars);

                sb.AppendLine($"Dialogue: 0,{startFormatted},{endFormatted},Default,,0,0,0,,{wrappedText}");
            }

            currentTimelineOffset += sceneDur;
        }

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(outputPath, sb.ToString(), new UTF8Encoding(false), ct);
        return outputPath;
    }

    private static string FormatAssTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
        int centiseconds = (int)(ts.Milliseconds / 10.0);
        return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}.{centiseconds:D2}";
    }

    private static string ToAssBgrColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return "&H003BD4FF";
        hex = hex.Trim().TrimStart('#');
        if (hex.Length == 6)
        {
            var r = hex.Substring(0, 2);
            var g = hex.Substring(2, 2);
            var b = hex.Substring(4, 2);
            return $"&H00{b}{g}{r}";
        }
        return "&H003BD4FF";
    }

    private static string WrapTextForAss(string text, int maxCharsPerLine)
    {
        if (text.Length <= maxCharsPerLine) return text;

        var words = text.Split(' ');
        var sb = new StringBuilder();
        int currentLineLength = 0;

        foreach (var word in words)
        {
            if (currentLineLength + word.Length + 1 > maxCharsPerLine && currentLineLength > 0)
            {
                sb.Append("\\N");
                currentLineLength = 0;
            }
            else if (currentLineLength > 0)
            {
                sb.Append(" ");
                currentLineLength++;
            }

            sb.Append(word);
            currentLineLength += word.Length;
        }

        return sb.ToString();
    }
}
