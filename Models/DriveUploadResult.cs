namespace VideoAutoWpf.Models;

public class DriveUploadResult
{
    public bool Success { get; set; }
    public string? FileId { get; set; }
    public string? FileName { get; set; }
    public string? WebViewLink { get; set; }
    public string? WebContentLink { get; set; }
    public string? FolderId { get; set; }
    public string? FolderLink { get; set; }
    public string? ErrorMessage { get; set; }
}
