# Kịch bản diễn tập với GVHD — 03/11/2026

Tài liệu này dùng cho buổi diễn tập trước giáo viên hướng dẫn. Dữ liệu sạch đã
được chốt ngày 03/11/2026. Mã phòng, khách, hợp đồng và hóa đơn để dùng ở các ô
`[CHỌN TRƯỚC]` đã liệt kê tại [bảng hồ sơ diễn tập](CHOT_DIEN_TAP_20261103.md).
Sau mỗi lần thao tác ghi, số liệu trên màn hình có thể thay đổi; phục hồi backup
nền trước lần diễn tập tiếp theo nếu muốn bắt đầu đúng cùng trạng thái.

## 1. Mục tiêu buổi diễn tập

- Chứng minh hệ thống giải quyết trọn luồng quản lý phòng trọ, không chỉ CRUD.
- Cho thấy API kiểm tra trạng thái, quan hệ và phạm vi cơ sở; WPF không chỉ ẩn nút.
- Trình bày dữ liệu hợp lý như một nhà trọ đang vận hành thật.
- Thao tác ghi dữ liệu có kiểm soát và có bản sao nền để phục hồi sau buổi demo.
- Nêu đúng giới hạn hiện tại, không giới thiệu chức năng chưa kiểm thử là đã hoàn tất.

## 2. Bộ dữ liệu nền cần chốt sau khi Toàn kiểm thử

Không giữ hồ sơ thử như `huy`, `quang`, tên viết thường, CCCD/SĐT giả ngắn hoặc
nội dung `test`. Không xóa riêng một khách đang có quan hệ; ưu tiên phục hồi bản
sao nền sạch hoặc chạy kịch bản dọn dữ liệu đã được kiểm tra trên bản sao CSDL.

### Danh mục tối thiểu

| Nhóm | Dữ liệu mục tiêu |
| --- | --- |
| Cơ sở | 2 cơ sở có tên, địa chỉ và SĐT rõ ràng: Quận 7 và Thủ Đức |
| Phòng | 11 phòng; có đủ trạng thái đang thuê, trống và bảo trì; tên nhất quán A101, A102, A201, B101... |
| Loại phòng | Phòng tiêu chuẩn, phòng ban công, phòng máy lạnh; giá thuê hợp lý |
| Khách | Chỉ dùng họ tên chỉnh chu như Nguyễn Minh Anh, Trần Quốc Huy, Phạm Thùy Linh, Lê Hoàng Nam, Bùi Đức Khang, Huỳnh Nhật Minh, Nguyễn Khánh Vy, Võ Thanh Hà, Đỗ Gia Bảo |
| Đặt phòng | 1 phiếu đã nhận phòng, 1 phiếu đã hủy, 1 phiếu đang giữ chỗ hoặc chờ xử lý hủy |
| Hợp đồng | Có hợp đồng hiệu lực, sắp hết hạn và đã thanh lý; ngày/giá/cọc khớp phòng |
| Người ở thực tế | Ít nhất 1 hợp đồng có người đứng tên khác người thực tế ở để giải thích nghiệp vụ |
| Cư trú | 1 hồ sơ đã duyệt, 1 hồ sơ chờ duyệt hoặc bị trả lại có lý do |
| Chỉ số | Hai kỳ liên tiếp cho điện và nước của phòng đang thuê; chỉ số mới không nhỏ hơn chỉ số cũ |
| Hóa đơn | 1 đã thu, 1 còn nợ và 1 thu một phần; chi tiết tiền phòng, điện, nước, dịch vụ rõ ràng |
| Vi phạm | 1 vi phạm đã xử lý nhưng chưa đưa vào hóa đơn và 1 khoản đã tính đúng một lần |
| Sửa chữa | 1 yêu cầu đang xử lý và 1 yêu cầu hoàn thành gắn đúng mã hiện vật |
| Tài sản | 4 mẫu tài sản; nhiều hiện vật có mã `TS...`, có tồn kho, tại cơ sở, trong phòng và lịch sử kiểm kê/sửa chữa |
| Tài khoản | Admin, Manager được giao đúng cơ sở, BackupOperator và Customer gắn đúng hồ sơ; không công khai mật khẩu trong tài liệu |
| Thông báo | 1 thông báo đã đọc và 1 chưa đọc; không trộn tin nhắn chat vào thông báo nghiệp vụ |

