# 🎬 VideoAutoWpf - Hệ Thống Tạo Video Tự Động Bằng AI (.NET 10 WPF)

**VideoAutoWpf** là ứng dụng desktop hiện đại trên nền tảng **WPF (.NET 10.0)** kết hợp sức mạnh của hệ sinh thái **Google Cloud AI** và **FFmpeg** để tự động hóa toàn bộ quy trình sản xuất video ngắn (TikTok, YouTube Shorts, Reels) và video dài (YouTube chuẩn 16:9).

---

## 🌟 Tính Năng Nổi Bật

### 1. 🤖 Soạn & Lên Kế Hoạch Kịch Bản AI 2 Giai Đoạn (Gemini 2.5 Flash)
- **Giai đoạn 1 (Lên kế hoạch thông minh):**
  - Hỗ trợ chọn nhanh các thể loại kịch bản hot: *Lịch sử hào hùng, Thần thoại kỳ ảo, Trinh thám ly kỳ, Khoa học viễn tưởng, Triết lý sống, Cổ tích nhân văn...*
  - AI tự động phân tích ý tưởng của bạn và đề xuất: Tóm tắt cốt truyện, số phân cảnh tối ưu, tone giọng dẫn dắt, giọng đọc phù hợp, phong cách mỹ thuật (Art Style), hiệu ứng chuyển động camera và **giai điệu nhạc nền (BGM)**.
  - Người dùng có thể nghe thử giọng đọc, nghe thử nhạc nền, đổi phong cách vẽ và điều chỉnh trước khi tạo.
- **Giai đoạn 2 (Viết kịch bản chi tiết):**
  - Tự động sinh nội dung lời bình (Voice script), prompt tạo hình ảnh chi tiết theo phong cách đã định hình, và gán hiệu ứng camera phù hợp cho từng cảnh.

### 2. 🎨 Sinh Ảnh Minh Họa Chuẩn Điện Ảnh (Vertex AI Imagen 3)
- Kết nối trực tiếp với mô hình tạo ảnh tiên tiến nhất của Google (**Imagen 3**).
- Tùy biến đa dạng phong cách: *Điện ảnh Siêu thực (Cinematic Realistic), Anime Nhật Bản, Sơn dầu Cổ điển (Oil Painting), 3D Pixar Animation, Cyberpunk Neon, Truyện tranh Manga...*
- Tạo ảnh sắc nét theo đúng tỷ lệ khung hình đã chọn (9:16, 16:9, 1:1).

### 3. 🎙️ Lồng Tiếng AI Truyền Cảm (Google Cloud Text-to-Speech)
- Sử dụng các giọng đọc **Neural2** và **WaveNet** chất lượng phòng thu bằng tiếng Việt và đa ngôn ngữ.
- Tính năng nghe thử giọng trực tiếp trong popup với cơ chế khóa nút thông minh, chống trùng lặp âm thanh.

### 4. 🎵 Thư Viện Nhạc Nền BGM Miễn Phí (CC0 Music)
- Tích hợp sẵn 5 bản nhạc nền đa thể loại: *Chill Ambient, Dramatic Mystery, Epic Cinematic, Dark Horror, Upbeat Pulse*.
- Tự động điều chỉnh âm lượng hòa âm (khuyên dùng 10-20%) giúp lời bình AI luôn nổi bật và **tránh bị quét bản quyền Content ID**.
- Nút bấm tiện ích mở ngay thư mục BGM để bạn thả file nhạc `.mp3` của riêng mình, cùng link hướng dẫn tới các kho nhạc an toàn 100% (Pixabay, YouTube Audio Library, Chosic, Suno AI).

### 5. 🎞️ Bộ Dựng Video Tự Động Đỉnh Cao (FFmpeg Engine)
- **Hiệu ứng Camera Ken Burns:** Hỗ trợ đa dạng hiệu ứng zoom/pan mượt mà (*Zoom In, Zoom Out, Pan Trái, Pan Phải, Pan Chéo, Nghiêng Camera...*).
- **Chuyển cảnh mềm mại:** Fade In/Out mờ dần giữa các phân cảnh.
- **Viền tối điện ảnh (Vignette):** Tạo chiều sâu và phong cách chuyên nghiệp cho khung hình.
- Tự động đồng bộ hóa thời lượng hiển thị hình ảnh khớp 100% với file âm thanh thuyết minh của từng cảnh.
- Hòa trộn đa tầng âm thanh (Voice + BGM).

