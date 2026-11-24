# HƯỚNG DẪN SỬ DỤNG - VIDEOAUTOWPF MINIMAL
**Dự án:** VideoAutoWpf Minimal  
**Tài liệu:** `docs/USER_GUIDE.md`  
**Phiên bản:** Minimal 1.0 Single Workflow  

---

## 1. Tổng Quan Giao Diện Điều Khiển

Ứng dụng hoạt động trên một cửa sổ duy nhất với giao diện tối chuẩn công nghệ (Dark Tech UI), thiết kế theo 8 thẻ bước tuần tự:

- **Thanh trạng thái đầu trang (Header):** Hiển thị trạng thái FFmpeg và Google Cloud Credentials. Nút **"Làm mới dự án"** cho phép bắt đầu dự án mới bất kỳ lúc nào.
- **Thanh tiến độ & Hủy tác vụ:** Thanh Progress bar hiển thị tiến trình % của bước hiện tại, kèm nút **"Hủy tác vụ"** để dừng ngay lập tức nếu cần.
- **Banner thông báo lỗi (Error Banner):** Tự động xuất hiện khi một bước gặp lỗi (như hết Quota Gemini hoặc rớt mạng), kèm nút **"Thử lại"** trực tiếp mà không làm mất dữ liệu các bước trước.

---

## 2. Quy Trình Sản Xuất Video 8 Bước Chi Tiết

### Bước 1 – Nhập Chủ Đề & Cấu Hình
1. Nhập **Chủ đề video** (ví dụ: *"Bí ẩn sâu thẳm dưới đáy đại dương"*).
2. Tùy chọn nhập thêm **Yêu cầu nội dung**: Tông giọng, phong cách kể chuyện (ví dụ: *"Hồi hộp, kịch tính, phong cách tài liệu National Geographic"*).
3. Chọn **Tỷ lệ khung hình**: `9:16 (Shorts/TikTok/Reels)` hoặc `16:9 (YouTube ngang)`.
4. Chọn **Phong cách mỹ thuật**: *Realistic Photo, Cinematic, Anime, 3D Render, Oil Painting, Cyberpunk...*
5. Chọn **Giọng đọc tiếng Việt**: `vi-VN-Neural2-D` (nam truyền cảm), `vi-VN-Neural2-A` (nữ tự nhiên).
6. Bấm nút **"Bắt đầu: Lập dàn ý"** để chuyển sang Bước 2.

### Bước 2 – AI Lập Dàn Ý
1. AI (Gemini 2.5 Flash) sẽ tự động tạo dàn ý gồm các phần (Heading), mô tả và thời lượng ước tính.
2. Bạn có thể xem và chỉnh sửa trực tiếp nội dung từng phần của dàn ý.
3. Nếu chưa ưng ý, bấm **"Tạo lại dàn ý"**.
4. Khi đã hài lòng, bấm **"Xác nhận dàn ý & Viết kịch bản"** để chuyển sang Bước 3.

### Bước 3 – AI Viết Kịch Bản Chi Tiết
1. Dựa trên dàn ý đã xác nhận, Gemini sinh kịch bản chi tiết gồm danh sách các phân cảnh (Scenes).
2. Mỗi phân cảnh bao gồm:
   - **Lời bình đọc (Narration):** Nội dung thuyết minh tiếng Việt.
   - **Mô tả hình ảnh (Visual Prompt):** Prompt chuẩn điện ảnh để đưa vào mô hình sinh ảnh.
   - **Thời lượng dự tính (Duration):** Số giây phân cảnh.
3. Bạn có thể chỉnh sửa lời bình và visual prompt của từng cảnh.
4. Bấm **"Xác nhận kịch bản & Tạo âm thanh"** để chuyển sang Bước 4.

### Bước 4 – Tạo Giọng Đọc (TTS)
1. Hệ thống gửi toàn bộ lời bình lên Google Cloud Text-to-Speech (Neural2).
2. Xuất file âm thanh `voice.mp3` lưu vào thư mục dự án.
3. Có nút **"Nghe thử âm thanh"** để phát thử trực tiếp.
4. Bấm **"Tiếp tục: Tạo hình ảnh"** để chuyển sang Bước 5.

### Bước 5 – Tạo Hình Ảnh Cho Phân Cảnh
1. Hệ thống tự động gọi Vertex AI Imagen / Gemini Image sinh ảnh cho từng phân cảnh.
2. Tích hợp bộ đệm an toàn **Sliding Window Rate Limiter (4 RPM)** chống nghẽn quota.
3. Mỗi ảnh sinh thành công được lưu vào thư mục `Images/scene_{index}.png`.
4. Bấm **"Tiếp tục: Tạo phụ đề"** để chuyển sang Bước 6.

### Bước 6 – Tạo Phụ Đề
1. Hệ thống tính toán thời gian và sinh file phụ đề `.ass` mượt mà, đồng bộ với lời đọc.
2. File phụ đề được lưu vào `subtitles.ass` trong thư mục dự án.
3. Bấm **"Tiếp tục: Dựng video"** để chuyển sang Bước 7.

### Bước 7 – Dựng Video Hoàn Chỉnh
1. FFmpeg tự động ghép:
   - Ảnh các phân cảnh với hiệu ứng chuyển động Ken Burns mượt mà.
   - File âm thanh giọng đọc thuyết minh.
   - Nhạc nền BGM với tính năng tự động giảm âm lượng (Audio Ducking) khi có tiếng đọc.
   - Nhúng phụ đề định dạng ASS.
2. Xuất video `final.mp4` chuẩn màu yuv420p, âm thanh AAC stereo.
3. Bấm **"Xem thành phẩm"** để chuyển sang Bước 8.

### Bước 8 – Hoàn Thành & Xuất Bản Phẩm
1. Hiển thị đường dẫn đầy đủ của file video MP4.
2. Nút **"Phát thử video"**: Mở video trực tiếp trên trình phát mặc định của Windows.
3. Nút **"Mở thư mục video"**: Mở Windows Explorer đến đúng thư mục dự án chứa toàn bộ ảnh, audio, sub và MP4.
4. Nút **"Bắt đầu dự án mới"**: Làm mới giao diện để sẵn sàng làm video tiếp theo.

---

## 3. Khắc Phục Sự Cố Nhanh

- **Gặp lỗi hết Quota API hoặc rớt mạng tại một bước:**
  - Banner đỏ xuất hiện với mô tả lỗi cụ thể.
  - Các bước đã hoàn thành trước đó không bị mất dữ liệu.
  - Sau khi kiểm tra mạng hoặc quota, bấm nút **"Thử lại"** trên banner để tiếp tục ngay tại bước bị lỗi.
- **Muốn hủy tác vụ đang chạy:**
  - Bấm nút **"Hủy tác vụ"** màu đỏ cạnh thanh tiến độ. Trạng thái hiện tại được bảo toàn, bạn có thể chỉnh sửa rồi chạy lại.