### Hồ sơ nên chọn trước khi trình bày

- `[PHÒNG TRỐNG]`: dùng để minh họa đặt phòng, không chọn phòng đang thuê/bảo trì.
- `[KHÁCH ĐẶT]`: hồ sơ sạch, chưa có phiếu giữ chỗ đang hiệu lực cho cùng phòng.
- `[HỢP ĐỒNG HIỆU LỰC]`: có đủ người đại diện, người ở, cọc và bàn giao.
- `[HÓA ĐƠN CÒN NỢ]`: dùng minh họa thu một phần; ghi sẵn số dư trước khi demo.
- `[HIỆN VẬT]`: có mã riêng, đang ở đúng phòng và đã có ít nhất một lần kiểm kê.
- `[YÊU CẦU SỬA]`: chưa hoàn thành, thuộc đúng phòng/cơ sở của tài khoản trình bày.
- Hai ảnh công tơ thật đã thử: số đen `08424` và `08867`; số đỏ là 1/10 kWh.

## 3. Chuẩn bị trước giờ trình bày

1. Chốt kết quả kiểm thử Mobile–API với Toàn; lỗi Mobile chưa xong không được đưa
   vào luồng demo chính của phần Desktop.
2. Làm sạch CSDL, kiểm tra các quan hệ và tạo bản sao nền `.bak` trước khi tập.
3. Chạy đúng một API; kiểm tra `http://localhost:5180/api/health` trả
   `database: connected` và Swagger JSON tải được.
4. Mở WPF, đăng nhập Admin, kiểm tra Dashboard, Phòng, Khách, Hợp đồng và Hóa đơn.
5. Mở sẵn thư mục ảnh OCR và thư mục xuất biểu mẫu; tắt thông báo không liên quan.
6. Ghi ra giấy các mã `[CHỌN TRƯỚC]`; không tìm ngẫu nhiên trong lúc nói.
7. Chuẩn bị tài khoản Manager nhưng không ghi mật khẩu lên slide hoặc tài liệu.
8. Sau một lần diễn tập có thao tác ghi, phục hồi bản sao nền để lần sau bắt đầu
   đúng cùng trạng thái.

## 4. Kịch bản chính 12–15 phút

### 0:00–1:00 — Mở đầu và kiến trúc

**Nói:** “Hệ thống gồm WPF dành cho chủ trọ/quản lý, ASP.NET Core API xử lý
nghiệp vụ và SQL Server lưu dữ liệu. Mobile dành cho khách dùng tài khoản do
chủ trọ cấp. Mọi ràng buộc quan trọng được kiểm tra lại ở API, không chỉ ở giao
diện.”

Mở Dashboard, chỉ nhanh tổng phòng, phòng trống, đang thuê, hợp đồng và công nợ.
Đổi khoảng doanh thu 3/6/12 tháng hoặc tùy chọn; đổi số ngày hợp đồng sắp hết hạn.

### 1:00–3:00 — Phân quyền theo cơ sở

Mở Cơ sở, Phòng và Tài khoản. Giải thích:

- Admin xem toàn bộ cơ sở, tài chính, tài khoản, kho, sao lưu.
- Manager gần giống chủ trọ trong vận hành hằng ngày nhưng chỉ tại cơ sở được giao;
  không quản trị tài khoản, kho chung, điều chuyển liên cơ sở hoặc sao lưu.
- Customer không đăng nhập WPF; tài khoản dùng phía Mobile.
- BackupOperator chỉ phụ trách sao lưu.

Nếu đủ thời gian, đăng nhập Manager ở cửa sổ đã chuẩn bị và cho thấy danh sách chỉ
còn phòng/khách/hợp đồng thuộc cơ sở được giao. Không sửa dữ liệu ở bước này.

### 3:00–5:00 — Đặt phòng đến hợp đồng và cư trú

Mở Đặt phòng, chọn `[PHÒNG TRỐNG]` và `[KHÁCH ĐẶT]` nhưng có thể dừng trước nút
Lưu nếu đang dùng CSDL nền. Giải thích:

- Chỉ phòng trống mới được giữ chỗ.
- Một khách có thể đứng tên thuê thêm phòng cho người thân; người đứng tên hợp
  đồng và người thực tế ở được lưu riêng.
- Khách đang thuê có thể chuẩn bị chuyển sang cơ sở khác, nhưng không được tạo
  hợp đồng/cư trú chồng chéo trái quy tắc chỉ để vượt trạng thái.