---

## 📋 Yêu Cầu Hệ Thống

- **Hệ điều hành:** Windows 10 / Windows 11 (64-bit).
- **Môi trường phát triển:** .NET 10.0 SDK hoặc Visual Studio 2022+ (đã cài đặt .NET Desktop Development).
- **FFmpeg:** Đã cài đặt và thêm vào biến môi trường `PATH`.
  - *Cách cài nhanh qua PowerShell:* `winget install Gyan.FFmpeg`
- **Tài khoản Google Cloud:** Đã tạo dự án và kích hoạt:
  - Text-to-Speech API
  - Vertex AI API (Imagen & Gemini)

---

## ⚙️ Cài Đặt & Cấu Hình

### Bước 1: Tải mã nguồn dự án
```bash
git clone https://github.com/lhn291/VideoAutoWpf.git
cd VideoAutoWpf/src
```

### Bước 2: Cấu hình khóa bảo mật Google Cloud
1. Vào **Google Cloud Console** > **IAM & Admin** > **Service Accounts**.
2. Tạo hoặc chọn Service Account đã cấp quyền:
   - `Vertex AI User`
   - `Cloud Text-to-Speech Admin` / `User`
3. Vào tab **Keys** > **Add Key** > **Create new key** > chọn **JSON** và tải file về.
4. Đổi tên file vừa tải thành `google_credentials.json` và đặt vào thư mục `src/Config/`:
   ```text
   src/Config/google_credentials.json
   ```
   *(Bạn có thể tham khảo file mẫu `src/Config/google_credentials.example.json`).*

> ⚠️ **Lưu ý bảo mật:** File `google_credentials.json` đã được cấu hình trong `.gitignore` để bảo vệ mã bí mật của bạn, tuyệt đối không commit file này lên GitHub công khai.

---

## 🚀 Hướng Dẫn Khởi Chạy

### Cách 1: Chạy từ Visual Studio
1. Mở file giải pháp `VideoAutoWpf.slnx` trong Visual Studio.
2. Trên thanh công cụ, chọn cấu hình:
   - **`VideoAutoWpf (Bypass SAC)`** *(Khuyên dùng để tránh lỗi Smart App Control trên Windows 11)*
   - Hoặc **`VideoAutoWpf`**
3. Bấm **F5** để khởi chạy ứng dụng.

### Cách 2: Chạy trực tiếp từ Terminal
Mở PowerShell tại thư mục dự án và gõ:
```powershell
dotnet run --project src/VideoAutoWpf.csproj
```

---

## 🎬 Quy Trình Tạo Video Từng Bước

1. **Soạn & Nạp Kịch Bản:**
   - Bấm nút **"✨ Soạn & Nạp Kịch Bản"** trên thanh tiêu đề.
   - Chọn thể loại gợi ý hoặc nhập ý tưởng vào ô nội dung > Bấm **"🎯 Lên Kế Hoạch AI"**.
   - Xem và tinh chỉnh kế hoạch: Nghe thử giọng đọc, chọn hiệu ứng camera, nghe thử nhạc nền > Bấm **"✅ Xác Nhận & Tạo Kịch Bản"**.
   - Kiểm tra danh sách phân cảnh đã sinh > Bấm **"✅ Chấp Nhận Kịch Bản Này"**.

2. **Tạo Âm Thanh & Hình Ảnh:**
   - Bấm **"🎙️ Tạo Toàn Bộ Âm Thanh"** để chuyển văn bản thành giọng đọc.
   - Bấm **"🎨 Tạo Toàn Bộ Ảnh"** để Imagen 3 vẽ ảnh cho tất cả phân cảnh.
   - *(Bạn có thể bấm vào từng phân cảnh để chỉnh sửa câu từ, prompt hoặc tạo lại ảnh/âm thanh riêng nếu muốn).*

