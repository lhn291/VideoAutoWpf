# BÁO CÁO TÌNH TRẠNG DỰ ÁN CHI TIẾT - VIDEOAUTOWPF MINIMAL
**Dự án:** VideoAutoWpf Minimal  
**Branch:** `feature/minimal-video-workflow`  
**Ngày kiểm định & hoàn thành:** 24/11/2026  
**Trạng thái tổng thể:** SẴN SÀNG CHẠY THỰC TẾ (STABLE MINIMAL SINGLE WORKFLOW)

---

## 1. Bảng Ma Trận Tình Trạng Tính Năng (Minimal Architecture Matrix)

| Hạng mục chức năng | Trạng thái kỹ thuật | Mức độ ưu tiên | Ghi chú từ mã nguồn thực tế |
|---|---|---|---|
| **Biên dịch Solution / Project** | Đang hoạt động | P0 - Cực cao | `dotnet build` đạt **0 lỗi (0 Errors), 0 cảnh báo (0 Warnings)** |
| **Kiểm thử tự động (Unit Tests)** | Đang hoạt động | P0 - Cực cao | `Tests/MinimalWorkflowTests.cs`: 5 test bao phủ lưu trạng thái, phục hồi sự cố, hủy tác vụ và quét bảo mật zero power commands |
| **Vòng đời ứng dụng & Mutex** | Đang hoạt động | P0 - Cực cao | Single Instance Mutex + Global unhandled exception handlers trong `App.xaml.cs`. `ShutdownMode.OnMainWindowClose` ngăn ngừa lỗi tắt app đột ngột |
| **Giao diện chính (MainWindow)** | Đang hoạt động | P0 - Cực cao | Single Window Dark Tech UI, hiển thị 8 bước rõ ràng, có thanh tiến độ, banner lỗi và nút hủy tác vụ |
| **Bước 1: Nhập chủ đề & Cấu hình** | Đang hoạt động | P0 - Cực cao | Tùy chọn tỷ lệ khung hình (9:16 Shorts / 16:9 Ngang), Tone giọng, Phong cách mỹ thuật, Nhạc nền BGM |
| **Bước 2: AI lập dàn ý (Gemini)** | Đang hoạt động | P0 - Cực cao | `GeminiScriptService.GenerateMinimalOutlineAsync`, cho phép xem, sửa và tạo lại dàn ý |
| **Bước 3: AI viết kịch bản (Gemini)** | Đang hoạt động | P0 - Cực cao | `GeminiScriptService.GenerateMinimalScriptAsync`, sinh phân cảnh chi tiết (lời bình, visual prompt, thời lượng) |
| **Bước 4: Tạo giọng đọc (Google TTS)** | Đang hoạt động | P0 - Cực cao | `GoogleTtsService.SynthesizeSpeechAsync`, hỗ trợ giọng đọc Neural2 tiếng Việt, nghe thử âm thanh |
| **Bước 5: Tạo hình ảnh (Imagen/Gemini)**| Đang hoạt động | P0 - Cực cao | `VertexImagenService.GenerateImageAsync`, có Sliding Window Rate Limiter 4 RPM, lưu vào thư mục `Images/` |
| **Bước 6: Tạo phụ đề (ASS Engine)** | Đang hoạt động | P0 - Cực cao | `AssSubtitleService.GenerateMinimalAssSubtitleAsync`, đồng bộ chữ với âm thanh |
| **Bước 7: Dựng video (FFmpeg)** | Đang hoạt động | P0 - Cực cao | `FFmpegService.GenerateVideoAsync`, hiệu ứng chuyển động Ken Burns, hòa âm Voice + BGM ducking, xuất MP4 |
| **Bước 8: Hoàn thành & Xuất bản phẩm** | Đang hoạt động | P0 - Cực cao | Hiển thị đường dẫn video MP4, mở video phát thử, mở thư mục dự án |
| **Lưu trạng thái từng bước (Persistence)**| Đang hoạt động | P0 - Cực cao | `VideoProjectData.Save` ghi `project.json` sau mỗi bước; retry bước lỗi mà không mất dữ liệu đã làm |
| **Lệnh tắt máy / Restart hệ điều hành** | **ĐÃ LOẠI BỎ 100%** | P0 - Cực cao | Đã quét toàn bộ mã nguồn: **0 lệnh `shutdown.exe`, 0 lệnh `Stop-Computer`, 0 lệnh `Restart-Computer`** |
| **Tự động hóa ngầm / Bot mạng xã hội** | **ĐÃ LOẠI BỎ 100%** | P0 - Cực cao | Đã loại bỏ hoàn toàn: SQLite job queue, Telegram Bot, YouTube/TikTok auto publisher, Google Drive auto upload, Startup Task |

---

## 2. Danh Sách Các Thành Phần Đã Loại Bỏ Hoàn Toàn (Deprecated Modules Removed)

Để đạt được triết lý "Đơn giản - Ổn định - Dễ bảo trì", các module phức tạp, chạy ngầm hoặc gây sập nguồn đã được gỡ bỏ triệt để:

1. **Nhóm Lập lịch ngầm & Tắt máy:**
   - `Services/Automation/ShutdownCoordinator.cs` (Nguyên nhân chính gây tắt máy tính).
   - `Services/Automation/PersistentJobScheduler.cs` (Vòng lặp chạy nền 10 giây).
   - `Services/Automation/RecoveryService.cs`
   - `Services/Automation/DatabaseService.cs` (SQLite database).
   - `Scripts/Register-VideoAutoStartupTask.ps1` (Task Scheduler Windows).
2. **Nhóm Duyệt bài & Mạng xã hội:**
   - `Services/Automation/TelegramApprovalService.cs`
   - `Services/Automation/YouTubePublishService.cs`
   - `Services/Automation/TikTokPublishService.cs`
   - `Services/Automation/QualityCheckService.cs`
   - `Services/Automation/PublishingStateMachine.cs`
   - `Services/GoogleDriveService.cs`
   - `Services/YouTubeAnalyticsService.cs`
   - `Services/CompetitorPatternAnalyzer.cs`
3. **Nhóm V2 Đa màn hình & Directed Graph phức tạp:**
   - `Services/VisualPlanningService.cs`, `VisualDirectorService.cs`, `ShotPlannerService.cs`, `ContinuityEngine.cs`, `TimelineBuilder.cs`, `GenerationQueue.cs`
   - `Components/` (Toàn bộ UserControls cũ gây xung đột UI Designer)
   - `Views/` (`ScriptGeneratorWindow`, `SeriesCreatorWindow`, `YouTubeAnalyticsWindow`, `VisualPlanningWindow`, `SocialPublishWindow`, `AutomationDashboardWindow`)
   - `Models/Automation/`, `Models/V2/`

---

## 3. Rà Soát Bảo Mật & An Toàn Mã Nguồn

- **Lệnh tắt máy (Power Management):** Xác nhận 100% sạch sẽ qua lệnh tĩnh và unit test `Test_05_ZeroPowerCommands_SourceCodeVerification`.
- **Xử lý biệt lệ WPF:** `App.xaml.cs` duy trì 3 tầng bắt ngoại lệ toàn cục (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`), ghi ra `crash_log.txt` và giữ ứng dụng không bị đóng đột ngột.
- **WPF ShutdownMode:** Đặt cố định `ShutdownMode.OnMainWindowClose`, chấm dứt hoàn toàn hiện tượng app bị tắt ngầm khi đóng cửa sổ phụ.
