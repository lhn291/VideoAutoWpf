# TÀI LIỆU CHI TIẾT CÁC MODULE HỆ THỐNG
**Dự án:** VideoAutoWpf  
**Vai trò:** Senior Software Architect & .NET Technical Lead  
**Tài liệu:** `docs/MODULES.md`

---

## Bảng Tổng Hợp Danh Mục Module

1. **Module 1:** Giao Diện Người Dùng & Điều Phối Trung Tâm (Presentation & Core ViewModels)
2. **Module 2:** AI Lập Kịch Bản & Phân Tích Nội Dung (Gemini Script & Series Engine)
3. **Module 3:** Trí Tuệ Nhân Tạo Hình Ảnh & Giọng Nói (Vertex Imagen, Google TTS & STT)
4. **Module 4:** Lập Kế Hoạch Góc Máy, Bối Cảnh & Nhất Quán Hình Ảnh (Visual Planning & Continuity V2)
5. **Module 5:** Xử Lý Đa Phương Tiện, Phụ Đề & Nhạc Nền (FFmpeg, ASS Subtitle & BgmService)
6. **Module 6:** Nghiên Cứu Thị Trường & Phân Tích Đối Thủ (YouTube Analytics Engine)
7. **Module 7:** Hàng Đợi Kiên Cố, Bộ Lập Lịch & Khôi Phục Thảm Họa (SQLite WAL Scheduler & Crash Recovery)
8. **Module 8:** Kiểm Định Chất Lượng Video & Duyệt Từ Xa (Quality Check & Telegram Approval)
9. **Module 9:** Xuất Bản Tự Động Đa Nền Tảng (Publishing State Machine - YouTube & TikTok)
10. **Module 10:** An Toàn Hệ Thống & Tự Động Tắt Máy (Shutdown Coordinator & Windows Lifecycle)

---

### Module 1: Giao Diện Người Dùng & Điều Phối Trung Tâm (Presentation & Core ViewModels)
- **Mục đích:** Cung cấp giao diện trực quan cho người dùng cấu hình tham số video, theo dõi tiến độ thời gian thực, quản lý phân cảnh, chi phí API và điều phối mọi dịch vụ.
- **Các file liên quan:**
  - `MainWindow.xaml`, `MainWindow.xaml.cs`
  - `ViewModels/MainViewModel.cs`
  - `Views/AutomationDashboardWindow.xaml`, `Views/AutomationDashboardWindow.xaml.cs`
  - `Views/SocialPublishWindow.xaml`, `Views/SocialPublishWindow.xaml.cs`
- **Các class quan trọng:**
  - `MainViewModel`: ViewModel hạt nhân kế thừa `ObservableObject`, chứa toàn bộ các property liên kết giao diện và các command xử lý render.
  - `MainWindow`: Code-behind hỗ trợ kéo thả tệp (`DragDropEffects.Copy`) để nhập liệu kịch bản hoặc hình ảnh trực tiếp.
- **Chức năng đã triển khai:**
  - Lựa chọn tỷ lệ khung hình (9:16 dọc cho Shorts/TikTok, 16:9 ngang cho YouTube dài).
  - Chọn giọng đọc (Neural2 tiếng Việt) và tốc độ đọc (0.80x - 1.00x).
  - Chọn phong cách hiệu ứng chuyển cảnh (Fade), hiệu ứng góc máy (Ken Burns: Zoom In/Out, Pan Left/Right), Vignette.
  - Chọn kiểu phụ đề Karaoke và font chữ tiếng Việt chuẩn (Montserrat, Oswald, Be Vietnam Pro...).
  - Thư viện nhạc nền BGM tích hợp nghe thử trực tiếp (`MediaPlayer`).
  - Ước tính chi phí Google Cloud API (Imagen 3, TTS Neural2, Gemini Flash) thời gian thực theo tỷ giá USD/VND.
  - Tự động tải video lên thư mục Google Drive được chỉ định sau khi render xong.
- **Input & Output:**
  - Input: Danh sách phân cảnh (`ObservableCollection<SceneItem>`), cấu hình render.
  - Output: Lệnh kích hoạt render tạo file MP4, bảng tóm tắt nội dung đăng mạng xã hội (`VideoPublishInfo`).
- **Luồng xử lý:** Người dùng nhập hoặc sinh kịch bản -> Tùy chỉnh thông số trên UI -> Bấm "Bắt đầu tạo video" -> `MainViewModel` kiểm tra hợp lệ -> Gọi `ExecuteRenderPipelineForJobAsync` -> Cập nhật thanh tiến trình và log Console.
- **Module phụ thuộc:** Module 2, 3, 4, 5, 7, 8, 9.
- **Trạng thái thực tế:** **Hoàn thành**. Hoạt động ổn định.

