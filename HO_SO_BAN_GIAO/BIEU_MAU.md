# Biểu mẫu hợp đồng và bàn giao phòng

## 1. Hợp đồng thuê phòng

Vào **Hợp đồng → chọn dòng hợp đồng → Xuất HĐ**. Chọn vị trí lưu `HopDong_<mã>.html`. Trình duyệt mở tệp đã lưu; nếu không tự mở, mở tệp bằng trình duyệt.

Ứng dụng đọc lại dữ liệu từ API khi xuất, gồm mã hợp đồng, ngày ký, phòng, cơ sở, khách đại diện, CCCD, liên hệ, thời hạn, giá thuê tháng, tiền cọc và ghi chú. Không lấy nội dung chưa lưu trong hộp thoại chỉnh sửa.

Mẫu gồm năm nhóm:

1. Thông tin bên cho thuê và bên thuê.
2. Phòng, thời hạn, giá thuê và cọc.
3. Thỏa thuận ngày thanh toán, phương thức, điện/nước, dịch vụ, hoàn hoặc khấu trừ cọc.
4. Biên bản bàn giao kèm theo, số người ở, bảo quản và sửa chữa.
5. Báo trước, kết thúc, gia hạn, công nợ, thỏa thuận khác và chữ ký.

**Các chỗ trống cần hai bên điền và thống nhất trước khi ký.** Phần mềm không tự suy đoán thông tin chủ trọ, địa chỉ nhà, đơn giá dịch vụ, hạn thanh toán hay điều kiện khấu trừ cọc. Mẫu là bản chuẩn bị để hoàn thiện theo giao dịch cụ thể; không coi nội dung còn trống là hợp đồng đã hoàn tất.

Xuất/in không tạo hợp đồng mới, không ghi nhận thanh toán, không cập nhật các thỏa thuận bổ sung vào CSDL. Nội dung viết thêm trên giấy cần được lưu cùng bản ký; cập nhật dữ liệu nghiệp vụ liên quan trong hệ thống khi phù hợp.

## 2. Biên bản bàn giao phòng/tài sản

Vào **Hợp đồng → chọn hợp đồng → Xuất bàn giao → chọn đúng biên bản nhận/trả đã lưu**. Lưu `BanGiao_<mã>.html`.

Nội dung gồm mã và loại biên bản, hợp đồng, phòng, khách đại diện, ngày bàn giao, chỉ số điện/nước, tài sản, số lượng, tình trạng, chi phí phạt và ghi chú. Có chỗ ghi người giao/nhận và nội dung đối chiếu bổ sung.

- Nếu chưa có biên bản, cần thực hiện nghiệp vụ bàn giao trước; xuất biểu mẫu không tự tạo dữ liệu thay thế.
- Nếu biên bản chưa ghi tài sản, bảng ghi rõ tình trạng đó; không có nghĩa phòng chắc chắn không có tài sản.
- Bảng tài sản phản ánh chi tiết của biên bản đã lưu, không phải toàn bộ lịch sử từng mã hiện vật. Tra mã `TS...` và lịch sử ở mục **Tài sản**.
- Chi phí phạt ghi trên biên bản không phải xác nhận đã thu tiền.

## 3. In hoặc lưu PDF

1. Mở tệp HTML bằng trình duyệt, bấm **In / lưu PDF** hoặc `Ctrl+P`.
2. Chọn máy in hoặc **Lưu dưới dạng PDF**; khổ giấy **A4**, hướng **dọc**. Dùng tỷ lệ mặc định, xem trước trước khi in.
3. Tắt đầu/chân trang tự thêm của trình duyệt nếu xuất hiện đường dẫn tệp hoặc ngày in không cần thiết.
4. Điền thông tin còn thiếu trên bản in; đối chiếu dữ liệu, đánh số phụ lục, ký và lưu bản hoàn chỉnh.

Mẫu đã khai báo lề A4, lặp tiêu đề bảng khi sang trang và giữ khối chữ ký liền nhau. Số trang phụ thuộc nội dung; phần kiểm tra trực tiếp bố cục bản in để thực hiện ở đợt kiểm thử sau.

Gợi ý tên bản lưu cuối: `HD_<mã>_<ngày-ký>_DaKy.pdf`, `BG_<mã>_<ngày>_DaKy.pdf`. Đây là quy ước lưu thủ công, không phải tính năng phần mềm tự đặt tên hay tự tải bản ký lên hệ thống.

Không ghi đè bản đã ký bằng bản xuất lại sau khi sửa thông tin khách hoặc gia hạn. Bản xuất mới sử dụng dữ liệu hiện tại, có thể khác dữ liệu tại thời điểm ký cũ.

Mã tạo biểu mẫu: [BieuMauHtml.cs](../QLPT.WPF/Services/BieuMauHtml.cs). Luồng xuất: [HopDongViewModel.cs](../QLPT.WPF/ViewModels/HopDongViewModel.cs).
