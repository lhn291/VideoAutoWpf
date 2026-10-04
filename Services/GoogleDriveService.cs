using System.IO;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Upload;
using VideoAutoWpf.Models;
using File = Google.Apis.Drive.v3.Data.File;

namespace VideoAutoWpf.Services;

public class GoogleDriveService
{
    private readonly GoogleAuthService _authService;
    private DriveService? _driveService;
    private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "drive_config.txt");

    public GoogleDriveService(GoogleAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Lấy email của Service Account từ file credentials để người dùng tiện chia sẻ folder Drive
    /// </summary>
    public string? GetServiceAccountEmail()
    {
        try
        {
            var keyPath = _authService.CredentialsFilePath;
            if (!string.IsNullOrEmpty(keyPath) && System.IO.File.Exists(keyPath))
            {
                var json = System.IO.File.ReadAllText(keyPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("client_email", out var emailProp))
                {
                    return emailProp.GetString();
                }
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Kiểm tra kết nối tới Google Drive API và quyền truy cập thư mục
    /// </summary>
    public async Task<(bool success, string message)> TestConnectionAsync(string? targetFolderId = null, CancellationToken ct = default)
    {
        try
        {
            var service = GetDriveService();
            if (!string.IsNullOrWhiteSpace(targetFolderId))
            {
                var getReq = service.Files.Get(targetFolderId.Trim());
                getReq.Fields = "id, name, mimeType, capabilities/canAddChildren";
                var f = await getReq.ExecuteAsync(ct);
                if (f.MimeType != "application/vnd.google-apps.folder")
                {
                    return (false, $"ID này là file '{f.Name}', không phải thư mục!");
                }
                var canAdd = f.Capabilities?.CanAddChildren ?? true;
                if (!canAdd)
                {
                    return (false, $"Đã tìm thấy thư mục '{f.Name}', nhưng Service Account chưa được cấp quyền Người chỉnh sửa (Editor)!");
                }
                return (true, $"✅ Kết nối thành công! Đã kết nối thư mục: '{f.Name}' (Có quyền ghi tải lên).");
            }
            else
            {
                var aboutReq = service.About.Get();
                aboutReq.Fields = "user";
                var about = await aboutReq.ExecuteAsync(ct);
                return (true, $"✅ Kết nối Google Drive thành công qua Service Account ({about.User?.EmailAddress ?? "OK"})!");
            }
        }
        catch (Google.GoogleApiException gEx)
        {
            if (gEx.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return (false, "Không tìm thấy thư mục với ID đã nhập. Hãy đảm bảo bạn đã bấm 'Chia sẻ' thư mục đó cho Service Account email!");
            }
            return (false, $"Lỗi Google Drive API ({gEx.HttpStatusCode}): {gEx.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi kết nối Drive: {ex.Message}");
        }
    }

    /// <summary>
    /// Lấy hoặc lưu ID thư mục Google Drive đích (thư mục được chia sẻ từ Drive cá nhân cho Service Account)
    /// </summary>
    public static string? LoadTargetFolderId()
    {
        var paths = new[]
        {
            ConfigPath,
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Config", "drive_config.txt")
        };

        foreach (var p in paths)
        {
            if (System.IO.File.Exists(p))
            {
                var id = System.IO.File.ReadAllText(p).Trim();
                if (!string.IsNullOrWhiteSpace(id)) return id;
            }
        }
        return null;
    }

    public static void SaveTargetFolderId(string folderId)
    {
        var dir = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(ConfigPath, folderId.Trim());

        try
        {
            var srcConfig = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Config", "drive_config.txt"));
            var srcDir = Path.GetDirectoryName(srcConfig);
            if (Directory.Exists(srcDir))
            {
                System.IO.File.WriteAllText(srcConfig, folderId.Trim());
            }
        }
        catch { }
    }

    /// <summary>
    /// Khởi tạo DriveService sử dụng Service Account từ google_credentials.json
    /// </summary>
    private DriveService GetDriveService()
    {
        if (_driveService != null) return _driveService;

        var keyPath = _authService.CredentialsFilePath;
        if (string.IsNullOrEmpty(keyPath) || !System.IO.File.Exists(keyPath))
        {
            throw new FileNotFoundException("Không tìm thấy file google_credentials.json để kết nối Google Drive.");
        }

        using var stream = System.IO.File.OpenRead(keyPath);
        var credential = GoogleCredential.FromStream(stream)
            .CreateScoped(DriveService.ScopeConstants.Drive, DriveService.ScopeConstants.DriveFile);

        _driveService = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "VideoAutoWpf"
        });

        return _driveService;
    }

    /// <summary>
    /// Tìm hoặc tạo mới 1 thư mục trên Google Drive
    /// </summary>
    public async Task<string> GetOrCreateFolderAsync(string folderName, string? parentFolderId = null, CancellationToken ct = default)
    {
        var service = GetDriveService();

        var query = $"mimeType = 'application/vnd.google-apps.folder' and name = '{folderName}' and trashed = false";
        if (!string.IsNullOrWhiteSpace(parentFolderId))
        {
            query += $" and '{parentFolderId}' in parents";
        }

        var listReq = service.Files.List();
        listReq.Q = query;
        listReq.Fields = "files(id, name, webViewLink)";
        var listRes = await listReq.ExecuteAsync(ct);

        if (listRes.Files != null && listRes.Files.Count > 0)
        {
            return listRes.Files[0].Id;
        }

        // Tạo thư mục mới
        var folderMetadata = new File
        {
            Name = folderName,
            MimeType = "application/vnd.google-apps.folder"
        };

        if (!string.IsNullOrWhiteSpace(parentFolderId))
        {
            folderMetadata.Parents = new List<string> { parentFolderId };
        }

        var createReq = service.Files.Create(folderMetadata);
        createReq.Fields = "id, name, webViewLink";
        var folder = await createReq.ExecuteAsync(ct);

        // Mở quyền truy cập link cho thư mục
        try
        {
            var perm = new Permission
            {
                Type = "anyone",
                Role = "reader"
            };
            await service.Permissions.Create(perm, folder.Id).ExecuteAsync(ct);
        }
        catch { }

        return folder.Id;
    }

    /// <summary>
    /// Tải video lên Google Drive kèm báo cáo tiến độ % và lấy link xem trực tiếp
    /// </summary>
    public async Task<DriveUploadResult> UploadVideoAsync(
        string localVideoPath,
        string? seriesOrFolderName = null,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        if (!System.IO.File.Exists(localVideoPath))
        {
            return new DriveUploadResult
            {
                Success = false,
                ErrorMessage = $"File video không tồn tại: {localVideoPath}"
            };
        }

        try
        {
            var service = GetDriveService();

            // Xác định thư mục cha
            string? parentFolderId = LoadTargetFolderId();

            if (!string.IsNullOrWhiteSpace(seriesOrFolderName))
            {
                parentFolderId = await GetOrCreateFolderAsync(seriesOrFolderName, parentFolderId, ct);
            }
            else if (string.IsNullOrWhiteSpace(parentFolderId))
            {
                parentFolderId = await GetOrCreateFolderAsync("VideoAuto_Outputs", null, ct);
            }

            var fileName = Path.GetFileName(localVideoPath);
            var fileMetadata = new File
            {
                Name = fileName,
                Description = $"Rendered by VideoAuto .NET lúc {DateTime.Now:dd/MM/yyyy HH:mm:ss}"
            };

            if (!string.IsNullOrWhiteSpace(parentFolderId))
            {
                fileMetadata.Parents = new List<string> { parentFolderId };
            }

            using var stream = new FileStream(localVideoPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var uploadReq = service.Files.Create(fileMetadata, stream, "video/mp4");
            uploadReq.Fields = "id, name, webViewLink, webContentLink, parents";

            uploadReq.ProgressChanged += (IUploadProgress p) =>
            {
                switch (p.Status)
                {
                    case UploadStatus.Uploading:
                        if (stream.Length > 0)
                        {
                            var pct = (double)p.BytesSent / stream.Length * 100.0;
                            progress?.Report(Math.Min(99.0, pct));
                        }
                        break;
                    case UploadStatus.Completed:
                        progress?.Report(100.0);
                        break;
                }
            };

            var uploaded = await uploadReq.UploadAsync(ct);
            if (uploaded.Status != UploadStatus.Completed)
            {
                return new DriveUploadResult
                {
                    Success = false,
                    ErrorMessage = uploaded.Exception?.Message ?? "Tải file lên Google Drive không thành công."
                };
            }

            var fileRes = uploadReq.ResponseBody;

            // Bật quyền xem công khai qua link
            try
            {
                var perm = new Permission
                {
                    Type = "anyone",
                    Role = "reader"
                };
                await service.Permissions.Create(perm, fileRes.Id).ExecuteAsync(ct);
            }
            catch { }

            // Lấy lại thông tin file kèm webViewLink sau khi set permission
            var getReq = service.Files.Get(fileRes.Id);
            getReq.Fields = "id, name, webViewLink, webContentLink";
            var finalFile = await getReq.ExecuteAsync(ct);

            // Lấy link folder
            string? folderLink = null;
            if (!string.IsNullOrWhiteSpace(parentFolderId))
            {
                try
                {
                    var fReq = service.Files.Get(parentFolderId);
                    fReq.Fields = "webViewLink";
                    var fInfo = await fReq.ExecuteAsync(ct);
                    folderLink = fInfo.WebViewLink;
                }
                catch { }
            }

            return new DriveUploadResult
            {
                Success = true,
                FileId = finalFile.Id,
                FileName = finalFile.Name,
                WebViewLink = finalFile.WebViewLink,
                WebContentLink = finalFile.WebContentLink,
                FolderId = parentFolderId,
                FolderLink = folderLink
            };
        }
        catch (Exception ex)
        {
            return new DriveUploadResult
            {
                Success = false,
                ErrorMessage = $"Lỗi khi tải lên Google Drive: {ex.Message}"
            };
        }
    }
}
