# Bản diễn tập đã chốt — 03/11/2026

Code đã hợp vào nhánh `khoi/winforms-net8-final` từ `codex/integration-final`.
Tên nhánh là tên cũ; sản phẩm Desktop hiện tại là WPF. Không sửa mã WinForms.
Các chỉnh sửa Mobile cũ chưa commit ở Desktop được cất nguyên trạng trong stash
`Desktop mobile before integration merge 20261103`; không áp lại lên bản mới.

## Dữ liệu đang chạy

- SQL Server: `.\SQLEXPRESS`; database: `KLCN052_QuanLyPhongTro`.
- API: `http://localhost:5180`; health đã báo `database: connected` sau cập nhật.
- 35 bảng/340 dòng; 11 phòng: 6 đang thuê, 3 trống, 1 giữ chỗ, 1 bảo trì.
- 11 khách, 8 hợp đồng, 11 hóa đơn; tổng hóa đơn 33.735.000 đ, đã thu
  21.900.000 đ, còn nợ 11.835.000 đ. Tiền cọc và tiền mua tài sản là nghiệp vụ riêng.
- 34 hiện vật: 16 trong phòng, 17 trong kho, 1 đã thanh lý; mỗi chiếc có mã riêng.
- 7 tài khoản giữ nguyên mật khẩu/vai trò/mã khách. `minhanh` gắn Nguyễn Minh Anh
  (#1), `thuylinh` gắn Phạm Thùy Linh (#3); tài khoản quản lý/cơ sở được bảo toàn.
- Mobile và WPF đọc cùng dữ liệu khi gọi cùng API. Máy Toàn phải phục hồi đúng
  SQL/.bak mới; dữ liệu hai máy không tự đồng bộ khi dùng hai SQL Server riêng.

## Chọn sẵn hồ sơ theo thứ tự diễn tập

| Nội dung | Hồ sơ đã có |
| --- | --- |
| Tổng quan | Dashboard: 11 phòng, 6 đang thuê; đổi 3/6/12 tháng và số ngày sắp hết hạn |
| Phân quyền | `admin` toàn hệ thống; `huy` Quận 7; `huy04` Thủ Đức; `huy01` sao lưu |
| Đặt mới/tạo hợp đồng | B202 Thủ Đức (#9) trống, phiếu giữ chỗ cũ đã hủy; Huỳnh Nhật Minh (#9) không đang cư trú |
| CRUD thêm/xóa | Tạo phòng mới A104 ở Quận 7, chưa phát sinh nghiệp vụ rồi thử xóa; phòng có lịch sử được giữ để tra cứu |
| Phòng trống có lịch sử | A103 Quận 7 (#3), B102 Thủ Đức (#7), để giải thích giữ lịch sử thay vì xóa |
| Hủy/hoàn cọc | Phiếu đặt #4, A105, Nguyễn Đức Huy; yêu cầu hủy #1 chờ duyệt, cọc 500.000 đ |
| Hủy đã xử lý | Phiếu #5 B102, Nguyễn Khánh Vy; yêu cầu #2 đã duyệt, hoàn 300.000 đ |
| Người đại diện khác người ở | Hợp đồng #7 B203: Đỗ Gia Bảo đứng tên, Trần Minh Quang cư trú; Bảo đang ở A202 theo hợp đồng #8 |
| Sắp hết hạn/gia hạn | Hợp đồng #3 A201, Phạm Thùy Linh, hết hạn 10/11/2026; có yêu cầu gia hạn chờ duyệt |
| Báo trả/thanh lý | Hợp đồng #2 A102, Trần Quốc Huy có báo trả chờ xử lý; chỉ thanh lý sau đối chiếu công nợ/cọc |
| Cư trú | Lê Hoàng Nam, B101 có hồ sơ chờ duyệt; Trần Minh Quang B203 cần bổ sung; Minh Anh đã xác nhận |
| Điện nước/OCR | Có dữ liệu tháng 9–11; để nhập mới kỳ 11 chọn A201 hoặc A102 chưa có chỉ số kỳ 11; OCR luôn đối chiếu ảnh trước khi lưu |
| Hóa đơn/thu một phần | Hóa đơn #4 A102: còn 2.700.000 đ; #2 A101 còn 1.420.000 đ; không thu vượt dư nợ |
| Chi tiết/PDF đã thu đủ | Hóa đơn #11 B203: 3.030.000 đ, đã thu đủ; người đại diện Đỗ Gia Bảo |
| Vi phạm | #1 A102 chưa xử lý; #2 B101 đã xử lý và đã tính một lần ở hóa đơn #8 |
| Tài sản | Giường gỗ: 6 kho/6 trong phòng; tủ: 5/5; máy lạnh: 3/3; bình nóng lạnh: 3 kho/2 phòng/1 thanh lý |
| Hiện vật có lịch sử sửa | TS00000005, máy lạnh B101, sửa 250.000 đ; quản lý Thủ Đức xem/kiểm kê được |
| Nhập kho/điều chuyển | Admin thao tác trên phòng trống B202; nhập ít nhất có thể khi diễn tập; không ghi nhận mới tại phòng để thay cho xuất kho |
| Xuất hợp đồng/bàn giao | Hợp đồng #1 A101 hoặc #7 B203 đã có biên bản nhận phòng |
| Báo cáo/sao lưu | Chọn tháng 10/11 năm 2026; có thu một phần và công nợ, backup sạch đã có |

Các thao tác duyệt hủy, thu tiền, nhập kho, tạo hợp đồng, thanh lý sẽ thay dữ liệu.
Sau diễn tập, phục hồi `.bak` nền sạch theo quy trình; không cần thiết kế lại bộ
dữ liệu hoặc tạo lại SQL chốt mỗi lần tập. Sau khi phục hồi phải đăng nhập lại.

## File gửi Toàn và file dự phòng

Trong thư mục Desktop `QUANLIPHONGTRO\HO_SO_BAN_GIAO\SQL_CHOT`:

1. `KLCN052_QuanLyPhongTro_CHOT_20261103.sql` — đầy đủ schema và dữ liệu.
2. `KLCN052_QuanLyPhongTro_DEMO_SACH_20261103.bak` — nền sạch để phục hồi nhanh.

Giữ kín hai file vì có mật khẩu băm tài khoản. Không chạy lại `01_Schema.sql`,
`02_SampleData.sql`, SQL mẫu cũ hoặc các migration đã áp dụng lên database sạch.
File SQL mới dừng nếu database đích tồn tại; máy Toàn cần backup bản cũ rồi
phục hồi có kiểm soát/đổi tên CSDL và cấu hình API tương ứng, không chạy chồng.

## Phục hồi dữ liệu trước đợt dọn này

Hai bản sao trước khi dọn vẫn ở thư mục Backup của SQL Server:
`QLPT_before_clean_20261103_9c3a7e.bak` và
`QLPT_before_switch_20261103_9c3a7e.bak`. Cả hai đã VERIFYONLY.
Dữ liệu thử được thay trong CSDL chính nhưng có thể phục hồi từ các bản này.
