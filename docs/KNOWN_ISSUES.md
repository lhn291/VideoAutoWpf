# CÁC VẤN ĐỀ ĐÃ BIẾT, HẠN CHẾ VÀ RỦI RO KỸ THUẬT
**Dự án:** VideoAutoWpf  
**Vai trò:** Senior Software Architect & .NET Technical Lead  
**Tài liệu:** `docs/KNOWN_ISSUES.md`

---

## 1. Xác Minh Về Module "TFT Highlight" (Teamfight Tactics)

> [!IMPORTANT]
> **KẾT QUẢ KIỂM TRA MÃ NGUỒN THỰC TẾ:**  
> Trong toàn bộ repository `VideoAutoWpf` (tất cả các nhánh, commit history, file `.cs`, `.xaml`, `.sql`), **HOÀN TOÀN KHÔNG CÓ BẤT KỲ MÃ NGUỒN NÀO** liên quan đến việc đọc video trận đấu, nhận diện highlight hoặc cắt video game Đấu Trường Chân Lý (TFT).  
> 
> Từ khóa `highlight` xuất hiện trong source code chỉ được sử dụng duy nhất cho chức năng:
> - **Karaoke Text Highlighting:** Đổi màu chữ vàng/trắng theo thời gian thực khớp với giọng đọc thuyết minh trong `AssSubtitleService.cs` và `FFmpegService.cs`.
> 
> *Nếu bạn dự định phát triển tính năng tự động cắt highlight trận đấu TFT, đây là một tính năng hoàn toàn mới (Chưa triển khai) và cần phải được xây dựng từ đầu.*

---

## 2. Các Lỗi và Xung Đột Môi Trường Đang Tồn Tại

### 2.1. Lỗi Visual Studio Roslyn Out-of-Process Crash (`Exit code 1073807364`)
- **Triệu chứng:** Khi mở solution bằng Visual Studio 2022, xuất hiện các thanh thông báo màu vàng/xanh:
  - `Feature 'Extension message handler' is currently unavailable due to an internal error. Exit code 1073807364`
  - `Feature 'Solution Events' is currently unavailable due to an internal error. Exit code 1073807364`
  - `Feature 'Asset synchronization' is currently unavailable due to an internal error. Exit code 1073...`
- **Nguyên nhân kỹ thuật:**
  1. Định dạng Solution mới `.slnx` (XML-based) được giới thiệu trên VS 17.10+ đôi khi gây crash tiến trình phân tích ngầm `ServiceHub.Host.dotnet.x64.exe` nếu bộ nhớ đệm ComponentModelCache bị hỏng hoặc chưa bật Preview Features.
  2. Sự hiện diện của .NET 11 Preview SDK (`11.0.100-rc.3`) trên máy khiến analyzer của Visual Studio 2022 gặp ngoại lệ không xử lý được.
- **Biện pháp xử lý:**
  - Xóa cache: `Remove-Item -Recurse -Force "$env:LOCALAPPDATA\Microsoft\VisualStudio\17.0_*\ComponentModelCache"` và thư mục `.vs`.
  - Mở trực tiếp `VideoAutoWpf.csproj` thay vì `VideoAutoWpf.slnx`.
  - Biên dịch và chạy hoàn toàn ổn định qua Terminal CLI (`dotnet run --project VideoAutoWpf.csproj` hoặc `Chay_App.bat`).

### 2.2. Xung Đột Windows 11 Smart App Control / WDAC (`0x800711C7`)
- **Triệu chứng:** Khi chạy `dotnet test` hoặc nháy đúp tệp `.exe` mới sinh, hệ thống báo lỗi:
  `System.IO.FileLoadException: An Application Control policy has blocked this file. (0x800711C7)`.
- **Nguyên nhân kỹ thuật:** Chính sách bảo mật Windows Defender Application Control (WDAC) hoặc Smart App Control trên Windows 11 tự động chặn nạp các file DLL/EXE chưa có chữ ký số (Code Signing Certificate) trong thư mục `bin\Debug`.
- **Biện pháp xử lý:**
  - Chạy app thông qua `Chay_App.bat` (lệnh `dotnet bin\Debug\...\VideoAutoWpf.dll`).
  - Thêm thư mục dự án vào danh sách ngoại trừ (Exclusions) của Windows Defender Antivirus.

---

## 3. Cảnh Báo Mã Nguồn & Công Nghệ Lỗi Thời (Code Warnings & Deprecations)

### 3.1. Cảnh báo `GoogleCredential.FromStream` lỗi thời (Obsolete Warning CS0618)
- **Vị trí:** `Services/GoogleDriveService.cs`, dòng 146.
- **Chi tiết:** Phương thức `GoogleCredential.FromStream(stream)` bị Google đánh dấu deprecated do rủi ro bảo mật trong thư viện `Google.Apis.Auth`.
- **Đề xuất khắc phục:** Chuyển sang sử dụng `Google.Apis.Auth.OAuth2.CredentialFactory` theo khuyến nghị của Google SDK 1.77+.

### 3.2. Cảnh báo Nullable Reference (CS8602 & CS8601)
- **Vị trí:**
  - `Services/YouTubeAnalyticsService.cs` (dòng 135, 137, 445): Truy xuất thuộc tính nullable của YouTube API response mà chưa qua toán tử `?`.
  - `ViewModels/SeriesCreatorViewModel.cs` (dòng 902): Gán đối tượng có khả năng null vào thuộc tính non-null.
- **Mức độ:** Cảnh báo ở mức Info/Warning, không làm ngắt quá trình build. Cần bổ sung kiểm tra null để tăng tính an toàn (defensive programming).

---

## 4. Hạn Mức Hạn Ngạch và Rate Limiting (API Quotas & Throttling)

### 4.1. Vertex AI Imagen 3 / Gemini Image
- **Hạn mức:** Google Cloud mặc định giới hạn tài khoản ở mức 5 requests / phút (RPM) cho endpoint sinh ảnh Vertex AI.
- **Giải pháp trong code:** `VertexImagenService.cs` đã tích hợp Sliding Window Rate Limiter giới hạn ở mức **4 RPM** kèm cơ chế retry exponential backoff (nghỉ 15s - 30s nếu gặp mã HTTP 429). Khi chạy batch render cho chuỗi nhiều video, thời gian tạo ảnh sẽ mất khoảng 15-20 giây cho mỗi phân cảnh.

### 4.2. Quota YouTube Data API v3
- **Hạn mức:** Mỗi dự án Google Cloud chỉ có 10,000 units quota mỗi ngày. Mỗi lần upload 1 video tốn 1,600 units. Do đó, một ngày tài khoản chỉ được upload tối đa khoảng 6 video qua API.
- **Khuyến nghị:** Đối với quy mô lớn, nên kết hợp tải video lên Google Drive và upload bán tự động hoặc xin nâng hạn ngạch YouTube API.

### 4.3. TikTok Content Posting API v2
- **Hạn mức:** TikTok kiểm soát rất chặt chẽ tài khoản Developer chưa được duyệt App Review. Nếu ứng dụng chưa qua kiểm định của TikTok, API chỉ cho phép đăng video ở chế độ nháp hoặc cho các tài khoản test đã liên kết trong Developer Portal.
