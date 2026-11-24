# HƯỚNG DẪN CÀI ĐẶT VÀ CHẠY DỰ ÁN TỪ ĐẦU
**Dự án:** VideoAutoWpf  
**Vai trò:** Senior Software Architect & .NET Technical Lead  
**Tài liệu:** `docs/SETUP_GUIDE.md`

---

## 1. Chuẩn Bị Môi Trường Hệ Thống

| Yêu cầu phần cứng / phần mềm | Khuyến nghị tối thiểu | Khuyến nghị tối ưu (Production) |
|---|---|---|
| **Hệ điều hành** | Windows 10 x64 (Build 19041+) | Windows 11 Pro 64-bit |
| **Bộ xử lý (CPU)** | Intel Core i5 / AMD Ryzen 5 (4 lõi) | AMD Ryzen 5 5600G / Intel Core i7 (6+ lõi) |
| **Bộ nhớ RAM** | 8 GB RAM | 16 GB - 32 GB RAM (để FFmpeg render đa luồng nhanh) |
| **Ổ cứng lưu trữ** | 10 GB dung lượng trống | SSD NVMe còn trống tối thiểu 50 GB |
| **Môi trường mạng** | Kết nối Internet ổn định | Băng thông >= 50 Mbps |

---

## 2. Cài Đặt .NET SDK và Các Thành Phần Phụ Thuộc

### Bước 2.1: Cài đặt .NET 10.0 / 11.0 SDK
Dự án được cấu hình mục tiêu là `net10.0-windows`. Kiểm tra phiên bản SDK hiện có trên máy bằng PowerShell:
```powershell
dotnet --list-sdks
```
Nếu chưa có .NET 10 SDK, tải về từ trang chủ Microsoft:
- Tải gói cài đặt **.NET 10.0 SDK x64** hoặc **.NET 11.0 Preview SDK x64**.
- Sau khi cài đặt, mở terminal mới và kiểm tra lại:
```powershell
dotnet --version
```

### Bước 2.2: Kiểm tra công cụ FFmpeg và FFprobe
FFmpeg là công cụ dựng video cốt lõi. Dự án đã tích hợp sẵn tệp nhị phân `ffmpeg.exe` và `ffprobe.exe` trong thư mục:
`c:\TOOL\VideoAutoWpf\bin\Debug\net10.0-windows\`
Kiểm tra tính sẵn sàng bằng lệnh:
```powershell
& "c:\TOOL\VideoAutoWpf\bin\Debug\net10.0-windows\ffmpeg.exe" -version
```

---

## 3. Cấu Hình Tệp Xác Thực và Dữ Liệu Ban Đầu

1. **Khóa Google Cloud Service Account:**
   - Đặt tệp `google_credentials.json` vào thư mục gốc:
     `c:\TOOL\VideoAutoWpf\google_credentials.json`
   - Hoặc copy vào thư mục thực thi:
     `c:\TOOL\VideoAutoWpf\bin\Debug\net10.0-windows\google_credentials.json`
2. **Khóa Gemini API (Tùy chọn bổ sung):**
   - Thiết lập biến môi trường:
     ```powershell
     [System.Environment]::SetEnvironmentVariable('GEMINI_API_KEY', 'AIzaSy...', 'User')
     ```
3. **Tài nguyên tĩnh (Assets):**
   - Đảm bảo thư mục `Assets/Bgm/` có đầy đủ 5 file MP3 mẫu.
   - Đảm bảo thư mục `Assets/StylePreviews/` có 10 ảnh preview phong cách.

---

## 4. Chuẩn Bị Cơ Sở Dữ Liệu SQLite

Hệ thống sử dụng cơ sở dữ liệu SQLite nhúng, bạn **không cần cài đặt SQL Server hay MySQL**.
- Khi ứng dụng khởi động lần đầu, `DatabaseService.cs` sẽ tự động tạo tệp `automation.db` và khởi tạo toàn bộ schema bảng (`automation_jobs`, `app_settings`, `audit_logs`).
- Cấu hình Write-Ahead Logging (WAL) được tự động kích hoạt để tối ưu hiệu năng ghi đĩa đồng thời.

---

## 5. Biên Dịch Dự Án (Build Solution)

### Câu lệnh Build chuẩn xác từ Terminal (PowerShell):
```powershell
cd c:\TOOL\VideoAutoWpf
dotnet build VideoAutoWpf.csproj -c Debug
```
Kết quả thành công sẽ xuất hiện:
```text
Build succeeded.
    0 Error(s)
    5 Warning(s)
