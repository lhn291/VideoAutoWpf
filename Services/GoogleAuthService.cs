using System.IO;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;

namespace VideoAutoWpf.Services;

public class GoogleAuthService
{
    private GoogleCredential? _credential;
    private string? _projectId;
    private string? _credentialsFilePath;

    public string ProjectId => _projectId ?? "text-to-speech-498915";
    public string? CredentialsFilePath => _credentialsFilePath;
    public bool IsAuthenticated => _credential != null;

    public GoogleAuthService()
    {
        InitializeCredentials();
    }

    public bool InitializeCredentials(string? customPath = null)
    {
        try
        {
            var path = LocateKeyFile(customPath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            _credentialsFilePath = path;
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("project_id", out var projProp))
            {
                _projectId = projProp.GetString();
            }

#pragma warning disable CS0618
            _credential = GoogleCredential.FromFile(path)
                .CreateScoped("https://www.googleapis.com/auth/cloud-platform");
#pragma warning restore CS0618

            return true;
        }
        catch
        {
            _credential = null;
            return false;
        }
    }

    private static string? LocateKeyFile(string? customPath)
    {
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
            return customPath;

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        // 1. Thư mục Config/ chuẩn của project
        var configKey = Path.Combine(baseDir, "Config", "google_credentials.json");
        if (File.Exists(configKey)) return configKey;

        // 2. Thư mục Config/ trong thư mục nguồn khi debug từ VS Code
        var srcConfigKey = Path.Combine(baseDir, "..", "..", "..", "Config", "google_credentials.json");
        if (File.Exists(srcConfigKey)) return Path.GetFullPath(srcConfigKey);

        // 3. Thư mục gốc app
        var localKey = Path.Combine(baseDir, "google_credentials.json");
        if (File.Exists(localKey)) return localKey;

        var srcKey = Path.Combine(baseDir, "..", "..", "..", "google_credentials.json");
        if (File.Exists(srcKey)) return Path.GetFullPath(srcKey);

        // 4. File key từ tool VideoAuto cũ
        var desktopOldKey = @"C:\Users\TY\Desktop\TOOL\TOOL\VideoAuto\text-to-speech-498915-845c07e92ddf.json";
        if (File.Exists(desktopOldKey)) return desktopOldKey;

        // 5. Biến môi trường GOOGLE_APPLICATION_CREDENTIALS
        var envKey = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
        if (!string.IsNullOrWhiteSpace(envKey) && File.Exists(envKey))
            return envKey;

        return null;
    }

    public GoogleCredential? GetCredential()
    {
        if (_credential == null)
            InitializeCredentials();

        return _credential;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        var cred = GetCredential();
        if (cred == null)
            throw new InvalidOperationException("Chưa cấu hình Google Service Account credentials. Vui lòng kiểm tra file key json.");

        var token = await cred.UnderlyingCredential.GetAccessTokenForRequestAsync();
        return token;
    }
}