3. **Xuất Video Thành Phẩm:**
   - Chọn tỷ lệ khung hình mong muốn (9:16 cho Shorts/TikTok, 16:9 cho YouTube ngang).
   - Chọn thư mục lưu và đặt tên file `.mp4`.
   - Bấm **"🎬 BẮT ĐẦU TẠO VIDEO"**.
   - Theo dõi thanh tiến trình và cửa sổ nhật ký. Khi hoàn tất, bạn có thể bấm **"▶ Mở Xem Video"** hoặc **"📂 Mở Thư Mục"** để nhận video!

---

## 📁 Cấu Trúc Dự Án

```text
src/
├── Assets/
│   └── Bgm/                    # Thư viện nhạc nền MP3 tích hợp sẵn
├── Components/
│   ├── AppHeaderControl.xaml   # Header ứng dụng, nút mở kịch bản
│   ├── ConsoleLogControl.xaml  # Bảng hiển thị tiến trình và log FFmpeg
│   ├── SceneCardControl.xaml   # Thẻ hiển thị chi tiết 1 phân cảnh
│   ├── SceneListControl.xaml   # Danh sách các phân cảnh dạng cuộn
│   └── VideoConfigControl.xaml # Cấu hình đầu ra (Tỷ lệ, Đường dẫn, Nút Render)
├── Config/
│   ├── google_credentials.example.json # File mẫu cấu hình GCP
│   └── google_credentials.json         # File key GCP thật của bạn (ignored)
├── Converters/                 # Các bộ chuyển đổi dữ liệu hiển thị WPF
├── Models/                     # Định nghĩa cấu trúc dữ liệu (Scene, Plan, BGM, Style...)
├── Properties/
│   └── launchSettings.json     # Cấu hình khởi chạy Visual Studio (Bypass SAC)
├── Services/
│   ├── BgmService.cs           # Quản lý và phát nhạc nền
│   ├── FFmpegService.cs        # Engine dựng, ghép ảnh, hiệu ứng và mix audio
│   ├── GeminiScriptService.cs  # Dịch vụ AI lên kế hoạch và viết kịch bản
│   ├── GoogleAuthService.cs    # Xác thực OAuth2 / Service Account
│   ├── GoogleTtsService.cs     # Tổng hợp giọng nói Google Cloud TTS
│   └── VertexImagenService.cs  # Sinh ảnh minh họa Imagen 3
├── ViewModels/
│   ├── MainViewModel.cs        # Điều phối chính toàn bộ luồng ứng dụng
│   └── ScriptGeneratorViewModel.cs # Logic cửa sổ soạn kịch bản AI
└── Views/
    ├── MainWindow.xaml         # Cửa sổ chính
    └── ScriptGeneratorWindow.xaml # Cửa sổ soạn nạp kịch bản popup
```

---

## 🔧 Xử Lý Sự Cố Thường Gặp

| Lỗi | Nguyên nhân | Cách khắc phục |
| :--- | :--- | :--- |
| **`0x800711C7` (FileLoadException)** | Tính năng Smart App Control của Windows 11 chặn nạp DLL | Chọn chạy bằng profile **`VideoAutoWpf (Bypass SAC)`** trong Visual Studio, hoặc mở `ms-settings:developers` và bật **Developer Mode**. |
| **FFmpeg không hoạt động** | Chưa cài đặt hoặc chưa gán vào biến môi trường PATH | Chạy lệnh `winget install Gyan.FFmpeg` trong PowerShell, sau đó khởi động lại máy hoặc IDE. |
| **Lỗi xác thực Google Cloud (401/403)** | File `google_credentials.json` sai hoặc chưa cấp quyền API | Kiểm tra lại key JSON và đảm bảo đã bật **Text-to-Speech API** và **Vertex AI API** trên GCP Console. |
| **Lỗi mạng khi tải nhạc BGM** | Thư viện ban đầu chưa được tải | Ứng dụng tích hợp sẵn bộ phát tự sinh harmonic tone và tự động quét các file `.mp3` trong thư mục `Assets/Bgm`. |

---

## 📄 Bản Quyền & Giấy Phép

Dự án được phát triển phục vụ mục đích tự động hóa sáng tạo nội dung. Mọi file âm thanh mẫu kèm theo đều thuộc miền công cộng (CC0 Public Domain).