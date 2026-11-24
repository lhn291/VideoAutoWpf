# HƯỚNG DẪN CẤU HÌNH HỆ THỐNG
**Dự án:** VideoAutoWpf  
**Vai trò:** Senior Software Architect & .NET Technical Lead  
**Tài liệu:** `docs/CONFIGURATION.md`

---

## 1. Cấu Hình Xác Thực Google Cloud (`google_credentials.json`)

Hệ thống sử dụng tệp khóa Service Account định dạng JSON để xác thực với toàn bộ hệ sinh thái Google Cloud Platform (TTS, STT, Vertex AI Imagen, Google Drive, YouTube Analytics).

### 1.1. Vị trí đặt tệp
Tệp khóa phải có tên chính xác là `google_credentials.json` và được đặt tại một trong các vị trí sau (theo thứ tự ưu tiên mà `GoogleAuthService.cs` tìm kiếm):
1. Thư mục chạy ứng dụng: `c:\TOOL\VideoAutoWpf\bin\Debug\net10.0-windows\google_credentials.json`
2. Thư mục gốc dự án: `c:\TOOL\VideoAutoWpf\google_credentials.json`
3. Biến môi trường hệ điều hành: `GOOGLE_APPLICATION_CREDENTIALS` trỏ đến đường dẫn tệp.