---

### Module 2: AI Lập Kịch Bản & Phân Tích Nội Dung (Gemini Script & Series Engine)
- **Mục đích:** Sử dụng mô hình Google Gemini để tự động nghiên cứu chủ đề, viết kịch bản phân cảnh kịch tính, tạo series video nhiều tập theo móc nối logic (cliffhanger).
- **Các file liên quan:**
  - `Services/GeminiScriptService.cs`
  - `ViewModels/ScriptGeneratorViewModel.cs`, `Views/ScriptGeneratorWindow.xaml`, `Views/ScriptGeneratorWindow.xaml.cs`
  - `ViewModels/SeriesCreatorViewModel.cs`, `Views/SeriesCreatorWindow.xaml`, `Views/SeriesCreatorWindow.xaml.cs`
  - `Models/ScriptPlan.cs`, `Models/SeriesPlan.cs`, `Models/SeriesProject.cs`, `Models/StoryBlueprint.cs`
- **Các class quan trọng:**
  - `GeminiScriptService`: Gọi REST API tới Gemini với cơ chế fallback qua danh sách model hoạt động (`gemini-3.5-flash-lite`, `gemini-flash-lite-latest`, `gemini-3.5-flash`, `gemini-3.8-flash`), bóc tách JSON bằng regex và sửa lỗi chuỗi thoát (escaped strings).
  - `ScriptGeneratorViewModel`: Quản lý giao diện tạo kịch bản đơn lẻ từ chủ đề hoặc link bài viết.
  - `SeriesCreatorViewModel`: Quản lý luồng tạo series nhiều tập (ví dụ: "Kỳ án lịch sử", "Hồ sơ vụ án"), chia cốt truyện thành các tập nối tiếp nhau.
- **Chức năng đã triển khai:**
  - Sinh dàn ý chi tiết theo từng phân cảnh (Scene) gồm: Lời thoại đọc (Narration), Mô tả hình ảnh (Visual Prompt tiếng Anh tối ưu cho Imagen), Thời lượng ước tính.
  - Lập kế hoạch Series đa tập: Mỗi tập có tên riêng, tóm tắt, móc nối kết thúc để giữ chân khán giả.
  - Trích xuất cấu trúc dữ liệu JSON chuẩn xác, chống lỗi model sinh thêm markdown code fence (```json ... ```).
- **Input & Output:**
  - Input: Chủ đề, phong cách (Tone), tóm tắt cốt truyện hoặc kịch bản mẫu.
  - Output: Đối tượng `ScriptPlan` hoặc `SeriesProject` chứa đầy đủ phân cảnh sẵn sàng chuyển sang hàng đợi render.
- **Luồng xử lý:** Nhận prompt từ UI -> Thêm System Prompt định dạng chuẩn phân cảnh -> Gọi Gemini API -> Chuẩn hóa JSON -> Đổ dữ liệu vào `ObservableCollection<SceneItem>`.
- **Module phụ thuộc:** `GoogleAuthService`.
- **Trạng thái thực tế:** **Hoàn thành**. Đã sửa lỗi tương thích model Gemini mới.

---

### Module 3: Trí Tuệ Nhân Tạo Hình Ảnh & Giọng Nói (Vertex Imagen, Google TTS & STT)
- **Mục đích:** Sản xuất tài nguyên truyền thông nguyên bản: hình ảnh phân cảnh độ phân giải cao và giọng nói kèm mốc thời gian từng từ.
- **Các file liên quan:**
  - `Services/GoogleAuthService.cs`
  - `Services/GoogleTtsService.cs`
  - `Services/GoogleSttService.cs`
  - `Services/VertexImagenService.cs`
- **Các class quan trọng:**
  - `GoogleAuthService`: Tìm kiếm và nạp tệp ủy quyền `google_credentials.json` (Service Account), cung cấp GoogleCredential có đủ quyền hạn Cloud Platform.
  - `GoogleTtsService`: Gọi `TextToSpeechClient`, cấu hình AudioConfig (MP3/WAV, sample rate, speaking rate) và VoiceSelectionParams (`vi-VN-Neural2-D` - giọng nam trầm, `vi-VN-Neural2-A` - giọng nữ).
  - `GoogleSttService`: Gọi `SpeechClient` (Cloud Speech-to-Text V2) với mô hình `chirp_2`, bật tính năng `EnableWordTimeOffsets = true` để lấy chính xác từng mili-giây phát âm của từng từ tiếng Việt.
  - `VertexImagenService`: Gọi API sinh ảnh Vertex AI với sliding-window Rate Limiter (tối đa 4 ảnh / phút để tránh lỗi Quota 429), hỗ trợ model `gemini-3.1-flash-image`, `gemini-2.5-flash-image`.