- Hủy giữ chỗ đi qua yêu cầu, duyệt/từ chối và số tiền hoàn cọc rõ ràng.

Mở `[HỢP ĐỒNG HIỆU LỰC]`, chỉ người đại diện/người ở, thời hạn, tiền cọc; mở Cư
trú và chỉ trạng thái chờ duyệt/đã khai báo. Nói rõ đây là phê duyệt nội bộ, chưa
phải gửi hồ sơ trực tiếp tới cơ quan nhà nước.

### 5:00–7:00 — Điện nước, OCR và hóa đơn

Mở lịch sử chỉ số hai kỳ của một phòng đang thuê. Nêu ràng buộc chỉ số mới không
nhỏ hơn cũ, một kỳ/loại không ghi trùng và chỉ số đã lên hóa đơn không tùy ý sửa.

Chọn ảnh công tơ, khoanh đúng năm ô số đen, bấm đọc. Nói:

“OCR chỉ là gợi ý. Người dùng phải đối chiếu ảnh và xác nhận trước khi lưu; số đỏ
trên công tơ này là phần thập phân nên không ghép vào năm số đen.”

Không bấm Thêm nếu chỉ minh họa. Mở `[HÓA ĐƠN CÒN NỢ]`, chỉ chi tiết tiền phòng,
điện, nước, dịch vụ, đã thu và còn lại. Nếu cần ghi tiền, thực hiện thu một phần
trên bản sao CSDL và cho thấy công nợ/sổ thu chi cập nhật một lần.

### 7:00–9:00 — Tài sản theo từng hiện vật

Mở Tài sản, chọn mẫu Giường gỗ và cho thấy các mã `TS...` riêng biệt. Giải thích:

- Mua 50 chiếc thì nhập số lượng một lần nhưng hệ thống sinh 50 mã hiện vật.
- Chuyển 20 chiếc từ kho vào phòng làm tồn kho giảm còn 30; mã không đổi khi
  điều chuyển.
- Quản lý cơ sở chỉ kiểm kê/sửa chữa hiện vật trong phòng thuộc cơ sở mình.
- Chủ trọ xem được toàn bộ hành trình, ngày/giá mua và tổng chi phí sửa theo mã.

Mở lịch sử `[HIỆN VẬT]`, chỉ sự kiện kiểm kê/sửa chữa. Không tạo lô 50 chiếc trên
CSDL trình bày; chỉ mô tả hoặc làm trên bản sao nếu giáo viên yêu cầu.

### 9:00–11:00 — Sửa chữa, vi phạm và tính tiền

Mở `[YÊU CẦU SỬA]`, cho thấy phòng, khách, mã hiện vật, trạng thái và chi phí.
Giải thích yêu cầu chung của phòng có thể không gắn hiện vật; sửa đúng chiếc thì
phải chọn mã `TS...` để cập nhật lịch sử tài sản.

Mở Vi phạm. Nói rõ “xử lý vi phạm” chốt mức phạt nhưng chưa có nghĩa đã thu tiền.
Khoản phạt được chọn đưa vào một hóa đơn phù hợp và khóa chống cộng trùng; thu
tiền là nghiệp vụ riêng.

### 11:00–13:00 — Báo cáo, biểu mẫu và sao lưu

Mở Báo cáo, đổi khoảng thời gian và chỉ doanh thu, đã thu, còn nợ. Xuất Excel nếu
đã tập trước. Ở Hợp đồng, chọn hợp đồng rồi Xuất HĐ/Xuất bàn giao; cho thấy biểu
mẫu A4 lấy dữ liệu đang lưu và có phần ký của hai bên.

Mở Sao lưu và giải thích `.bak` nằm trên máy SQL Server. Chỉ bấm tạo sao lưu nếu
đã thử quyền trước; không khôi phục trong luồng chính. Khôi phục tạo CSDL bản sao,
không ghi đè CSDL đang chạy.

### 13:00–15:00 — Kết luận và câu hỏi

**Nói:** “Điểm chính của hệ thống là dữ liệu xuyên suốt từ phòng, khách, đặt
phòng, hợp đồng, cư trú, chỉ số, hóa đơn đến tài sản và báo cáo; trạng thái cũ
được giữ làm lịch sử thay vì xóa tùy tiện. Phần Mobile dùng cùng API/CSDL và đang
được kiểm thử tích hợp riêng trước khi chốt bản nộp.”

