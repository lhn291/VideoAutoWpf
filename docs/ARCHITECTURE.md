# KIẾN TRÚC HỆ THỐNG - VIDEOAUTOWPF MINIMAL
**Dự án:** VideoAutoWpf Minimal  
**Kiến trúc:** Single-Workflow Desktop Video Production Engine  
**Phiên bản:** Minimal 1.0 (.NET 10 WPF)  
**Tài liệu:** `docs/ARCHITECTURE.md`

---

## 1. Triết Lý Thiết Kế (Core Philosophy)

1. **Một Luồng Sản Xuất Duy Nhất (Single Sequential Workflow):** Toàn bộ quy trình sản xuất video gói gọn trong 8 bước tuần tự, không phân nhánh, không popup chồng chéo.
2. **Giao Diện Đơn Màn Hình (Single-Window Dark Tech UI):** Mọi thao tác nằm trong cửa sổ chính `MainWindow.xaml`. Loại bỏ hoàn toàn lỗi đóng app đột ngột do WPF `ShutdownMode.OnLastWindowClose`.
3. **Bảo Toàn Trạng Thái Từng Bước (Per-Step State Persistence):** Sau mỗi bước, trạng thái được tự động lưu vào `Output/{ProjectName}/project.json`. Khi một bước gặp sự cố (quota API, mất mạng), người dùng có thể thử lại ngay mà không phải tạo lại dữ liệu các bước trước.
4. **An Toàn Tuyệt Đối Về Nguồn Điện (Zero Power Commands):** Tuyệt đối loại bỏ `shutdown.exe`, `Stop-Computer`, `Restart-Computer`, Windows Task Scheduler tự động tắt máy. Ứng dụng chỉ hoạt động khi người dùng chủ động yêu cầu.
5. **Không Tự Động Hóa Ngầm (No Background Automation):** Không SQLite job scheduler, không bot Telegram duyệt bài, không tự động đăng lên mạng xã hội.

---

## 2. Sơ Đồ Kiến Trúc Tổng Thể (System Architecture)

```mermaid
graph TD
    subgraph UI_Layer ["Tầng Giao Diện (Presentation - WPF MVVM)"]
        MW["MainWindow (Single Window UI - Dark Tech Theme)"]
        MVM["MainViewModel (Điều phối luồng 8 bước)"]
    end

    subgraph Core_Pipeline ["Quy Trình 8 Bước (Minimal Pipeline)"]
        S1["Bước 1: Nhập chủ đề & Cấu hình"]
        S2["Bước 2: AI lập dàn ý (Gemini 2.5 Flash)"]
        S3["Bước 3: AI viết kịch bản (Gemini 2.5 Flash)"]
        S4["Bước 4: Tạo giọng đọc (Google Cloud TTS Neural2)"]
        S5["Bước 5: Tạo hình ảnh (Vertex AI Imagen / Gemini Image)"]
        S6["Bước 6: Tạo phụ đề (ASS Subtitle Service)"]
        S7["Bước 7: Dựng video hoàn chỉnh (FFmpeg Engine)"]
        S8["Bước 8: Hoàn thành & Xuất bản phẩm"]
    end

    subgraph Service_Layer ["Tầng Dịch Vụ Cốt Lõi (Core Services)"]
        GS["GeminiScriptService (Lập dàn ý & Kịch bản)"]
        TTS["GoogleTtsService (Lồng tiếng Neural2)"]
        STT["GoogleSttService (Đồng bộ timestamp giọng đọc)"]
        IMG["VertexImagenService (Tạo ảnh & Sliding Window Limiter)"]
        ASS["AssSubtitleService (Sinh phụ đề ASS mượt mà)"]
        FF["FFmpegService (Dựng video, Ken Burns, BGM Ducking)"]
        BGM["BgmService (Quản lý thư viện nhạc nền)"]
    end

    subgraph Storage_Layer ["Tầng Lưu Trữ & Phục Hồi (Storage & Persistence)"]
        PDIR["Thư mục dự án: Output/{ProjectName}/"]
        PJSON[("project.json (Lưu trạng thái từng bước)")]
        MEDIA["Files: voice.mp3, images/*.png, subtitles.ass, final.mp4"]
    end

    MW --> MVM
    MVM --> S1
    S1 --> S2 --> S3 --> S4 --> S5 --> S6 --> S7 --> S8

    S2 --> GS
    S3 --> GS
    S4 --> TTS
    S5 --> IMG
    S6 --> ASS
    S6 -.-> STT
    S7 --> FF
    S7 --> BGM

    MVM --> PDIR
    PDIR --> PJSON
    PDIR --> MEDIA
```

---

## 3. Luồng Hoạt Động Chi Tiết 8 Bước (Pipeline Flow)

