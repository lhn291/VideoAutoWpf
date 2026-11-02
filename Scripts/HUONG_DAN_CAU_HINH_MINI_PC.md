# HƯỚNG DẪN CẤU HÌNH VẬN HÀNH TỰ ĐỘNG CHO MINI PC (RYZEN 5 5600G)

Tài liệu này hướng dẫn cách thiết lập mini PC chạy Windows tự vận hành 100%: **Có điện tự bật → Tự đăng nhập → VideoAutoWpf tự tạo video → Gửi Telegram duyệt qua iPhone → Tự động đăng TikTok / YouTube → Tự động tắt máy an toàn khi hết việc**.

---

## 1. Cấu hình BIOS/UEFI: Tự Động Bật Máy Khi Có Điện (Restore on AC Power Loss)

Khi cúp điện rồi có điện trở lại, máy tính thông thường sẽ ở trạng thái TẮT (Standby). Task Scheduler **không thể** tự khởi động máy nếu phần cứng chưa bật nguồn.

### Các bước thực hiện:
1. Cắm bàn phím, màn hình và bật nguồn mini PC.
2. Nhấn liên tục phím `Delete` hoặc `F2` (tùy dòng bo mạch chủ / mini PC Minisforum, Beelink, Asus, v.v.) để vào **BIOS/UEFI Setup**.
3. Tìm đến một trong các mục sau (tùy hãng):
   - **Advanced** → **APM Configuration** (hoặc **Power Management Setup**).
   - **Chipset** → **South Bridge** (hoặc **PCH**).
4. Tìm tùy chọn có tên:
   - `Restore AC Power Loss` hoặc `State After G3` hoặc `Power On After Power Failure`.
5. Đổi giá trị từ `Power Off` thành **`Power On`** (hoặc **`Always On`**).
6. Nhấn `F10` để **Save & Exit**.

> **Kết quả:** Từ thời điểm này, bất cứ khi nào mini PC được cấp nguồn điện (hoặc sau khi có điện lại), máy sẽ tự động bật mà không cần bấm nút nguồn vật lý.

---

## 2. Cấu hình Windows Auto-Logon (Đăng nhập tự động an toàn)

### Vì sao chọn "At log on" thay vì "At startup"?
- **VideoAutoWpf** là ứng dụng đồ họa **WPF** (.NET 10.0), sử dụng phần cứng đồ họa (DirectX, GPU Radeon Vega trên Ryzen 5 5600G) và FFmpeg để dựng video, phụ đề karaoke ASS và hiển thị giao diện.
- Nếu chạy dạng Windows Service hoặc `At system startup` mà không có User Session (Session 0), ứng dụng WPF không thể khởi tạo cửa sổ đồ họa và có thể bị lỗi DirectWrite/Direct3D.
- Do đó, phương án tối ưu và an toàn nhất là: **Windows khởi động → Tự động đăng nhập vào Desktop tương tác → Scheduled Task chạy VideoAutoWpf.exe**.

### Cách thiết lập Auto-Logon an toàn (Không lưu plain text):
1. **Khuyến nghị:** Sử dụng công cụ chính thức của Microsoft Sysinternals: **[Autologon for Windows](https://learn.microsoft.com/en-us/sysinternals/downloads/autologon)**.
2. Tải `Autologon.exe` về máy, chạy với quyền Administrator.
3. Nhập tên người dùng và mật khẩu tài khoản Windows hiện tại, bấm **Enable**.
   - *Lưu ý bảo mật:* Autologon của Microsoft lưu mật khẩu dạng mã hóa trong Windows LSA Secrets (Local Security Authority), **không lưu dạng text thông thường**.
4. Khởi động lại máy để kiểm tra: Máy sẽ vào thẳng màn hình Desktop mà không cần gõ mật khẩu.

---

## 3. Đăng ký Task Scheduler chạy VideoAutoWpf

Sau khi đã build file `VideoAutoWpf.exe`, mở **PowerShell với quyền Administrator** tại thư mục dự án và chạy:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\Scripts\Register-VideoAutoStartupTask.ps1 -DelaySeconds 30
```

### Tham số tùy biến:
- `-DelaySeconds 30`: Chờ 30 giây sau khi đăng nhập mới mở app (giúp mạng Wi-Fi/Ethernet, driver card âm thanh, GPU khởi động ổn định trước).
- `-TaskName "VideoAutoWpf_AutoRun"`: Tên tác vụ trong Windows Task Scheduler.

---

## 4. Cấu hình Telegram Bot & Phê duyệt từ xa trên iPhone

### Bước 1: Tạo Telegram Bot
1. Mở Telegram trên iPhone, tìm bot `@BotFather`.
2. Gõ `/newbot`, đặt tên cho bot và lấy mã **Bot Token** (dạng `7123456789:ABCdefGhI...`).
3. Mở bot của bạn vừa tạo trên iPhone và bấm `/start`.

### Bước 2: Lấy User ID của bạn
1. Tìm bot `@userinfobot` trên Telegram.
2. Bấm `/start` để xem `Id` của bạn (ví dụ `123456789`).

### Bước 3: Nạp cấu hình vào VideoAutoWpf
Bạn có thể cấu hình bằng 1 trong 2 cách:
- **Cách 1 (Biến môi trường Windows - Khuyến nghị):**
  - Mở PowerShell:
    ```powershell
    [System.Environment]::SetEnvironmentVariable('TELEGRAM_BOT_TOKEN', 'MÃ_TOKEN_CỦA_BẠN', 'User')
    ```
- **Cách 2 (Giao diện ứng dụng):**
  - Mở VideoAutoWpf → Vào mục **Cấu Hình Tự Động Vận Hành**.
  - Dán Token, Chat ID và User ID của bạn vào ô tương ứng rồi bấm **Lưu Cấu Hình**.

---

## 5. Quy trình Vận hành Hoàn chỉnh Thực tế

1. **Chuẩn bị kịch bản / Job:**
   - Bạn tạo sẵn các video hoặc chuỗi kịch bản Series trong ứng dụng, hoặc nạp kịch bản vào hàng đợi tự động.
2. **Tự động chạy:**
   - Khi có điện hoặc mở máy, VideoAutoWpf tự bật, quét các job `Pending` trong SQLite và bắt đầu tạo video (TTS tiếng Việt → Gemini Imagen 3 → FFmpeg Timeline V2).
3. **Duyệt qua iPhone:**
   - Video render xong, app tự kiểm tra chất lượng file MP4 và gửi tin nhắn Telegram gồm: *Ảnh thumbnail, Tiêu đề, Thời lượng, Caption*.
   - Kèm 3 nút bấm: `[✅ DUYỆT ĐĂNG]`, `[❌ TỪ CHỐI]`, `[ℹ️ XEM CHI TIẾT]`.
   - Bạn chỉ cần mở Telegram trên iPhone và bấm **✅ DUYỆT ĐĂNG**.
4. **Tự động đăng:**
   - Ngay khi nhận được callback duyệt hợp lệ, app đưa video vào Publishing State Machine và đăng lên TikTok / YouTube.
5. **Tự động tắt máy an toàn:**
   - Khi không còn video nào chờ duyệt, không còn job render, không còn upload dở dang:
   - Hệ thống đếm ngược 60 giây (báo tin nhắn Telegram mini PC sắp tắt).
   - Nếu trong 60 giây không có việc mới hoặc không bị hủy → Gọi lệnh tắt máy Windows bình thường.
   - Mini PC tắt hoàn toàn, tiết kiệm điện và sẵn sàng cho chu kỳ tiếp theo!
