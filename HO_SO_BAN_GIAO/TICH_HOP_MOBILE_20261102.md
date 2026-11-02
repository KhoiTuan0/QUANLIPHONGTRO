# Bản tích hợp để kiểm thử Mobile — 02/11/2026

Nhánh: codex/integration-final. Nền API/WPF: efda895. Mobile lấy từ
Toan2208/main tại c106758 (Mobile 05fda63). Đây là bản tích hợp chờ kiểm thử
Flutter trên máy Toàn, chưa phải bản main đã nghiệm thu.

## Phần đã tích hợp

- Mobile mới của Toàn; giữ API/WPF hiện có, không thay cả API bằng bản Toàn.
- Cookie phiên được gửi qua các phương thức API Mobile; API_BASE_URL có thể
  đặt bằng dart-define. Android Emulator mặc định dùng http://10.0.2.2:5180/api.
- Hủy đặt phòng sử dụng cùng endpoint và số tiền hoàn rõ ràng với WPF;
  duyệt vẫn dùng khóa/giao dịch và quy tắc hoàn cọc của API hiện có.
- Yêu cầu sửa chữa và xác nhận hoàn tất gọi endpoint Customer.
- Lịch sử điện nước/dịch vụ sử dụng API giới hạn theo hồ sơ và thời gian thuê.
- Thông báo Customer dùng mã khách để tra đúng tài khoản nhận.
- Bổ sung gia hạn của Toàn; quản lý chỉ xem/duyệt cơ sở được giao.
  Ghi chú trạng thái gia hạn dùng bảng GiaHanHopDong hiện có, giới hạn nội dung 280 ký tự.
- Bổ sung PDF hóa đơn của Toàn với font tiếng Việt; Customer chỉ xem hóa đơn của mình.
- Chat Admin–Customer dùng ThongBao/ThongBaoNguoiNhan; phía máy chủ xác định
  vai trò người gửi, không tin isAdmin từ Mobile. Không lẫn chat vào thông báo.
- Đổi mật khẩu xác thực mật khẩu hiện tại, lấy tài khoản từ phiên đăng nhập,
  vô hiệu phiên sau khi đổi. Customer cập nhật liên hệ; định danh phải qua quản lý.
- Chủ trọ có endpoint cấp hồ sơ/tài khoản Customer trong một giao dịch;
  đăng ký công khai không được mở. Login Mobile hướng dẫn liên hệ chủ trọ.
- Lập hóa đơn thủ công Mobile gửi danh sách mã vi phạm để API gắn khoản phạt
  theo giao dịch và đánh dấu đã tính, dùng chung quy tắc chống tính trùng với WPF.

## Bằng chứng hiện có

- API build thành công.
- WPF build thành công; còn các cảnh báo thư viện tương thích .NET đã có trước.
- 278 kiểm tra API độc lập đạt, có kiểm tra quyền các endpoint mới, giả mạo
  khách chat, giới hạn mật khẩu và sinh byte PDF. Các kiểm tra này không dùng SQL.
- Không thay đổi file WPF/WinForms hoặc database demo trong đợt tích hợp này.
- Chưa chạy flutter analyze, flutter test hoặc APK; chưa xác nhận luồng end-to-end
  với SQL cho các phần mới. Không suy ra các bước đó đã đạt từ kết quả build API.

## Toàn chạy trên máy có Flutter

Lấy nhánh codex/integration-final từ fork KhoiTuan0, dùng một checkout riêng.
API dùng cấu hình SQL của máy chạy API; không lấy chuỗi kết nối máy khác.
Chạy đúng một API:

    dotnet run --project QLPT.API --launch-profile http

Nếu điện thoại/giả lập ở máy khác, API phải lắng nghe địa chỉ mạng và firewall
cho phép cổng 5180 trên mạng tin cậy. Thay <IP_MAY_API> bằng IP thật:

    dotnet run --project QLPT.API --no-launch-profile --urls http://0.0.0.0:5180 --environment Development
    cd mobileui
    flutter pub get
    flutter analyze
    flutter test
    flutter run --dart-define=API_BASE_URL=http://<IP_MAY_API>:5180/api

Android Emulator cùng máy API dùng 10.0.2.2; điện thoại thật dùng IP LAN.
Hai ứng dụng phải gọi cùng tiến trình API để dùng cùng database.

Kiểm thử bằng bản sao database, chuẩn bị Admin, Customer A, Customer B và
Manager theo cơ sở. Không gửi mật khẩu thật trong báo lỗi.

1. Đăng nhập Customer, tải phòng/hợp đồng/hóa đơn của mình; thử truy cập mã khách
   khác phải bị chặn. Đăng xuất và đăng nhập người khác không giữ dữ liệu phiên cũ.
2. Đặt phòng trống -> WPF thấy phiếu -> gửi yêu cầu hủy -> WPF hoặc Mobile Admin
   duyệt số tiền hoàn -> phòng trống và phiếu thu chi khớp, không duyệt hai lần.
3. Customer gửi sửa chữa -> WPF xử lý hoàn thành -> Customer xác nhận; không xác
   nhận trước khi chủ trọ hoàn thành hoặc sửa yêu cầu người khác.
4. Gia hạn -> chủ trọ/đúng quản lý duyệt -> ngày kết thúc hiện đúng trên WPF;
   quản lý khác cơ sở bị chặn, hợp đồng đổi thời hạn trước đó phải báo xung đột.
5. Chủ trọ gửi thông báo -> Customer đọc; chat hai khách không xem lẫn nhau.
6. Tải PDF: chữ tiếng Việt, tổng tiền, tiền đã thu/còn lại khớp WPF;
   Customer không tải PDF hóa đơn người khác.
7. Lập hóa đơn Mobile, tiền phạt được ghi thành chi tiết và đánh dấu đã tính;
   WPF thấy cùng tổng tiền, lần lập sau không cộng lại khoản phạt đó.
8. Cấp tài khoản từ chủ trọ; Customer đổi liên hệ và mật khẩu; đăng nhập lại bằng
   mật khẩu mới; Customer không tự thay CCCD/họ tên.

## Điểm cần Toàn xác nhận trước khi chốt

- Kiểm thử Flutter có thể cần cập nhật các test cũ đang kỳ vọng URL cũ.
- Firebase push: mã Mobile có gọi TinNhan/register-device nhưng API chung chưa
  có dịch vụ gửi FCM/credentials; chat và danh sách thông báo là HTTP.
  Không coi push nền đã hoàn tất. Toàn xác nhận có đưa FCM vào phạm vi bàn giao không.
- OCR Mobile vẫn là dịch vụ Flask/Ollama của Toàn ở cổng 5000, độc lập OCR
  Gemini/Tesseract trên WPF. Khi chạy máy khác phải cấu hình riêng dịch vụ này.
- Mobile của Toàn chưa có màn hình khai báo cư trú; API và WPF vẫn có luồng đó.
  Cần nối màn hình Mobile nếu đưa vào kịch bản demo chung.
- Chức năng duyệt gia hạn mới hiện có trên API/Mobile; WPF vẫn dùng chức năng
  gia hạn trực tiếp vốn có, chưa có danh sách duyệt yêu cầu gia hạn mới.

Sau khi Toàn trả kết quả, sửa lỗi, kiểm thử SQL bằng bản sao, mới hợp nhất main,
làm dữ liệu demo sạch và xuất SQL/.bak chốt mới. Không restore/xóa database thật
chỉ để thử bản tích hợp này.