### 1.2. Các API cần kích hoạt trên Google Cloud Console
Đăng nhập vào [Google Cloud Console](https://console.cloud.google.com/) trên dự án của bạn và kích hoạt các API sau:
- **Cloud Text-to-Speech API** (Tạo giọng đọc thuyết minh)
- **Cloud Speech-to-Text API v2** (Bóc tách thời gian từng từ)
- **Vertex AI API** (Sinh hình ảnh với Imagen 3 / Gemini Image)
- **Google Drive API** (Tự động tải video thành phẩm lên Cloud Drive)
- **YouTube Data API v3** (Phân tích đối thủ và tải video lên YouTube)

### 1.3. Phân quyền IAM (Service Account Roles)
Gán các quyền tối thiểu sau cho Service Account:
- `Vertex AI User` (hoặc `Vertex AI Administrator`)
- `Cloud Speech Client`
- `Storage Object Viewer / Creator` (nếu dùng bucket tạm)

---

## 2. Cấu Hình Gemini AI (API Key / AI Studio)

`GeminiScriptService.cs` hỗ trợ gọi Gemini API thông qua 2 phương thức:
1. **Thông qua Service Account (Vertex AI):** Tự động kế thừa từ `google_credentials.json` nếu dự án có liên kết billing.
2. **Thông qua API Key Google AI Studio (Khuyên dùng vì tốc độ và hạn mức miễn phí):**
   - Đặt biến môi trường Windows:
     ```powershell
     [System.Environment]::SetEnvironmentVariable('GEMINI_API_KEY', 'AIzaSyYourGeminiApiKey...', 'User')
     ```
   - Hoặc lưu vào bảng `app_settings` trong SQLite:
     ```sql
     INSERT OR REPLACE INTO app_settings (key, value, updated_at) 
     VALUES ('gemini_api_key', 'AIzaSyYourGeminiApiKey...', datetime('now'));
     ```
   - Danh sách model được hỗ trợ tự động luân phiên:
     - `gemini-3.5-flash-lite` (Ưu tiên số 1 - Tốc độ cực nhanh, giá rẻ)
     - `gemini-flash-lite-latest`
     - `gemini-3.5-flash`
     - `gemini-3.8-flash`

---

## 3. Cấu Hình Google Drive Tự Động Sao Lưu

Để video sau khi render tự động tải lên Google Drive của bạn:
1. Tạo một thư mục mới trên Google Drive cá nhân của bạn (ví dụ: `VideoAuto_Output`).
2. Mở thư mục, nhìn lên thanh địa chỉ trình duyệt để lấy **Folder ID**:
   - URL: `https://drive.google.com/drive/folders/1a2b3c4d5e6f7g8h9...` -> ID là: `1a2b3c4d5e6f7g8h9...`
3. Mở tệp `google_credentials.json`, tìm dòng `"client_email": "videoauto-sa@your-project.iam.gserviceaccount.com"`.
4. Quay lại Google Drive, bấm **Chia sẻ (Share)** thư mục vừa tạo và cấp quyền **Người chỉnh sửa (Editor)** cho địa chỉ email `client_email` ở bước 3.
5. Trong ứng dụng `VideoAutoWpf`, dán Folder ID vào ô **Google Drive Folder ID** hoặc lưu vào SQLite:
   ```sql
   INSERT OR REPLACE INTO app_settings (key, value, updated_at) 
   VALUES ('drive_target_folder_id', '1a2b3c4d5e6f7g8h9...', datetime('now'));
   ```

---

## 4. Cấu Hình Telegram Bot Quản Trị & Duyệt Video

Hệ thống sử dụng Telegram Bot để gửi ảnh thumbnail và nhận lệnh `[DUYỆT ĐĂNG]`:

### 4.1. Tạo Bot Telegram
1. Mở ứng dụng Telegram, tìm bot `@BotFather`.
2. Gửi lệnh `/newbot` và làm theo hướng dẫn để đặt tên bot.
3. Nhận mã **Bot Token** (dạng `7123456789:AAFlKj...`).

### 4.2. Lấy Chat ID / User ID của bạn
1. Chat với bot `@userinfobot` trên Telegram để lấy User ID (dạng số nguyên, ví dụ: `123456789`).

### 4.3. Lưu cấu hình vào SQLite
Mở bảng `app_settings` trong SQLite `automation.db` và thêm:
```sql
-- Bot Token
INSERT OR REPLACE INTO app_settings (key, value, updated_at) 
VALUES ('telegram_bot_token', '7123456789:AAFlKj...', datetime('now'));

-- Danh sách ID quản trị viên được phép duyệt (ngăn chặn kẻ lạ bấm duyệt)
INSERT OR REPLACE INTO app_settings (key, value, updated_at) 
VALUES ('telegram_allowed_user_ids', '123456789', datetime('now'));

-- Chat ID nhận thông báo duyệt video
INSERT OR REPLACE INTO app_settings (key, value, updated_at) 
VALUES ('telegram_target_chat_id', '123456789', datetime('now'));
```

---

## 5. Cấu Hình Xuất Bản Mạng Xã Hội (YouTube & TikTok)

### 5.1. Cấu hình YouTube Data API
- `YouTubePublishService` yêu cầu quyền OAuth của kênh YouTube cá nhân để đăng video thật (do Service Account không thể sở hữu kênh YouTube thông thường).
- Để kiểm thử quy trình không tốn API và không đăng thật, bật chế độ **Dry Run Mode**:
  ```sql
  INSERT OR REPLACE INTO app_settings (key, value, updated_at) 
  VALUES ('youtube_dry_run', 'true', datetime('now'));
  ```

### 5.2. Cấu hình TikTok Content Posting API
- Đăng ký ứng dụng tại [TikTok for Developers](https://developers.tiktok.com/).
- Bật tính năng **Content Posting API**.
- Lưu thông tin vào SQLite:
  ```sql
  INSERT OR REPLACE INTO app_settings (key, value, updated_at) 
  VALUES ('tiktok_client_key', 'your_tiktok_client_key', datetime('now'));

  INSERT OR REPLACE INTO app_settings (key, value, updated_at) 
  VALUES ('tiktok_client_secret', 'your_tiktok_client_secret', datetime('now'));

  INSERT OR REPLACE INTO app_settings (key, value, updated_at) 
  VALUES ('tiktok_dry_run', 'true', datetime('now'));
  ```

---

## 6. Cấu Hình Cơ Sở Dữ Liệu SQLite (`automation.db`)

Cơ sở dữ liệu cục bộ được khởi tạo tự động khi ứng dụng chạy lần đầu tại:
`c:\TOOL\VideoAutoWpf\bin\Debug\net10.0-windows\automation.db` (hoặc thư mục chạy app).

### Các bảng dữ liệu chính:
1. `automation_jobs`:
   - `id` (INTEGER PRIMARY KEY AUTOINCREMENT)
   - `title` (TEXT)
   - `status` (TEXT: `Pending`, `Running`, `QualityCheck`, `AwaitingApproval`, `Approved`, `Rejected`, `Uploading`, `Completed`, `Failed`)
   - `video_path` (TEXT)
   - `thumbnail_path` (TEXT)
   - `lease_token` (TEXT - GUID khóa tiến trình)
   - `lease_expires_at` (TEXT ISO8601)
   - `retry_count` (INTEGER)
   - `created_at`, `updated_at`
2. `app_settings`:
   - Lưu trữ các cặp `key` - `value` động cấu hình toàn bộ ứng dụng.
3. `audit_logs`:
   - Ghi lại vết lịch sử mọi hành động của hệ thống (bắt đầu render, kết quả QC, phản hồi Telegram, lỗi ngoại lệ).

---

## 7. Cấu Hình Bảng Tham Số Điều Phối (`AutomationConfig`)

Trong mã nguồn `Models/Automation/AutomationConfig.cs`, các thông số mặc định đã được căn chỉnh tối ưu:
- `HeartbeatIntervalSeconds = 30`: Nhịp tim gia hạn khóa job.
- `LeaseDurationMinutes = 5`: Thời hạn tối đa của một lần khóa; nếu máy sập nguồn, sau 5 phút job sẽ được mở khóa an toàn để chạy lại.
- `MaxRetryCount = 3`: Số lần thử lại tối đa cho mỗi job lỗi.
- `ShutdownGracePeriodSeconds = 60`: Thời gian chờ đếm ngược trước khi tắt máy để admin kịp hủy lệnh qua Telegram.
- `VertexImageRateLimitPerMinute = 4`: Giới hạn tối đa 4 request sinh ảnh mỗi phút để không vượt hạn ngạch Google Cloud.
