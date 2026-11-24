# TỔNG QUAN HỆ THỐNG VIDEOAUTOWPF
**Phiên bản tài liệu:** 1.0 (Tháng 11/2026)  
**Vai trò phụ trách:** Senior Software Architect & .NET Technical Lead  
**Mã nguồn:** `c:\TOOL\VideoAutoWpf`  
**Nhánh hoạt động:** `feature/ky-an-series`

---

## 1. Mục Đích & Tầm Nhìn Dự Án

`VideoAutoWpf` là một nền tảng máy trạm Windows (WPF .NET 10) chuyên biệt, hướng tới tự động hóa hoàn toàn quy trình sáng tạo và xuất bản nội dung video ngắn/dài tập (Shorts, TikTok, YouTube Series) với sự hỗ trợ của Generative AI (Google Gemini, Vertex AI Imagen, Google Cloud TTS/STT, FFmpeg).

Hệ thống được thiết kế đặc thù cho mô hình **Unattended Mini PC** (ví dụ máy tính Mini PC Ryzen 5 5600G chạy liên tục không cần màn hình, tự động bật máy theo lịch BIOS/Task Scheduler, xử lý hàng đợi video, gửi ảnh duyệt qua Telegram Bot, tự động đăng tải đa nền tảng và tự động tắt máy an toàn khi hoàn tất).

### Giá trị cốt lõi giải quyết:
1. **Sản xuất video quy mô lớn (Scale Content):** Tự động hóa từ khâu phân tích kịch bản đối thủ, lập kịch bản phân cảnh, tạo giọng đọc truyền cảm (Neural2), tạo phụ đề Karaoke chuẩn từng từ (Chirp 2 STT), sinh hình ảnh nhất quán nhân vật/bối cảnh (Imagen 3 / Gemini Image), ghép video động (Ken Burns, hiệu ứng chuyển cảnh, nhạc nền BGM ducking).
2. **Kiểm soát chất lượng bán tự động (Human-in-the-loop qua Telegram):** Không đăng video mù quáng; hệ thống xuất bản video, trích xuất ảnh thumbnail kiểm định chất lượng, gửi tin nhắn đến quản trị viên qua Telegram với nút bấm Inline `[Duyệt Đăng]` hoặc `[Từ Chối]`.
3. **Vận hành tự chủ và an toàn tuyệt đối:** Có hệ thống quản lý hàng đợi kiên cố (SQLite WAL), cơ chế khóa cho thuê (Lease Lock) chống xử lý trùng lặp, tự khôi phục sau sập nguồn (Crash Recovery), kiểm định tính toàn vẹn của tệp MP4 qua FFmpeg probe, và quy tắc 12 bước đánh giá an toàn trước khi kích hoạt tắt máy tính (`shutdown /s /t 60`).

---

## 2. Công Nghệ & Nền Tảng Kỹ Thuật