- **Chức năng đã triển khai:**
  - Tạo tệp audio giọng đọc cho từng phân cảnh hoặc toàn bộ kịch bản.
  - Bóc tách word-level timestamps chính xác cho từng từ để phục vụ hiệu ứng Karaoke.
  - Sinh ảnh phân cảnh chất lượng cao theo đúng tỷ lệ 9:16 hoặc 16:9.
  - Cơ chế tự động đợi và thử lại (Retry with Exponential Backoff) khi gặp lỗi giới hạn tốc độ mạng/quota.
- **Input & Output:**
  - Input: Văn bản thuyết minh tiếng Việt, prompt mô tả hình ảnh tiếng Anh.
  - Output: Tệp âm thanh `.wav`/`.mp3`, danh sách `WordTiming`, tệp ảnh `.png`/`.jpg`.
- **Module phụ thuộc:** `Google.Cloud.TextToSpeech.V1`, `Google.Cloud.Speech.V2`, `Google.Apis.Auth`.
- **Trạng thái thực tế:** **Hoàn thành**.

---

### Module 4: Lập Kế Hoạch Góc Máy, Bối Cảnh & Nhất Quán Hình Ảnh (Visual Planning & Continuity V2)
- **Mục đích:** Đảm bảo chất lượng điện ảnh cho video thông qua việc duy trì nhất quán khuôn mặt nhân vật, bối cảnh không gian và chia nhỏ cảnh thành các góc máy (Shots) chuyên nghiệp.
- **Các file liên quan:**
  - `Services/VisualPlanningService.cs`, `Services/VisualDirectorService.cs`
  - `Services/ShotPlannerService.cs`, `Services/ContinuityEngine.cs`
  - `ViewModels/VisualPlanningViewModel.cs`, `Views/VisualPlanningWindow.xaml`
  - `Models/VisualStyleBible.cs`, `Models/CharacterProfile.cs`, `Models/V2/*`
- **Các class quan trọng:**
  - `VisualPlanningService`: Lưu trữ và quản lý "Kinh thánh phong cách" (Visual Style Bible), danh sách nhân vật (`CharacterProfile`) và địa điểm (`LocationProfile`).
  - `ContinuityEngine`: Phân tích kịch bản và ghép nối các đặc điểm nhận dạng bất biến (quần áo, màu tóc, vết sẹo, phong cách kiến trúc) vào prompt trước khi gửi sang AI sinh ảnh.
  - `ShotPlannerService`: Chia 1 cảnh lớn (Scene) thành các góc máy nhỏ (Wide shot, Medium shot, Close-up) để video không bị tĩnh một góc quá 4-5 giây.
- **Chức năng đã triển khai:**
  - Giao diện quản lý hồ sơ nhân vật (tên, tuổi, trang phục, diện mạo đặc trưng).
  - Tự động inject từ khóa Style (ví dụ: `cinematic lighting, 8k, dark anime style, hyper-detailed`) vào từng prompt sinh ảnh.
  - Hỗ trợ kiến trúc Pipeline V2 (`VideoProject`, `Scene`, `Shot`, `Timeline`).
- **Trạng thái thực tế:** **Hoàn thành**.

---

### Module 5: Xử Lý Đa Phương Tiện, Phụ Đề & Nhạc Nền (FFmpeg, ASS Subtitle & BgmService)
- **Mục đích:** Trực tiếp biên tập, kết xuất video hoàn chỉnh với hiệu ứng chuyển động, nhạc nền và chữ chạy karaoke.
- **Các file liên quan:**
  - `Services/FFmpegService.cs`, `Services/IFFmpegService.cs`
  - `Services/AssSubtitleService.cs`
  - `Services/BgmService.cs`, `Services/TimelineBuilder.cs`
  - `Models/SubtitleStyleOption.cs`, `Models/BgmTrack.cs`
- **Các class quan trọng:**
  - `FFmpegService`: Tạo tiến trình gọi `ffmpeg.exe` với các filter graph phức tạp (`scale`, `crop`, `zoompan`, `amix`, `volume`, `subtitles`).
  - `AssSubtitleService`: Sinh định dạng file `.ass` với cú pháp Karaoke `{\k<duration_cs>}` khớp theo word timestamps của Google STT.
  - `BgmService`: Tự động quét các file nhạc trong thư mục `Assets/Bgm/`, tính toán trường độ và chuẩn hóa âm lượng theo chuẩn phát sóng (-14 LUFS).
