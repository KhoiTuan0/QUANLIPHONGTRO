# Cài đặt và vận hành WPF/API

Các lệnh dưới đây là hướng dẫn chuẩn bị bàn giao, **chưa được thực thi như một đợt triển khai mới**. Thực hiện kiểm tra trên máy đích ở đợt sau.

## Cấu trúc

`WPF → HTTP API → SQL Server`

WPF nhập địa chỉ API ở màn hình đăng nhập. Chuỗi kết nối SQL đặt tại API; WPF không kết nối trực tiếp CSDL. OCR tại máy dùng thư mục `tessdata`; phần AI dự phòng được cấu hình ở API.

## Máy phát triển

- Windows, .NET SDK 8 theo `global.json`, SQL Server phù hợp với bản sao lưu đang nhận.
- Mở PowerShell tại thư mục gốc dự án. Chạy từng dự án WPF/API, không build toàn solution chứa dự án ngoài phạm vi bàn giao.
- Môi trường hiện tại dùng SQL Server `.\SQLEXPRESS` và CSDL `KLCN052_QuanLyPhongTro`. Khi chuyển máy phải xác nhận lại instance và tên CSDL.

Chuỗi kết nối mẫu dùng Windows Authentication, không chứa mật khẩu:

```text
Server=.\SQLEXPRESS;Database=KLCN052_QuanLyPhongTro;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;
```

`TrustServerCertificate=True` phục vụ cấu hình SQL cục bộ với chứng chỉ chưa được tin cậy. Triển khai máy chủ chính thức cần cấu hình chứng chỉ phù hợp.

Đặt chuỗi kết nối ở `ConnectionStrings:DefaultConnection` trong cấu hình API hoặc biến môi trường của tiến trình. Ví dụ chạy cục bộ trong PowerShell:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=.\SQLEXPRESS;Database=KLCN052_QuanLyPhongTro;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;'
dotnet run --project .\QLPT.API\QLPT.API.csproj --launch-profile http
```

Mở terminal thứ hai:

```powershell
dotnet run --project .\QLPT.WPF\QLPT.WPF.csproj
```

Tại màn hình đăng nhập nhập API `http://localhost:5180`. API phát triển có Swagger; bản chạy Production không mặc định mở Swagger.

## CSDL khi chuyển máy

1. Nhận bản sao lưu `.bak` mới, biết rõ nguồn CSDL và thời điểm tạo. Tệp phải ở vị trí SQL Server truy cập được.
2. Phục hồi sang CSDL mới trên máy đích bằng SSMS; tránh ghi đè CSDL có sẵn. Kiểm tra tương thích phiên bản SQL Server của bản sao lưu.
3. Ghi tên instance, tên CSDL, danh sách migration đã áp dụng vào phiếu bàn giao. API không tự áp dụng migration khi khởi động.
4. Đổi chuỗi kết nối API sang đúng CSDL phục hồi. Tài khoản Windows chạy API cần được cấp quyền SQL thích hợp.

Không chạy lại các script tạo bảng hoặc dữ liệu mẫu ở thư mục gốc trên CSDL đang sử dụng. Không chạy toàn bộ thư mục `migrations` một cách máy móc; bản `.bak` có thể đã chứa các thay đổi đó. Không chuyển sang bản sao test chỉ vì tên tệp gần giống bản demo.

## Chuẩn bị bản phát hành

Khi đến đợt triển khai, tạo hai thư mục đầu ra riêng bằng SDK 8:

```powershell
dotnet publish .\QLPT.API\QLPT.API.csproj -c Release --self-contained false -o .\artifacts\ban-giao\api
dotnet publish .\QLPT.WPF\QLPT.WPF.csproj -c Release --self-contained false -o .\artifacts\ban-giao\wpf
```

Đây là bản phụ thuộc runtime. Máy đích cần .NET 8 Windows Desktop Runtime cho WPF và ASP.NET Core Runtime 8 cho API, đúng kiến trúc bộ ứng dụng. Giữ toàn bộ DLL, thư mục runtime/native và `tessdata`; không chỉ chép `.exe`.

Trước khi giao thư mục API, kiểm tra các tệp cấu hình được publish kèm. Loại bỏ khóa OCR, mật khẩu, đường dẫn riêng và cấu hình phát triển không cần thiết khỏi bản giao; không đưa bí mật vào Git hoặc tài liệu.

Chạy API từ thư mục đã publish sau khi đặt chuỗi kết nối cho terminal đó:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
dotnet .\QLPT.API.dll --urls http://localhost:5180
```

Chạy `QLPT.WPF.exe` từ thư mục WPF. `launchSettings.json` không quyết định cổng khi chạy bản publish; vì vậy lệnh trên chỉ định cổng rõ ràng.

Nếu WPF chạy ở máy khác, `localhost` là chính máy đó: cần địa chỉ máy API, cấu hình mạng và HTTPS phù hợp. Hướng dẫn mặc định ở đây dành cho chạy cục bộ, không phải mở dịch vụ công khai lên Internet.

## Vận hành và tiếp quản

- `/api/health/live`: API còn hoạt động. `/api/health`: kiểm tra kết nối CSDL. Các địa chỉ này dành cho đợt xác nhận triển khai sau.
- Chủ trọ cấp tài khoản và phân cơ sở; bàn giao mật khẩu riêng, đổi mật khẩu ban đầu theo quy trình tiếp quản.
- Bản `.bak` nằm trên máy SQL Server. Lịch sử sao lưu trong ứng dụng không phải nơi chứa nội dung tệp.
- Phục hồi bản sao tạo CSDL riêng; không tự chuyển API sang CSDL mới. Người vận hành quyết định sau khi đối chiếu.
- Cấu hình OCR xem [OCR_SETUP.md](../QLPT.API/OCR_SETUP.md). Không bắt buộc có khóa AI để nhập chỉ số bằng tay. Không gửi khóa qua tài liệu bàn giao công khai.
- Khi cập nhật ứng dụng, chuẩn bị bản sao lưu và lưu phiên bản cũ trước. Đổi schema chỉ theo migration phù hợp; không mặc định rằng phiên bản cũ sẽ chạy được trên schema mới.

## Thông tin không đóng gói trong tài liệu

Mật khẩu, cookie đăng nhập, khóa Gemini/OpenAI, bản sao CSDL chứa thông tin cá nhân và bản hợp đồng đã ký được giao riêng cho đúng người được phép tiếp nhận.