| Thành phần | Công nghệ / Thư viện | Phiên bản | Mục đích sử dụng |
|---|---|---|---|
| **Framework nền tảng** | .NET 10.0 Windows Desktop (WPF) | `net10.0-windows` (C# 13) | Xây dựng ứng dụng máy trạm đồ họa tốc độ cao |
| **Kiến trúc ứng dụng** | MVVM (Model-View-ViewModel) | CommunityToolkit.Mvvm 8.4.2 | Tách biệt giao diện, dữ liệu và điều phối logic qua RelayCommand, ObservableProperty |
| **Cơ sở dữ liệu cục bộ** | SQLite (WAL Mode) | `Microsoft.Data.Sqlite` 10.0.13 | Lưu trữ hàng đợi công việc tự động (`automation_jobs`), cấu hình (`app_settings`), nhật ký kiểm toán (`audit_logs`) |
| **Trí tuệ nhân tạo (Script & Phân tích)** | Google Gemini API (AI Studio & Vertex AI) | `gemini-3.5-flash-lite`, `gemini-3.5-flash`, `gemini-3.8-flash` | Lập dàn ý kịch bản, trích xuất cấu trúc phân cảnh JSON, phân tích công thức video đối thủ |
| **Sinh hình ảnh nhất quán** | Google Vertex AI Imagen 3 / Gemini Image | `gemini-3.1-flash-image`, `gemini-2.5-flash-image` | Tạo hình ảnh phân cảnh 9:16 hoặc 16:9, duy trì Style Bible và Character Profile |
| **Giọng đọc thuyết minh (TTS)** | Google Cloud Text-to-Speech V1 | `Google.Cloud.TextToSpeech.V1` 3.18.0 | Tạo audio giọng đọc tiếng Việt Neural2 (`vi-VN-Neural2-D`, `vi-VN-Neural2-A`) |
| **Nhận diện giọng nói & Căn thời gian (STT)** | Google Cloud Speech V2 (Chirp 2) | `Google.Cloud.Speech.V2` 1.8.0 | Nhận diện giọng nói tiếng Việt mức từng từ (Word-level timestamps) |
| **Xử lý Audio/Video** | FFmpeg & FFprobe (Win64) | Tích hợp trong `bin/Debug` / Root | Cắt ghép video, căn chỉnh timeline, hiệu ứng phóng thu Ken Burns, ghép BGM ducking, burn phụ đề ASS |
| **Phụ đề Karaoke** | ASS Substation Alpha v4.00+ | Tự sinh mã định dạng `{\k}` | Phụ đề hiển thị hiệu ứng đổi màu theo từng từ khớp nhịp giọng đọc |
| **Tương tác từ xa** | Telegram Bot API | HTTPS REST (Long Polling `getUpdates`) | Nhận lệnh điều khiển, gửi thumbnail và nhận phản hồi duyệt video |
| **Xuất bản YouTube** | Google YouTube Data API v3 | `Google.Apis.YouTube.v3` 1.77.0.4284 | Upload video YouTube Shorts/Long-form tự động theo lịch hẹn |
| **Xuất bản TikTok** | TikTok Content Posting API v2 | HTTPS REST (`open.tiktokapis.com/v2/post/publish/video/init/`) | Đăng video tự động lên tài khoản TikTok qua API |
| **Lưu trữ đám mây** | Google Drive API v3 | `Google.Apis.Drive.v3` 1.77.0.4276 | Tự động sao lưu video thành phẩm lên Google Drive sau khi render |
| **Kiểm thử tự động** | xUnit & Moq | xUnit 2.9.3 | Kiểm thử đơn vị các dịch vụ điều phối, phục hồi sự cố, trạng thái máy xuất bản |

---

## 3. Cấu Trúc Tổng Thể Kho Lưu Trữ

```text
C:\TOOL\VideoAutoWpf\
│
├── App.xaml / App.xaml.cs                 # Vòng đời ứng dụng, Single-Instance Mutex, Global Crash Handlers
├── MainWindow.xaml / MainWindow.xaml.cs   # Giao diện chính, điều phối chuyển hướng và Drag-and-Drop
├── VideoAutoWpf.csproj                    # Project file chính (net10.0-windows)
├── VideoAutoWpf.slnx                      # Solution format mới dạng XML (VS 2022 v17.10+)
├── Chay_App.bat / run.bat                 # Script chạy nhanh ứng dụng vượt kiểm duyệt Smart App Control
│
├── Assets/                                # Tài nguyên tĩnh: Nhạc nền BGM (.mp3), Style Previews (.jpg)
│   ├── Bgm/                               # 5 bản nhạc nền miễn phí: Chill, Dramatic, Epic, Horror, Upbeat
│   └── StylePreviews/                     # 10 ảnh mẫu phong cách nghệ thuật (Anime, 3D, Cyberpunk...)
│
├── Models/                                # Định nghĩa dữ liệu nghiệp vụ
│   ├── Automation/                        # Dữ liệu phục vụ hàng đợi: AutomationJob, AutomationConfig...
│   ├── V2/                                # Mô hình dữ liệu Pipeline V2: VideoProject, Scene, Shot, Timeline...
│   └── ...                                # StyleCard, TopicCard, SceneItem, SeriesProject, CharacterProfile...
│
├── Services/                              # Lớp logic dịch vụ nghiệp vụ (Core Services)
│   ├── GoogleAuthService.cs               # Quản lý xác thực Google Cloud (Service Account / ADC)
│   ├── GoogleTtsService.cs                # Xử lý sinh âm thanh giọng đọc tiếng Việt
│   ├── GoogleSttService.cs                # Xử lý bóc tách timestamp mức từ bằng Speech V2
│   ├── VertexImagenService.cs             # Sinh ảnh qua Vertex AI / Gemini Image với Rate Limiter
│   ├── GeminiScriptService.cs             # Tạo kịch bản kịch tính, dàn ý series bằng Gemini API
│   ├── AssSubtitleService.cs              # Sinh mã phụ đề ASS Karaoke thời gian thực
│   ├── FFmpegService.cs                   # Lệnh gọi FFmpeg dựng video hoàn chỉnh (V1 & V2)
│   ├── VisualPlanningService.cs           # Quản lý Visual Bible, hồ sơ nhân vật và bối cảnh
│   ├── ShotPlannerService.cs              # Lập kế hoạch từng góc máy (Shot) cho phân cảnh
│   ├── TimelineBuilder.cs                 # Xây dựng timeline ghép nối audio, video, subtitle
│   ├── ContinuityEngine.cs                # Đảm bảo sự nhất quán hình ảnh giữa các phân cảnh
│   ├── BgmService.cs                      # Quản lý thư viện nhạc nền và âm lượng ducking
│   ├── GoogleDriveService.cs              # Tải tệp lên Google Drive của Service Account
│   ├── YouTubeAnalyticsService.cs         # Phân tích kênh đối thủ, cào kịch bản và bóc tách công thức
│   │
│   └── Automation/                        # Hệ thống tự động hóa và điều phối an toàn
│       ├── DatabaseService.cs             # Quản lý SQLite WAL, tạo bảng, lock lease, ghi audit log
│       ├── PersistentJobScheduler.cs      # Bộ lập lịch hàng đợi, quản lý vòng lặp xử lý video
│       ├── QualityCheckService.cs         # Kiểm tra tính toàn vẹn video bằng FFmpeg probe
│       ├── TelegramApprovalService.cs     # Bot Telegram gửi ảnh duyệt và nhận callback phản hồi
│       ├── PublishingStateMachine.cs      # Máy trạng thái xuất bản video lên YouTube & TikTok
│       ├── YouTubePublishService.cs       # Dịch vụ upload video lên YouTube
│       ├── TikTokPublishService.cs        # Dịch vụ upload video lên TikTok
│       ├── RecoveryService.cs             # Tự phục hồi sau sập nguồn, kiểm tra và dọn dẹp job hỏng
│       └── ShutdownCoordinator.cs         # Đánh giá 12 điều kiện an toàn trước khi tắt máy tính
│
├── ViewModels/                            # Tầng ViewModel điều phối MVVM
│   ├── MainViewModel.cs                   # ViewModel chính chứa toàn bộ dịch vụ, hàng đợi, render
│   ├── ScriptGeneratorViewModel.cs        # Tạo kịch bản đơn lẻ từ chủ đề
│   ├── SeriesCreatorViewModel.cs          # Tạo chuỗi video nhiều tập (Kỳ án, truyện dài)
│   ├── VisualPlanningViewModel.cs         # Thiết kế diện mạo nhân vật và phong cách đồ họa
│   └── YouTubeAnalyticsViewModel.cs       # Phân tích kênh đối thủ và cào dữ liệu
│
├── Views/                                 # Giao diện XAML WPF
│   ├── MainWindow.xaml                    # Màn hình chính tạo video đơn lẻ
│   ├── ScriptGeneratorWindow.xaml         # Cửa sổ tạo kịch bản AI
│   ├── SeriesCreatorWindow.xaml           # Cửa sổ tạo series dài tập
│   ├── VisualPlanningWindow.xaml          # Cửa sổ Visual Bible
│   ├── YouTubeAnalyticsWindow.xaml        # Cửa sổ nghiên cứu đối thủ YouTube
│   ├── SocialPublishWindow.xaml           # Cửa sổ soạn tiêu đề, hashtag đăng mạng xã hội
│   └── AutomationDashboardWindow.xaml     # Bảng điều khiển hàng đợi tự động và Telegram Bot
│
├── Scripts/                               # Kịch bản triển khai môi trường
│   ├── HUONG_DAN_CAU_HINH_MINI_PC.md      # Tài liệu hướng dẫn cấu hình máy trạm Mini PC
│   └── Register-VideoAutoStartupTask.ps1  # Script PowerShell đăng ký Task Scheduler khởi động cùng Windows
│
└── Tests/                                 # Bộ kiểm thử tự động xUnit
    ├── VideoAutoWpf.Tests.csproj          # Project test
    └── ...                                # 23 unit tests cho Scheduler, Recovery, StateMachine, YouTube...
```

---

## 4. Trạng Thái Hiện Tại Của Kho Mã Nguồn

1. **Khả năng biên dịch:**
   - Solution và Project `VideoAutoWpf.csproj` biên dịch thành công 100% với 0 lỗi (0 Errors), chỉ còn 5 cảnh báo CS8602/CS0618 nhỏ không ảnh hưởng đến vận hành.
2. **Bộ kiểm thử (Unit Tests):**
   - Đầy đủ 23 kiểm thử unit test bao phủ toàn bộ các tình huống ngặt nghèo của hệ thống tự động hóa: phục hồi sau sập nguồn, xử lý callback Telegram trùng lặp, idempotency khi xuất bản, khóa hàng đợi.
3. **Tính sẵn sàng của các tính năng:**
   - Tạo video đơn lẻ (V1 & V2 Pipeline): **Hoàn thiện, hoạt động tốt**.
   - Tạo chuỗi video nhiều tập (Series Creator): **Hoàn thiện, hoạt động tốt**.
   - Phân tích đối thủ YouTube: **Hoàn thiện, hoạt động tốt**.
   - Hàng đợi tự động & Duyệt video qua Telegram: **Hoàn thiện, hoạt động tốt**.
   - Tự động tắt máy an toàn (Shutdown Coordinator): **Hoàn thiện, hoạt động tốt**.
   - Xuất bản YouTube/TikTok: **Đã triển khai kiến trúc máy trạng thái, hỗ trợ dry-run mode và API chính thức**.
4. **Lưu ý về module TFT Highlight:**
   - Kho mã nguồn **hoàn toàn không có** bất kỳ tính năng nào liên quan đến cắt highlight game Đấu Trường Chân Lý (Teamfight Tactics). Từ khóa `highlight` trong toàn bộ mã nguồn chỉ được dùng duy nhất để chỉ hiệu ứng **Karaoke Text Highlighting** (đổi màu chữ theo nhịp đọc) trong phụ đề ASS.