- **Chức năng đã triển khai:**
  - Tạo video dọc Shorts 1080x1920 hoặc ngang 1920x1080.
  - Hiệu ứng chuyển cảnh mượt mà giữa các bức ảnh (Fade Transition).
  - Hiệu ứng camera Ken Burns sống động (tự động zoom từ từ vào trọng tâm bức ảnh).
  - Nhạc nền tự động giảm âm lượng (Audio Ducking) khi có lời dẫn chuyện của MC và tự tăng âm lượng nhẹ ở đoạn dạo đầu/kết thúc.
  - Burn phụ đề Karaoke chữ chạy vàng/trắng nổi bật viền đen vào video, không bị lỗi font tiếng Việt.
- **Trạng thái thực tế:** **Hoàn thành**.

---

### Module 6: Nghiên Cứu Thị Trường & Phân Tích Đối Thủ (YouTube Analytics Engine)
- **Mục đích:** Phân tích kênh YouTube của đối thủ, bóc tách kịch bản của các video triệu view, nhận diện công thức giữ chân người xem (Retention Formula) để tái sử dụng.
- **Các file liên quan:**
  - `Services/YouTubeAnalyticsService.cs`, `Services/CompetitorPatternAnalyzer.cs`
  - `ViewModels/YouTubeAnalyticsViewModel.cs`, `Views/YouTubeAnalyticsWindow.xaml`
  - `Models/YouTubeAnalyticsData.cs`, `Models/ContentAnalysis.cs`
- **Các class quan trọng:**
  - `YouTubeAnalyticsService`: Tương tác với YouTube Data API v3 để lấy danh sách video, lượt xem, thời lượng, tag, description và cào transcript phụ đề tự động.
  - `CompetitorPatternAnalyzer`: Dùng Gemini AI để phân tích tại sao video đó thành công: Cấu trúc 3 giây đầu (Hook), cách dẫn dắt cao trào (Pacing), lời kêu gọi hành động (CTA).
- **Chức năng đã triển khai:**
  - Phân tích kênh theo Channel ID hoặc URL.
  - Bóc tách toàn bộ kịch bản lời thoại của video đối thủ.
  - Tạo kịch bản mới dựa trên "bộ khung thành công" của video đối thủ.
- **Trạng thái thực tế:** **Hoàn thành**.

---

### Module 7: Hàng Đợi Kiên Cố, Bộ Lập Lịch & Khôi Phục Thảm Họa (SQLite WAL Scheduler & Crash Recovery)
- **Mục đích:** Cung cấp hàng đợi công việc bất biến, chống mất dữ liệu khi mất điện hoặc máy tính bị tắt đột ngột, điều phối nhịp nhàng các job tự động.
- **Các file liên quan:**
  - `Services/Automation/DatabaseService.cs`
  - `Services/Automation/PersistentJobScheduler.cs`
  - `Services/Automation/RecoveryService.cs`
  - `Models/Automation/AutomationJob.cs`, `Models/Automation/AutomationConfig.cs`
- **Các class quan trọng:**
  - `DatabaseService`: Kết nối `Microsoft.Data.Sqlite`, cấu hình `PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;`. Quản lý bảng `automation_jobs`, `app_settings`, `audit_logs`.
  - `PersistentJobScheduler`: Vòng lặp Worker nền định kỳ 10 giây quét tìm job `Pending`, thực hiện Lease Locking với thời hạn 5 phút (Heartbeat 30 giây) để không job nào bị 2 tiến trình xử lý cùng lúc.
  - `RecoveryService`: Quét dọn hệ thống khi khởi động ứng dụng; nếu phát hiện job đang dở dang ở phiên trước, gọi `QualityCheckService` để xác định video đã xong hay hỏng dở, xóa tệp rác và reset về trạng thái an toàn.
- **Chức năng đã triển khai:**
  - Khóa hàng đợi an toàn (Atomic Lease Lock).
  - Tự động thử lại khi gặp lỗi tạm thời (Max retry = 3).
  - Phục hồi 100% dữ liệu hàng đợi sau sự cố sập nguồn.
- **Trạng thái thực tế:** **Hoàn thành**. Đã kiểm thử qua bộ xUnit unit tests.

---

