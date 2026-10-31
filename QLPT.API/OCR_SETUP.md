# Đọc chỉ số điện từ ảnh trong WPF

WPF thử OCR trên máy trước. Nếu kết quả chưa đủ chắc chắn, WPF gửi **vùng số đã khoanh và ảnh đồng hồ thu nhỏ** tới API. API ưu tiên Gemini khi có `GEMINI_API_KEY`, nếu không thì dùng OpenAI khi có `OPENAI_API_KEY`. Kết quả chỉ là gợi ý; người dùng phải đối chiếu ảnh rồi xác nhận số trước khi lưu. Ảnh đầy đủ chỉ được lưu qua endpoint `/api/Anh` khi bấm **Lưu**. Bước đọc AI không ghi vào CSDL.

## Cấu hình

Máy chạy `QLPT.API` cần khóa API của Gemini hoặc OpenAI. Với Gemini, đặt biến môi trường `GEMINI_API_KEY` cho tiến trình API, chẳng hạn trong cửa sổ PowerShell dùng để chạy API:

```powershell
$env:GEMINI_API_KEY = '<Gemini API key của bạn>'
dotnet run --project QLPT.API/QLPT.API.csproj --launch-profile http
```

Không đặt key trong WPF, `appsettings.json`, Git hoặc ảnh chụp màn hình. Nếu đặt key bằng biến môi trường người dùng Windows, phải khởi động lại tiến trình API để nhận giá trị mới. Không gửi key vào chat.

Gemini mặc định dùng `gemini-3.6-flash` (có thể đổi bằng `Gemini__MeterModel`). OpenAI mặc định dùng `gpt-4.1-mini` (có thể đổi bằng `OpenAI__MeterModel`). Hạn mức và chi phí phụ thuộc dự án/nhà cung cấp; xem trang Rate Limit của AI Studio hoặc Billing của OpenAI trước khi demo nhiều lần. API giới hạn 5 lần đọc AI mỗi phút cho một tài khoản.

## Kiểm tra

1. Mở `Thêm chỉ số điện nước`, chọn `Điện` và ảnh đồng hồ.
2. Khoanh đúng các chữ số **màu đen**, không gồm ô đỏ `1/10 kWh`; chọn số chữ số tương ứng.
3. Bấm **Đọc số từ ảnh**. Nếu WPF chưa đọc chắc chắn, trạng thái sẽ thông báo đang gửi qua API. Nếu API chưa có key, ứng dụng báo rõ và vẫn cho nhập tay.
4. Đối chiếu số gợi ý với ảnh, bấm **Dùng số gợi ý** hoặc sửa trực tiếp, đánh dấu xác nhận rồi mới **Lưu**.

Ngày 27/10/2026, Gemini thật tiếp tục trả `08424` cho ảnh thứ nhất và `08867` cho ảnh thứ hai khi khoanh sát dãy số đen (tương ứng giá trị nguyên `8424` và `8867`). WPF giữ số `0` ở đầu dãy khi hiện gợi ý hoặc đưa vào ô nhập. Bài kiểm tra WPF đã xác nhận đường chuyển sang API khi OCR tại máy chưa chắc chắn; bước chọn tệp và khoanh vùng trực tiếp trên cửa sổ thật vẫn cần kiểm tra riêng. Ảnh thứ hai hơi mờ, nên vẫn phải đối chiếu bằng mắt trước khi xác nhận. Ô đỏ biểu thị `1/10 kWh`, không ghép vào dãy số nguyên.