## 5. Các thao tác chỉ làm cuối buổi hoặc trên bản sao

- Nhận phòng, tạo hợp đồng, thanh lý, báo trả và duyệt hoàn cọc.
- Thu tiền thật, đưa khoản phạt vào hóa đơn.
- Nhập kho hàng loạt, điều chuyển hoặc thanh lý hiện vật.
- Duyệt cư trú, hoàn thành sửa chữa.
- Khôi phục CSDL.

Các thao tác trên đổi nhiều bảng và trạng thái. Nếu giáo viên yêu cầu, thực hiện
trên bản sao đã chuẩn bị rồi quay lại bản nền sau buổi diễn tập.

## 6. Câu hỏi dự kiến và câu trả lời ngắn

| Câu hỏi | Trả lời |
| --- | --- |
| Vì sao khách hủy đặt vẫn còn trong Khách thuê? | Hồ sơ khách là danh mục người, không phải trạng thái thuê. Hủy phiếu chỉ kết thúc lần giữ chỗ; giữ hồ sơ để đặt lại và bảo toàn lịch sử. |
| Khách đang thuê có được thuê thêm phòng? | Có thể đứng tên thêm cho người thân nếu phòng mới trống và tạo hợp đồng/người ở thực tế rõ ràng; hệ thống không tạo hồ sơ khách trùng để lách ràng buộc. |
| Vì sao Manager không làm mọi thứ như Admin? | Manager vận hành cơ sở được giao; tài khoản toàn hệ thống, tài chính tổng, kho chung, điều chuyển liên cơ sở và sao lưu thuộc chủ trọ. |
| Một mẫu Giường và từng chiếc khác nhau thế nào? | Mẫu là chủng loại; mỗi chiếc là hiện vật có mã riêng, vị trí và lịch sử sửa chữa riêng. |
| OCR sai thì sao? | OCR chỉ gợi ý; ảnh và vùng số được hiển thị để người dùng đối chiếu, sửa và xác nhận trước khi lưu. |
| Có sửa CCCD/SĐT khách đang thuê không? | Thông tin có thể cập nhật theo quyền; thay đổi định danh quan trọng được ghi lịch sử cũ/mới, thời điểm và tài khoản sửa. |
| Thông báo có phải push thời gian thực? | Danh sách thông báo/chat dùng API và CSDL. Firebase push nền chưa được coi là hoàn tất nếu chưa kiểm thử cấu hình Mobile. |
| Cư trú có gửi công an không? | Chưa. Đây là luồng khách gửi hồ sơ và chủ trọ/quản lý duyệt nội bộ; tích hợp cổng nhà nước là hướng phát triển. |
| Sao lưu nằm ở đâu? | Tệp `.bak` được tạo trên máy SQL Server. Lịch sử trong WPF mô tả các bản sao; quyền tạo/khôi phục được phân riêng. |

## 7. Điều không nên làm khi trình bày

- Không nói “tất cả đã hoàn hảo”; nói đúng phần đã kiểm thử và giới hạn hiện tại.
- Không đăng nhập nhiều API cùng cổng 5180 hoặc mở API từ hai Visual Studio.
- Không sửa trực tiếp chuỗi kết nối, chạy migration hay script mẫu trước mặt giáo viên.
- Không thử ngẫu nhiên hồ sơ chưa biết trạng thái.
- Không công khai mật khẩu, API key, chuỗi kết nối có bí mật hoặc file SQL đầy đủ.
- Không dùng dữ liệu `huy`, `quang`, `test`, số điện thoại/CCCD sai định dạng trong
  bộ dữ liệu trình bày chính thức.

## 8. Việc chốt sau khi Toàn trả kết quả

1. Sửa lỗi tích hợp thực sự nếu có; chạy lại build và kiểm tra API/WPF.
2. Làm sạch dữ liệu theo quan hệ và dựng lại ma trận dữ liệu ở Mục 2.
3. Điền các mã `[CHỌN TRƯỚC]` vào bản kịch bản này.
4. Tạo `.bak` nền và diễn tập từ đầu đến cuối một lần có bấm lưu.
5. Phục hồi nền, chạy lại lần thứ hai không cần trợ giúp.
6. Sau khi dữ liệu cuối được xác nhận, xuất lại SQL chốt và lưu bằng chứng kiểm thử.