Time Elapsed 00:00:08
```

---

## 6. Hướng Dẫn Chạy Ứng Dụng

### Cách 1: Chạy bằng Terminal (Khuyên dùng - Nhanh nhất và ổn định nhất)
Chạy trực tiếp project:
```powershell
cd c:\TOOL\VideoAutoWpf
dotnet run --project VideoAutoWpf.csproj
```
Hoặc chạy tệp script đã cấu hình sẵn:
```powershell
.\Chay_App.bat
```
*(Script `Chay_App.bat` chạy file DLL trực tiếp qua `start "" dotnet bin\Debug\net10.0-windows\VideoAutoWpf.dll`, bỏ qua sự can thiệp của Smart App Control trên Windows 11).*

### Cách 2: Chạy bằng Visual Studio 2022
1. Mở Visual Studio 2022 (khuyến nghị phiên bản 17.10 trở lên để hỗ trợ tệp `.slnx`).
2. Chọn **File -> Open -> Project/Solution** và chọn `VideoAutoWpf.slnx` (hoặc mở trực tiếp `VideoAutoWpf.csproj`).
3. Chọn cấu hình **Debug** và mục tiêu **Any CPU** hoặc **x64**.
4. Bấm phím **F5** hoặc nút **Start (Play xanh)** trên thanh công cụ.

---

## 7. Xử Lý Những Lỗi Thường Gặp (Troubleshooting)

### Lỗi 1: Visual Studio báo lỗi "Feature 'Extension message handler' is currently unavailable... Exit code 1073807364"
- **Nguyên nhân:** Tiến trình ServiceHub Out-of-Process (Roslyn Code Analysis) của Visual Studio bị crash do bộ đệm MEF ComponentModelCache bị lỗi, hoặc xung đột giữa định dạng `.slnx` với phiên bản Visual Studio hiện tại.
- **Cách khắc phục triệt để:**
  1. Đóng hoàn toàn Visual Studio.
  2. Mở PowerShell và xóa thư mục cache của Visual Studio:
     ```powershell
     Remove-Item -Recurse -Force "$env:LOCALAPPDATA\Microsoft\VisualStudio\17.0_*\ComponentModelCache"
     Remove-Item -Recurse -Force "c:\TOOL\VideoAutoWpf\.vs"
     ```
  3. Mở lại Visual Studio bằng cách mở trực tiếp file `VideoAutoWpf.csproj` thay vì `VideoAutoWpf.slnx`.
  4. Nếu vẫn còn thông báo, bạn hoàn toàn có thể bỏ qua thanh info bar này vì đây chỉ là lỗi của tiện ích mở rộng IDE, mã nguồn `VideoAutoWpf.csproj` vẫn build và chạy hoàn toàn bình thường qua `dotnet run` hoặc `Chay_App.bat`.

### Lỗi 2: Lỗi Smart App Control / Windows Defender "An Application Control policy has blocked this file (0x800711C7)"
- **Nguyên nhân:** Windows 11 Smart App Control chặn các file `.exe` hoặc `.dll` mới được sinh ra trong thư mục `bin\Debug`.
- **Cách khắc phục:**
  - Không chạy trực tiếp file `.exe` bằng nháy đúp.
  - Sử dụng lệnh `dotnet run --project VideoAutoWpf.csproj` hoặc chạy qua `Chay_App.bat`.
  - Trong Windows Defender -> Virus & threat protection -> Exclusions: Thêm thư mục `c:\TOOL\VideoAutoWpf` vào danh sách ngoại trừ.

### Lỗi 3: Ứng dụng thông báo "VideoAutoWpf đã đang chạy trong một phiên khác..."
- **Nguyên nhân:** Cơ chế Single-Instance Mutex phát hiện một tiến trình trước đó chưa được giải phóng hoặc đang chạy ngầm trong Task Manager.
- **Cách khắc phục:**
  - Mở PowerShell và tắt tiến trình cũ:
    ```powershell
    Stop-Process -Name VideoAutoWpf -Force -ErrorAction SilentlyContinue
    ```
  - Sau đó khởi động lại ứng dụng.

### Lỗi 4: Lỗi 404 hoặc "Model not found" khi gọi Gemini
- **Nguyên nhân:** Khóa API hoặc tài khoản Google Cloud sử dụng tên model đã bị Google khai tử (như `gemini-2.5-flash`).
- **Cách khắc phục:** Mã nguồn trong `GeminiScriptService.cs` đã được cập nhật sang danh sách model mới nhất: `gemini-3.5-flash-lite`, `gemini-flash-lite-latest`, `gemini-3.5-flash`. Hãy kiểm tra lại biến môi trường `GEMINI_API_KEY`.
