namespace VideoAutoWpf.Models;

public class BgmTrack
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Mood { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string SourceTag { get; set; } = "CC0 / Miễn phí";
    public bool IsBuiltIn { get; set; }
    public bool IsCustom { get; set; }

    public string DisplayText => string.IsNullOrWhiteSpace(SourceTag) 
        ? Title 
        : $"{Title} ({SourceTag})";

    public override string ToString() => DisplayText;
}