```mermaid
sequenceDiagram
    autonumber
    actor User as Người dùng
    participant UI as MainWindow / MainViewModel
    participant AI as GeminiScriptService
    participant TTS as GoogleTtsService
    participant IMG as VertexImagenService
    participant SUB as AssSubtitleService
    participant FF as FFmpegService
    participant FS as File System (Output/Project)

    Note over User, FS: BƯỚC 1: NHẬP CHỦ ĐỀ & CẤU HÌNH
    User->>UI: Nhập chủ đề, chọn tỷ lệ khung hình (9:16/16:9), tone giọng, phong cách
    UI->>FS: Khởi tạo thư mục dự án & lưu project.json

    Note over User, FS: BƯỚC 2: AI LẬP DÀN Ý
    User->>UI: Bấm "Lập dàn ý AI"
    UI->>AI: GenerateMinimalOutlineAsync(topic, style, tone, ratio)
    AI-->>UI: VideoOutline (tiêu đề, tóm tắt, danh sách section)
    UI->>FS: Lưu project.json (Outline State)
    User->>UI: Xem, chỉnh sửa tiêu đề/nội dung dàn ý, bấm "Xác nhận dàn ý"

    Note over User, FS: BƯỚC 3: AI VIẾT KỊCH BẢN
    User->>UI: Bấm "Viết kịch bản chi tiết"
    UI->>AI: GenerateMinimalScriptAsync(outline, style, ratio)
    AI-->>UI: VideoScript (danh sách VideoSceneItem: lời bình, visual prompt)
    UI->>FS: Lưu project.json (Script State)
    User->>UI: Tùy chỉnh lời bình hoặc prompt từng phân cảnh

    Note over User, FS: BƯỚC 4: TẠO GIỌNG ĐỌC
    User->>UI: Bấm "Tạo giọng đọc TTS"
    UI->>TTS: SynthesizeSpeechAsync(fullNarration, voiceName)
    TTS-->>UI: voice.mp3
    UI->>FS: Lưu voice.mp3 & cập nhật project.json
    User->>UI: Nghe thử file âm thanh

    Note over User, FS: BƯỚC 5: TẠO HÌNH ẢNH
    User->>UI: Bấm "Tạo hình ảnh cho phân cảnh"
    loop Từng phân cảnh (Scene)
        UI->>IMG: GenerateImageAsync(visualPrompt, aspectRatio)
        IMG-->>UI: Lưu ảnh scene_{i}.png
    end
    UI->>FS: Cập nhật project.json (Images State)

    Note over User, FS: BƯỚC 6: TẠO PHỤ ĐỀ
    User->>UI: Bấm "Tạo phụ đề ASS"
    UI->>SUB: GenerateMinimalAssSubtitleAsync(scenes, voicePath)
    SUB-->>UI: subtitles.ass
    UI->>FS: Lưu subtitles.ass & cập nhật project.json

    Note over User, FS: BƯỚC 7: DỰNG VIDEO
    User->>UI: Bấm "Dựng video MP4"
    UI->>FF: GenerateVideoAsync(scenes, config, progress)
    FF-->>UI: final.mp4 hoàn thiện
    UI->>FS: Cập nhật project.json (Completed State)

    Note over User, FS: BƯỚC 8: HOÀN THÀNH
    UI->>User: Hiển thị đường dẫn MP4, nút xem video, nút mở thư mục
```

---

## 4. Cơ Chế Lưu Trữ & Phục Hồi Sự Cố (Failure Recovery & State Persistence)

Mỗi dự án video được đóng gói trong một thư mục độc lập:
```
Output/
  └── {Ten_Du_An}_{yyyyMMdd_HHmmss}/
      ├── project.json            # Trạng thái tổng thể & metadata các bước
      ├── voice.mp3               # File âm thanh giọng đọc TTS
      ├── subtitles.ass           # Phụ đề đã căn chỉnh thời lượng
      ├── final.mp4               # Video xuất bản phẩm hoàn thiện
      ├── Images/                 # Chứa các ảnh minh họa đã sinh
      │   ├── scene_001.png
      │   └── scene_002.png
      └── logs/                   # Ghi log tiến trình thực thi
```

Khi một bước gặp lỗi (ví dụ: Google TTS hết quota hoặc mất mạng):
1. Bước hiện tại đánh dấu `Failed` và hiển thị thông báo lỗi chi tiết trên banner.
2. Dữ liệu các bước trước (`Outline`, `Script`, `Images`) được bảo toàn 100% trong `project.json`.
3. Người dùng khắc phục sự cố (kiểm tra mạng, đổi giọng) và bấm thử lại ngay tại bước đó mà không cần làm lại từ đầu.
