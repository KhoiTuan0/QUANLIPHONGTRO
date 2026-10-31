# Tích hợp yêu cầu hủy và tiền phạt — 29/10/2026

Phạm vi đã sửa: API, WPF, SQL; **không sửa mobile**. Client giữ cookie đăng nhập `QLPT.Session` như hiện tại. Server xác định khách/quản lý từ phiên đăng nhập, không dùng mã khách hay vai trò client tự gửi.

Hai SQL mobile được gộp trong `migrations/20261029_huy_dat_phong_va_phat_hoa_don.sql`, chạy lại được. `themrangbuoc (1).sql` trùng file `themrangbuoc.sql` cũ, không cần chạy cả hai. Không chạy lại nguyên bản script CREATE TABLE yêu cầu hủy sau migration. Dùng sqlcmd `-b -f 65001` để giữ dấu tiếng Việt.

## Yêu cầu hủy đặt phòng

**POST `/api/DatPhong/yeu-cau-huy`**

```json
{ "maDatPhong": 12, "lyDo": "Thay đổi kế hoạch chuyển chỗ ở" }
```

Customer chỉ gửi cho phiếu của mình; Admin/Manager đúng cơ sở có thể tiếp nhận thay khách. Phiếu phải đang `Đã đặt cọc`, chưa có yêu cầu `Chờ duyệt`. Lý do bắt buộc, tối đa 500 ký tự. Server tự ghi thời gian, trạng thái và tài khoản tạo. Kết quả 200 có `success`, `maYeuCau`, `message`.

Gửi yêu cầu chưa nhả phòng và chưa hoàn tiền. Số ngày báo trước tính từ ngày gửi đến ngày dự kiến nhận; có thể âm hoặc null nếu thiếu ngày nhận. Không tự áp chính sách phần trăm hoàn cọc.

**GET `/api/DatPhong/yeu-cau-huy`**

Trả mảng trong phạm vi đăng nhập: khách chỉ của mình, quản lý chỉ cơ sở được giao, chủ trọ toàn bộ. Các trường: `maYeuCau`, `maDatPhong`, `tenPhong`, `tenKhach`, `ngayYeuCau`, `lyDo`, `trangThai`, `tienCoc`, `ngayDuKienNhan`, `soNgayBaoTruoc`, `ngayXuLy`, `soTienHoan`, `tyLeHoan`, `ghiChuXuLy`, `maTaiKhoanYeuCau`, `maTaiKhoanXuLy`.

**PUT `/api/DatPhong/yeu-cau-huy/{maYeuCau}/xu-ly`** — chỉ Admin/Manager đúng cơ sở.

```json
{ "duyet": true, "soTienHoan": 600000, "ghiChuXuLy": "Hai bên thống nhất hoàn một phần" }
```

- Duyệt phải nhập rõ số tiền hoàn, từ 0 đến tiền cọc, tối đa 2 chữ số thập phân. Ghi chú quyết định bắt buộc (tối đa 500 ký tự).
- Duyệt cập nhật yêu cầu `Đã duyệt`, phiếu đặt `Đã hủy`, nhả phòng hợp lệ và ghi phiếu chi nếu tiền hoàn > 0, cùng một giao dịch. Chỉ xác nhận khi thực hiện hoàn tiền; phần mềm không chuyển khoản ngân hàng.
- Từ chối dùng `duyet=false`, `soTienHoan=0`, ghi rõ lý do; giữ nguyên phiếu đặt. Khách có thể gửi yêu cầu mới sau đó, lịch sử cũ vẫn còn.
- Không duyệt lại hoặc hoàn tiền lần hai; khách không được duyệt. Tỷ lệ hoàn server tính từ tiền thực hoàn / tiền cọc.
- Kết quả được gửi thành thông báo trong hệ thống nếu khách có tài khoản; chưa khẳng định có push notification thời gian thực.

WPF: **Đặt phòng → Yêu cầu hủy** để xem/xử lý. Muốn tiếp nhận trực tiếp, chọn phiếu đặt trước khi mở. Khi có yêu cầu chờ, Hủy giữ chỗ trực tiếp và Nhận phòng đều yêu cầu xử lý yêu cầu đó trước.

## Đưa tiền phạt vào hóa đơn

WPF chủ trọ: **Vi phạm → xử lý chốt mức phạt → chọn dòng → Đưa phạt vào hóa đơn → chọn hóa đơn → xác nhận**. Nếu chưa có hóa đơn phù hợp, lập hóa đơn trước. Không tự gộp tất cả vi phạm khi lập hóa đơn.

**GET `/api/ViPham/{maViPham}/hoa-don-phu-hop`** — Admin; trả hóa đơn kèm tổng hiện tại, tiền phạt và tổng sau bổ sung.

**POST `/api/ViPham/{maViPham}/hoa-don`** — Admin.

```json
{ "maHoaDon": 25 }
```

Vi phạm phải đã xử lý, tiền phạt dương và chưa gắn hóa đơn. Hóa đơn phải chưa có bất kỳ thanh toán nào, đúng phòng/khách đại diện, hợp đồng còn hiệu lực. Ngày vi phạm nằm trong thời hạn hợp đồng; kỳ hóa đơn không trước tháng vi phạm. Phạt người ở cùng không phải khách đại diện không tự quy vào hóa đơn người khác.

Server thêm dòng `ChiTietHoaDon.MaViPham`, cập nhật tổng/công nợ và `ViPham.DuocTinhHoaDon=true` cùng giao dịch. Trong phiên bản này cờ nghĩa là **đã đưa vào hóa đơn**. Unique index chống cùng khoản phạt vào hai hóa đơn. Không tự ghi phiếu thu; thu tiền theo luồng hóa đơn có sẵn. Hóa đơn đã thu dù một phần không được bổ sung phạt.

Mobile chỉ đọc hóa đơn bằng endpoint hiện tại. Chi tiết có `noiDung`, `soLuong`, `donGia`, `thanhTien` và `maViPham` khi là khoản phạt. **Không cộng phạt thêm ở client vì `tongTien` đã bao gồm.** Quyền sở hữu hóa đơn vẫn áp dụng.

## Lỗi và cập nhật phiên bản

400: sai dữ liệu/điều kiện; 401/403: đăng nhập/quyền; 404: không tồn tại; 409: trùng hoặc đã xử lý. Hiển thị `message`, tải lại danh sách khi 409, không tự gửi lặp thao tác thu/hoàn tiền.

Áp dụng migration trước khi chạy API/WPF mới. Không scaffold ghi đè các model/controller đã chỉnh. Kiểm tra có mục tiêu trên CSDL riêng và rollback dữ liệu thử đã thực hiện; kiểm tra giao diện WPF và tích hợp mobile thực tế là bước riêng.
