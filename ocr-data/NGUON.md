# Mô hình OCR
vie.traineddata được giữ lại từ thư mục tessdata trong bộ source fuzzy-guide của nhóm đã cung cấp, thay vì bỏ mất khi loại bin/obj khỏi ZIP.
Csproj API và WinForms sao chép file này vào tessdata cạnh assembly khi build. Engine dùng package Tesseract 5.2.0.
Nguồn mô hình Tesseract tiếng Việt: https://github.com/tesseract-ocr/tessdata
OCR chỉ trả gợi ý số; phải đối chiếu và xác nhận. Không có cam kết độ chính xác trước khi thử trên ảnh đồng hồ thực.