### Module 8: Kiểm Định Chất Lượng Video & Duyệt Từ Xa (Quality Check & Telegram Approval)
- **Mục đích:** Ngăn chặn việc đăng video lỗi lên mạng xã hội; gửi ảnh thumbnail và thông tin video về điện thoại của chủ kênh qua Telegram Bot để bấm nút duyệt.
- **Các file liên quan:**
  - `Services/Automation/QualityCheckService.cs`
  - `Services/Automation/TelegramApprovalService.cs`
- **Các class quan trọng:**
  - `QualityCheckService`: Kiểm tra kích thước file tối thiểu (> 50KB) và chạy lệnh `ffmpeg -v error -i <video> -f null -` để phát hiện frame bị hỏng; trích xuất ảnh thumbnail tại giây thứ 1.
  - `TelegramApprovalService`: Chạy luồng Long Polling gọi API `getUpdates` của Telegram, gửi ảnh kèm Inline Keyboard `[DUYỆT ĐĂNG] (appr:{id})` và `[TỪ CHỐI] (rejt:{id})`, kiểm tra `AllowedUserIds` để chống kẻ lạ ra lệnh.
- **Chức năng đã triển khai:**
  - Gửi thông báo kèm ảnh đại diện video lên kênh chat riêng.
  - Bộ nhớ đệm chống trùng lặp callback (Deduplication Cache) khi mạng chập chờn.
  - Thông báo hủy lệnh tắt máy khẩn cấp trực tiếp qua tin nhắn Telegram.
- **Trạng thái thực tế:** **Hoàn thành**.

---

### Module 9: Xuất Bản Tự Động Đa Nền Tảng (Publishing State Machine - YouTube & TikTok)
- **Mục đích:** Đăng video đã được duyệt lên YouTube Shorts và TikTok an toàn, có hỗ trợ hẹn giờ và ghi nhận trạng thái độc lập.
- **Các file liên quan:**
  - `Services/Automation/PublishingStateMachine.cs`
  - `Services/Automation/YouTubePublishService.cs`
  - `Services/Automation/TikTokPublishService.cs`
- **Các class quan trọng:**
  - `PublishingStateMachine`: Quản lý luồng xuất bản đa nền tảng theo mô hình idempotent (nếu YouTube đã đăng mà TikTok lỗi thì khi chạy lại chỉ đăng tiếp TikTok, không đăng lặp lại YouTube).
  - `YouTubePublishService`: Sử dụng `Google.Apis.YouTube.v3` với luồng Resumable Upload, cấu hình tiêu đề, thẻ tags, danh mục, quyền riêng tư (Public/Private/Unlisted).
  - `TikTokPublishService`: Triển khai TikTok Content Posting API v2 (khởi tạo, upload binary stream và polling kết quả). Có chế độ `DryRun` để kiểm thử không tốn lượt đăng thật.
- **Chức năng đã triển khai:**
  - Upload resumable có khả năng tiếp tục khi gián đoạn mạng.
  - Kiểm tra trạng thái xuất bản trước khi gửi yêu cầu mới.
  - Đầy đủ chế độ mô phỏng kiểm thử (`DryRunMode = true`).
- **Trạng thái thực tế:** **Hoàn thành về mặt kiến trúc và tích hợp**. Cần cấu hình OAuth Token người dùng thực tế của kênh khi đưa vào khai thác thực tế.

---

### Module 10: An Toàn Hệ Thống & Tự Động Tắt Máy (Shutdown Coordinator & Windows Lifecycle)
- **Mục đích:** Giúp máy trạm Mini PC tự tắt máy sau khi hoàn thành toàn bộ khối lượng công việc ban đêm mà không gây mất dữ liệu hay hỏng hệ điều hành.
- **Các file liên quan:**
  - `Services/Automation/ShutdownCoordinator.cs`
  - `Scripts/Register-VideoAutoStartupTask.ps1`
  - `Scripts/HUONG_DAN_CAU_HINH_MINI_PC.md`
- **Các class quan trọng:**
  - `ShutdownCoordinator`: Chứa hàm `EvaluateSafetyRulesAsync()` thẩm định 12 quy tắc an toàn (hàng đợi rỗng, không có render dở, không còn upload dở, không có lệnh giữ máy).
- **Chức năng đã triển khai:**
  - Đếm ngược 60 giây trước khi gọi `shutdown.exe /s /t 0`.
  - Gửi cảnh báo khẩn cấp lên Telegram kèm nút `[HỦY TẮT MÁY]` cho phép admin hủy lệnh từ xa.
  - Script cấu hình Windows Task Scheduler để máy tự khởi động ứng dụng mỗi sáng khi bật nguồn.
- **Trạng thái thực tế:** **Hoàn thành**.
