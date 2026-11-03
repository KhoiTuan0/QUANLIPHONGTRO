# Kiểm thử Flutter và API dùng chung — 03/11/2026

## Môi trường trên máy Khôi

- Flutter 3.47.9, Dart 3.13.7, SDK ở `C:\dev\flutter`.
- PATH người dùng đã thêm `C:\dev\flutter\bin`; mở terminal mới để nhận PATH.
- Windows Developer Mode đã được chủ máy đồng ý bật để hỗ trợ plugin symlink.
- Đường dẫn ngắn `C:\dev\qlpt-integration` là junction đến cùng worktree; script
  Run-Mobile tự dùng đường dẫn này để tránh giới hạn đường dẫn tlog của MSBuild.
- Firebase C++ 13.12.0 ở `C:\dev\firebase-cpp\firebase_cpp_sdk_windows`: tải chọn
  lọc header/CMake và thư viện App/Auth MD x64, kiểm CRC từng tệp từ archive chính
  thức; Windows engine đúng revision, bỏ PDB profile/release không cần để chạy.
- SQL Server SQLEXPRESS; API kiểm thử ở `http://127.0.0.1:5187`.
- CSDL kiểm thử: `QLPT_Test_20261103_b69f073a`, phục hồi từ bản COPY_ONLY có CHECKSUM
  của `KLCN052_QuanLyPhongTro`. Tài khoản và dữ liệu phát sinh chỉ ở bản sao.
- Android SDK/giả lập chưa được cài. Kết quả Flutter Windows không thay thế
  kiểm thử APK, camera, chọn ảnh, quyền thông báo và push trên thiết bị Android.

## Kết quả đã xác nhận

| Kiểm tra | Kết quả |
| --- | --- |
| `flutter pub get` | Thành công |
| `flutter analyze --no-pub --no-fatal-infos` | Exit 0; không còn error/warning; 49 góp ý mức info về phong cách mã |
| `flutter test --no-pub` | 29 đạt; 1 live test bỏ qua mặc định |
| Live test Dart với API/SQL thật | 1 kịch bản đạt: đăng nhập Customer, hồ sơ, lịch sử điện nước/dịch vụ, danh sách gia hạn, hóa đơn/PDF, chat, đăng xuất, đổi phiên; truy cập hồ sơ/chat khách khác bị chặn |
| Kiểm thử HTTP/SQL hiện có | 85 kiểm tra chính + 31 bổ sung đạt trên bản sao |
| WPF với API/SQL thật | 26 kiểm tra đạt: chặn Customer vào Desktop, đăng nhập Admin, smoke các trang và đăng xuất |
| Build Flutter Windows debug | Thành công; có cảnh báo thiếu PDB của Firebase/libcurl, không chặn build |
| Mở ứng dụng Windows | Màn hình đăng nhập hiển thị đầy đủ, đã lưu ảnh bằng chứng; đăng nhập/quyền dữ liệu được kiểm bằng live test Dart riêng |
| Chạy trên máy Toàn | Chờ Toàn xác nhận cùng nhánh, cấu hình và CSDL |

HTTP 403 trong log live test là kết quả mong đợi khi thử đọc dữ liệu khách khác.
Các test mặc định dùng mock không thay thế live test SQL. Live test chỉ được bật
bằng `QLPT_LIVE_TEST=true` cùng fixture riêng; không chạy bằng tài khoản/CSDL demo.

## Các sửa đổi phát hiện trong quá trình cài/test

- Ứng dụng vẫn khởi động khi Firebase push chưa cấu hình được; chỉ khởi tạo push
  trên Android/iOS, không gọi plugin messaging không hỗ trợ Windows.
- Không ghi token FCM ra log.
- OCR Mobile nhận địa chỉ qua `METER_OCR_URL`; API nhận qua `API_BASE_URL`.
- Lịch sử dịch vụ đọc đúng `donGia` và `tenPhong` API trả về.
- Test lịch sử dùng endpoint Customer có giới hạn phía server; test hóa đơn kiểm
  tra danh sách mã vi phạm và test đăng nhập kiểm tra widget thực tế.
