# Bản SQL chốt mới nhất — xác nhận 31/10/2026

**File giao:** `KLCN052_QuanLyPhongTro_CHOT_20261031.sql`. Bản ngày 30/10 là ảnh chụp cũ, giữ lại để đối chiếu.

Nguồn: **`.\SQLEXPRESS → KLCN052_QuanLyPhongTro`**, CSDL chính đang dùng, chụp ngày 31/10 sau khi thêm lịch sử sửa thông tin khách. File xuất từ bản sao phục hồi `QLPT_Export_20261031`, không lấy dữ liệu thử từ `QLPT_DemoPrep_20261019`.

## Đã bao gồm

- Toàn bộ 35 bảng và 202 dòng dữ liệu tại thời điểm chụp, bao gồm tài khoản, cơ sở, phòng, khách, hợp đồng, hóa đơn, tài sản riêng lẻ, lịch sử và sơ đồ CSDL.
- 48 khóa ngoại, 7 CHECK constraint, 52 chỉ mục; các cột tính toán, mặc định, khóa chính/unique và thủ tục/hàm sơ đồ.
- Bảng `LichSuKhachThue` ghi trường thay đổi, giá trị cũ/mới, thời điểm và tài khoản sửa. Bảng đang trống vì chưa sửa khách sau khi áp dụng migration.
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

File SQL ngày 31/10 đã chạy thành công trên CSDL mới riêng `QLPT_Verify_20261031`. So sánh hai chiều từng bảng bằng `EXCEPT` với bản sao nguồn: **0 bảng khác dữ liệu**, 202/202 dòng khớp. Số khóa ngoại, CHECK và chỉ mục khớp (48/7/52). Đây là kiểm tra riêng cho file SQL, không thay cho nghiệm thu giao diện WPF/mobile.

API/WPF phải chạy mã nguồn mới đã build sau migration để thấy hai nghiệp vụ. Endpoint và thao tác: [API_MOBILE_20261029.md](../API_MOBILE_20261029.md). Không đổi chuỗi kết nối API/WPF sang các database kiểm tra.

File chứa dữ liệu khách và mật khẩu băm của tài khoản hiện tại; giao riêng cho thành viên/người nhận có quyền, không đưa vào kho công khai. File full đã được loại khỏi Git bằng `.gitignore`; migration và tài liệu vẫn có thể quản lý phiên bản bình thường.
