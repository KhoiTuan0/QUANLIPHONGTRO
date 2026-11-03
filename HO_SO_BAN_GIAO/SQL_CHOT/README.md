# Bản SQL chốt mới nhất — 03/11/2026, dữ liệu sạch diễn tập

**File giao:** `KLCN052_QuanLyPhongTro_CHOT_20261103.sql`.
**Backup nền:** `KLCN052_QuanLyPhongTro_DEMO_SACH_20261103.bak`.
Các file ngày 30–31/10 là bản cũ; không dùng thay cho hai file mới này.

Nguồn: **`.\SQLEXPRESS → KLCN052_QuanLyPhongTro`**, đã nạp dữ liệu sạch chung cho Mobile/API/WPF ngày 03/11. SQL xuất từ bản staging sạch `QLPT_Export_20261103`, đã đối chiếu với dữ liệu chính. Tài khoản, mật khẩu, vai trò và liên kết mã khách được giữ; hai tên hồ sơ thử được chuẩn hóa thành Nguyễn Đức Huy và Trần Minh Quang.

## Đã bao gồm

- Toàn bộ 35 bảng và 340 dòng: 2 cơ sở, 11 phòng, 11 khách, 8 hợp đồng, 11 hóa đơn, 34 hiện vật, 7 tài khoản cùng dữ liệu nghiệp vụ/lịch sử và sơ đồ CSDL.
- 48 khóa ngoại, 7 CHECK constraint, 52 chỉ mục; các cột tính toán, mặc định, khóa chính/unique và thủ tục/hàm sơ đồ.
- Có lịch sử thay đổi khách; lịch sử hiện vật gồm nhập kho, điều chuyển, kiểm kê, sửa chữa và thanh lý.
- `ViPham.DuocTinhHoaDon`, `ChiTietHoaDon.MaViPham`, khóa ngoại và unique index chống tính phạt trùng.
- Bảng `YeuCauHuyDatPhong`, ràng buộc trạng thái, chỉ mục một yêu cầu chờ cho mỗi phiếu, thông tin tài khoản tạo/xử lý và giới hạn số tiền/tỷ lệ.
- Các cập nhật cơ sở, quản lý, tài sản, cư trú và khách tiếp nhận đã có trước đó trong CSDL chính.

## Dùng file nào

- **Máy hiện tại:** CSDL chính đã được cập nhật. Không cần chạy lại file SQL đầy đủ hoặc hai file mobile nguyên bản.
- **Máy mới / tạo CSDL từ đầu:** dùng file SQL chốt này. Script tạo CSDL `KLCN052_QuanLyPhongTro`, không cố định đường dẫn MDF/LDF của máy nguồn.
- **Máy đang có dữ liệu cũ cần nâng cấp:** dùng migration phù hợp, có sao lưu trước; không lấy file full để ghi đè dữ liệu đang dùng. Các migration mới gồm `migrations/20261029_huy_dat_phong_va_phat_hoa_don.sql` và `migrations/20261031_lich_su_sua_khach.sql`.

File full chủ động dừng khi database đích đã tồn tại để tránh ghi đè. Nếu muốn phục hồi cạnh bản đang có, đổi nhất quán tên database ở phần đầu script sang tên mới; không xóa bản chính chỉ để chạy file này. Trong SSMS, sau khi gặp chặn `NOEXEC`, mở cửa sổ truy vấn mới trước khi chạy thao tác khác.

Mở bằng SSMS, dùng SQL Server 2022 hoặc phiên bản tương thích; nếu chạy sqlcmd thêm `-b -f 65001`. Tài khoản thực thi cần quyền tạo CSDL. Server login/quyền hệ điều hành của máy nguồn không được sao chép; cấp quyền truy cập CSDL cho tài khoản chạy API trên máy đích.

## Đối chiếu đã thực hiện

SQL ngày 03/11 đã tạo thành công CSDL kiểm tra `QLPT_Test_20261103_c60d71e9`. Đối chiếu hai chiều cả 35 bảng bằng `EXCEPT`: **0 bảng khác dữ liệu** trước khi thêm tài khoản thử vào bản kiểm tra. `DBCC CHECKDB`, `CHECKCONSTRAINTS`, các phép kiểm tổng hóa đơn/thanh toán, tồn kho, người ở, tính phạt và tài khoản đều đạt. WPF qua 26 kiểm tra với bản phục hồi này. Backup sạch đã `RESTORE VERIFYONLY` có CHECKSUM.

SHA256 SQL: `1A27BE8E02970AE808D4A5B24C1E5F230A0EF7C34A66C96FCC3CF68A991DF66B`.
SHA256 BAK: `00081B90FA90C865E3BF04FDBFB98EF8B38FC33963FB3005F3A429388082C004`.

Chọn hồ sơ để diễn tập: [CHOT_DIEN_TAP_20261103.md](../CHOT_DIEN_TAP_20261103.md).

API/WPF phải chạy mã nguồn mới đã build sau migration để thấy hai nghiệp vụ. Endpoint và thao tác: [API_MOBILE_20261029.md](../API_MOBILE_20261029.md). Không đổi chuỗi kết nối API/WPF sang các database kiểm tra.

File chứa dữ liệu khách và mật khẩu băm của tài khoản hiện tại; giao riêng cho thành viên/người nhận có quyền, không đưa vào kho công khai. File full đã được loại khỏi Git bằng `.gitignore`; migration và tài liệu vẫn có thể quản lý phiên bản bình thường.