- Có test opt-in `mobileui/test/live_api_test.dart` dùng chính ApiService thay vì
  mô phỏng HTTP client để xác nhận cookie phiên và cách ly dữ liệu.

## Đổi máy chạy dự phòng

Cả hai máy cần cùng commit và bản CSDL đã chốt. Mật khẩu/khóa được cấu hình riêng
trên từng máy; không sao chép file cấu hình chứa bí mật lên GitHub.

Tại thư mục gốc, chạy API nội bộ:

```powershell
.\HO_SO_BAN_GIAO\Run-SharedApi.ps1 -SqlServer '.\SQLEXPRESS' -Database KLCN052_QuanLyPhongTro
```

Nếu điện thoại hoặc máy còn lại gọi API qua LAN:

```powershell
.\HO_SO_BAN_GIAO\Run-SharedApi.ps1 -SqlServer '.\SQLEXPRESS' -Database KLCN052_QuanLyPhongTro -Lan
```

Cho phép cổng API trên mạng tin cậy nếu Windows Firewall đang chặn. WPF điền
`http://<IP_MAY_API>:5180` ở ô địa chỉ API trên màn hình đăng nhập. Flutter chạy:

```powershell
.\HO_SO_BAN_GIAO\Run-Mobile.ps1 -ApiBaseUrl 'http://<IP_MAY_API>:5180/api' -Device windows
```

Android Emulator cùng máy API dùng `http://10.0.2.2:5180/api`, Device là mã thiết
bị từ `flutter devices`. Điện thoại thật dùng IP LAN của máy API.
Nếu demo OCR Mobile, bổ sung `-MeterOcrUrl 'http://<IP_MAY_OCR>:5000/api/ocr-dong-ho'`
và chạy riêng dịch vụ OCR của Toàn. OCR WPF vẫn dùng API .NET hiện có.

Chuyển máy sau khi dừng ghi trên máy cũ, sao lưu CSDL mới nhất và phục hồi trên máy
dự phòng. Chỉ đổi địa chỉ API mà không chuyển CSDL mới nhất sẽ có dữ liệu cũ.
Không chạy đồng thời hai bản CSDL độc lập rồi coi chúng tự đồng bộ.

## Bản chạy và bằng chứng trên máy Khôi

- Bản Flutter vừa build:
  `C:\dev\qlpt-integration\mobileui\build\windows\x64\runner\Debug\mobileui.exe`.
  Bản này trỏ API thử `http://127.0.0.1:5187/api`, dùng CSDL bản sao; không coi
  dữ liệu thử trong ứng dụng này là bộ dữ liệu trình bày chính thức.
- Ảnh và log: `BANG_CHUNG_KIEM_THU_20261103/` cạnh tài liệu này.
- WPF giữ các cảnh báo thư viện .NET cũ và hai cảnh báo C# đã có trước; build
  không lỗi, smoke test đạt. Không mở rộng sửa WPF trong lượt cài Flutter này.
- Thư mục build lỗi đường dẫn dài được chuyển nguyên vẹn sang
  `C:\dev\qlpt-longpath-build-20261103`; đây là output build tạm, không phải mã
  nguồn hoặc bản SQL chốt. Có thể dọn sau khi nghiệm thu bản chạy mới.

## Việc còn phải xác nhận trước bản nộp

- Toàn chạy Mobile/Android thật, kiểm tra camera/chọn ảnh, PDF và quyền thiết bị.
- Firebase push nền, OCR Mobile và giao diện khai báo cư trú vẫn theo các giới hạn
  đã ghi trong `TICH_HOP_MOBILE_20261102.md`; không suy ra đã đạt từ lượt test này.
- Chốt dữ liệu demo sạch và backup nền sau khi hai máy hoàn thành diễn tập.
