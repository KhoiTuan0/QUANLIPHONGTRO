# Hướng dẫn sử dụng và phân quyền

## Vai trò

| Vai trò | Phạm vi vận hành |
| --- | --- |
| Admin — chủ trọ | Toàn bộ cơ sở; tài khoản/phân quyền, nghiệp vụ thuê, tài chính, báo cáo, kho chung, điều chuyển và thanh lý hiện vật, sao lưu |
| Manager — quản lý cơ sở | Phòng, tiếp nhận/cập nhật khách, đặt/nhận phòng, tạo/gia hạn/thanh lý hợp đồng, cư trú, chỉ số, sửa chữa và kiểm kê hiện vật trong phòng thuộc cơ sở được giao; thao tác vẫn chịu ràng buộc trạng thái |
| BackupOperator — phụ trách sao lưu | WPF giới hạn ở chức năng sao lưu được cấp. API cho xem/tạo sao lưu và phục hồi thành CSDL bản sao; không có quyền nghiệp vụ thuê phòng |
| Customer — khách thuê | Tài khoản do chủ trọ cấp để dùng phía mobile; không đăng nhập WPF. Hồ sơ khách thuê vẫn được quản lý trên desktop |

Quản lý không được sửa tài khoản/phân quyền, cấu hình toàn hệ thống, điều chuyển kho chung, thanh lý hiện vật hoặc sửa ngày/giá mua. Quản lý có thể đọc hóa đơn liên quan để xử lý hợp đồng trong phạm vi cho phép; không tương đương quyền quản trị tài chính của chủ trọ. API kiểm tra quyền và cơ sở, không chỉ dựa vào việc ẩn nút trên WPF.

## Tiếp nhận và cho thuê

1. Tìm khách theo thông tin định danh trước khi tạo hồ sơ mới để tránh trùng người.
2. Chọn phòng phù hợp ở cơ sở được giao. Đặt phòng dùng để giữ chỗ; nhận phòng/tạo hợp đồng là bước tiếp theo, không phải cùng một trạng thái.
3. Khách đang ở cơ sở khác vẫn có thể đặt phòng để chuẩn bị chuyển. Trước khi lập hợp đồng mới, xử lý hợp đồng/cư trú cũ theo ràng buộc; không tạo hồ sơ khách trùng chỉ để vượt điều kiện.
4. Tạo hợp đồng, lập bàn giao và ghi nhận cư trú theo nghiệp vụ. Xuất biểu mẫu từ dữ liệu đã lưu.
5. Khi gia hạn hoặc thanh lý, đối chiếu thời hạn, công nợ và tiền cọc trước khi xác nhận.

Thông tin liên hệ có thể cần cập nhật trong quá trình thuê. Quyền sửa phụ thuộc phạm vi quản lý và ràng buộc trường dữ liệu; có hợp đồng không có nghĩa toàn bộ thông tin khách phải khóa vĩnh viễn. Bản ký cũ cần lưu riêng vì dữ liệu liên hệ có thể thay đổi.

## Kho và hiện vật

- **Tài sản mẫu** là loại, ví dụ Giường gỗ. **Hiện vật** là từng chiếc có mã `TS...` riêng.
- Chủ trọ nhập số lượng một lần; mỗi chiếc được tạo mã riêng. Nhập 50 rồi chuyển 20 từ kho vào phòng thì còn 30 trong kho nếu chưa có giao dịch khác.
- Chức năng ghi nhận tài sản mới tại phòng không phải xuất từ kho có sẵn. Với đồ đã nhập kho, dùng luồng điều chuyển để tránh đếm hai lần.
- Mã hiện vật được giữ khi điều chuyển. Chủ trọ tra lịch sử theo mã; nên dán mã lên đồ thật để nhận diện.
- Quản lý xem số **Tại cơ sở**, kiểm kê và ghi sửa chữa/bảo dưỡng cho hiện vật trong phòng được giao. Họ không tự lấy đồ kho chung hoặc chuyển sang cơ sở khác.
- Ngày mua/giá mua là thông tin mua ban đầu; giá mua từng hiện vật khác giá tham khảo của mẫu. Chưa biết thì để thiếu, không suy diễn thành đã mua với giá 0.
- Chi phí sửa chữa ghi riêng theo sự kiện. Thanh lý hiện vật khác thanh lý hợp đồng; không dùng thay cho nhau.

## Chỉ số, OCR và hóa đơn

Chọn phòng/kỳ/loại chỉ số. Có thể nhập tay; với ảnh công tơ điện, chọn ảnh, khoanh đúng các ô số đen, đọc gợi ý, đối chiếu rồi xác nhận. Ô đỏ thập phân không ghép vào hàng đơn vị khi chọn chế độ số nguyên. OCR không tự lập hóa đơn hay xác nhận tiền thu.

Chỉ số đã dùng lập hóa đơn chịu ràng buộc sửa/xóa. Chủ trọ quản lý hóa đơn, thu tiền và công nợ. Chi phí phạt trong biên bản hoặc vi phạm không mặc nhiên là khoản đã thu.

## Cư trú và dữ liệu lịch sử

Ngày dọn vào/rời phòng mô tả việc cư trú; khác ngày gửi/xác nhận khai báo. Chủ trọ hoặc quản lý đúng cơ sở xem/duyệt hồ sơ được gửi vào hệ thống. Luồng giao diện mobile không thuộc bộ bàn giao này.

Không xóa dữ liệu đã phát sinh quan hệ chỉ để làm danh sách gọn. Dùng trạng thái kết thúc, hủy, rời đi hoặc thanh lý đúng nghiệp vụ để giữ lịch sử. Nút bị khóa hoặc API từ chối cần được giải thích theo thông báo cụ thể, không coi mọi trường hợp là lỗi máy chủ.

Kịch bản trình bày chi tiết: [DEMO.md](../QLPT.WPF/DEMO.md). Việc diễn tập/kiểm thử tiếp theo được tách khỏi đợt hoàn thiện hồ sơ này.
