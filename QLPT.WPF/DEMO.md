# Kịch bản demo — QLPT (WPF)

Kịch bản đi theo 3 luồng nghiệp vụ, dùng dữ liệu đang có trong DB `KLCN052_QuanLyPhongTro`
(số liệu chụp ngày 10/10/2026). Thời lượng khoảng 15–20 phút.

> **Trước khi demo**
> - Chạy API: `http://localhost:5000` (Swagger: `/swagger`), rồi mở `wpf_sc\QLPT.WPF.exe`.
> - Mọi thao tác trong demo **ghi thật vào DB**, và chức năng Sao lưu hiện chưa khôi phục được.
>   Hãy tập trước, và để bước **không hoàn tác được** (thanh lý hợp đồng) ở cuối.
> - Tài khoản quản trị: `admin` / `Admin@123`.

---

## 0. Mở đầu (2 phút)

| Bước | Thao tác | Điểm cần nói |
|---|---|---|
| 0.1 | Đăng nhập với mật khẩu sai | Thông báo chung, không lộ là sai tên hay sai mật khẩu. Mật khẩu lưu bằng bcrypt. |
| 0.2 | Đăng nhập `admin` / `Admin@123` | Phân quyền: Admin thấy đủ menu, khách thuê chỉ thấy 2 mục. |
| 0.3 | Xem **Dashboard** | 14 phòng (8 đang thuê, 4 trống, 1 đặt cọc, 1 bảo trì), công nợ **30.773.000 đ**. Biểu đồ doanh thu 6 kỳ, biểu đồ vòng tình trạng phòng. Bảng sắp hết hạn có **HĐ#5 – Phòng A203 – Phạm Thị Dung (31/10/2026)**. |
| 0.4 | Bật **chế độ tối** | Toàn bộ giao diện, biểu đồ và dialog đổi màu theo. |
| 0.5 | Gõ `dien nuoc` vào ô tìm kiếm ở sidebar | Tìm menu không cần gõ dấu. |

---

## ① Phòng & thiết bị → Đặt phòng → Hợp đồng → Trả phòng

| Bước | Thao tác | Kết quả mong đợi |
|---|---|---|
| 1.1 | **Đặt phòng** cho **Phòng A104** (đang trống), cọc `2000000` | Ô tiền tự định dạng `2.000.000 đ`. Phòng chuyển *Đã đặt cọc*, **Sổ thu chi** tự có phiếu thu "Thu tiền đặt cọc phòng A104". |
| 1.2 | **Hủy** đặt phòng vừa tạo, chọn hoàn cọc | Phòng trở lại *Trống*, có **phiếu chi** hoàn cọc. |
| 1.3 | *Trường hợp bị từ chối:* đặt một phòng đang thuê | Hệ thống chặn, chỉ cho đặt phòng *Trống*. |
| 1.4 | **Nhận phòng**: tạo hợp đồng từ booking **Phòng B102 – Trương Mỹ Linh** (cọc 2.000.000 đ, hẹn nhận 10/10) | Hệ thống tự làm cùng lúc: phòng sang *Đang thuê*, booking sang *Đã nhận phòng*, **thêm cư trú**, **đăng ký dịch vụ Điện và Nước**, ghi phiếu thu phần cọc còn thiếu. |
| 1.5 | **Bàn giao tài sản**: gán đồ đạc cho phòng B102 | Danh sách tài sản của phòng, sửa và xóa được. |
| 1.6 | *Trường hợp bị từ chối:* **Cư trú**, thêm một khách đang ở hợp đồng khác | Bị chặn với thông báo "đang cư trú ở hợp đồng #…". |
| 1.7 | **Gia hạn HĐ#5** (A203): chọn ngày kết thúc mới, có thể đổi giá thuê | Có bản ghi gia hạn, khách nhận **thông báo**. |

---

## ② Chỉ số điện nước → Tính tiền → Hóa đơn → Thu tiền → Công nợ

| Bước | Thao tác | Kết quả mong đợi |
|---|---|---|
| 2.1 | **Chỉ số điện nước**: ghi chỉ số **tháng 10/2026** cho một phòng (DB đang có đến 9/2026) | Lưu chỉ số cũ và mới của kỳ. |
| 2.2 | **Hóa đơn** → *Tạo tự động* tháng 10 | Mỗi hóa đơn gồm các dòng: tiền phòng; tiền điện (chỉ số A → B kWh × đơn giá); nước tương tự; phí các dịch vụ khác. Không tạo trùng kỳ. |
| 2.3 | *Trường hợp bị từ chối:* tạo hóa đơn cho phòng **chưa ghi chỉ số** | Báo thiếu chỉ số điện/nước của kỳ. |
| 2.4 | **Thu tiền** hóa đơn của **HĐ#2 – Phòng A102** (nợ 3.090.000 đ): thu trước `1000000` | Hóa đơn sang *Thanh toán một phần*, Sổ thu chi có phiếu thu, **công nợ trên Dashboard giảm**. |
| 2.5 | **Báo cáo**: xem các tab | Biểu đồ doanh thu 12 kỳ, biểu đồ **công nợ theo phòng**, tab *Doanh thu theo kỳ* (bảng số liệu đi kèm biểu đồ). |
| 2.6 | **Xuất Excel** rồi mở file | File có 4 sheet. Số tiền là số thật (lọc và sắp xếp được), dòng tổng dùng công thức `SUM`. |

---

## ③ Vi phạm → Xử lý → Phản hồi / Thông báo → Nhắc nhở

| Bước | Thao tác | Kết quả mong đợi |
|---|---|---|
| 3.1 | **Vi phạm**: có 2 vụ *Chưa xử lý*. Bấm xử lý → *Phạt tiền*, tick gửi thông báo | Vi phạm sang *Đã xử lý*, khách nhận thông báo. |
| 3.2 | **Thông báo** → **Nhắc nhở** | Tự gửi nhắc nợ cho khách đang nợ và nhắc HĐ#5 sắp hết hạn. Bấm lần 2 trong cùng ngày thì **không gửi trùng**. |
| 3.3 | **Tài khoản** → tạo tài khoản *Customer* cho **Trương Mỹ Linh (mã khách 11)**, là người vừa nhận phòng ở bước 1.4 | Mỗi khách chỉ có một tài khoản, mật khẩu được mã hóa. |
| 3.4 | Đăng xuất → đăng nhập bằng tài khoản vừa tạo | Menu chỉ còn **Thông tin của tôi** (hợp đồng, hóa đơn) và **Thông báo**. |
| 3.5 | Đọc một thông báo → **Phản hồi** | Phản hồi được gửi về cho admin. |
| 3.6 | Đăng nhập lại `admin` → **Thông báo** | Cột trạng thái hiện "Đã đọc x/y · z phản hồi", bấm vào để xem nội dung phản hồi. |
| 3.7 | **Sửa chữa**: yêu cầu *Chờ tiếp nhận* → *Đang sửa* → *Đã hoàn thành*, nhập chi phí | Trạng thái và chi phí được cập nhật. |

---

## Kết: Báo trả & thanh lý (làm cuối vì không hoàn tác được)

| Bước | Thao tác | Kết quả mong đợi |
|---|---|---|
| 4.1 | **Báo trả phòng** cho HĐ#2 (A102) | Ghi nhận yêu cầu trả phòng. |
| 4.2 | **Thanh lý HĐ#2**. Sau bước 2.4, HĐ#2 còn nợ 2.090.000 đ, cọc 2.600.000 đ | Cọc được **cấn trừ vào hóa đơn còn nợ** (từ cũ đến mới), phần dư **510.000 đ hoàn lại qua phiếu chi**. Hợp đồng *Đã thanh lý*, phòng *Trống*, cư trú và dịch vụ kết thúc, báo trả *Đã hoàn tất*, khách nhận thông báo. |

---

## Dữ liệu tham khảo (10/10/2026)

**Phòng trống:** A104 (3.800.000 đ), A202 (4.500.000 đ), B201 (3.200.000 đ), C101 (2.400.000 đ).

**Hợp đồng đang nợ:**

| Hợp đồng | Phòng | Tiền cọc | Còn nợ |
|---|---|---:|---:|
| HĐ#2 | A102 | 2.600.000 đ | 3.090.000 đ |
| HĐ#3 | A103 | 3.800.000 đ | 6.455.000 đ |
| HĐ#5 | A203 | 3.000.000 đ | 7.050.000 đ |
| HĐ#6 | B101 | 2.700.000 đ | 3.318.000 đ |
| HĐ#7 | B202 | 5.800.000 đ | 6.487.500 đ |
| HĐ#8 | C102 | 3.600.000 đ | 4.372.500 đ |

**Khách chưa có tài khoản** (dùng cho bước 3.3): #9 Ngô Thanh Mai, #10 Lý Quốc Khánh,
#11 Trương Mỹ Linh, #12 Nguyễn Thị Hoa.

---

## Những điểm nên tránh hoặc chuẩn bị câu trả lời

- **Sao lưu / khôi phục chưa làm thật.** Dữ liệu sao lưu chỉ nằm trong bộ nhớ của API. Không demo, hoặc nói rõ là phạm vi chưa làm.
- **Sửa tài khoản chưa đổi được mật khẩu**, chỉ đổi được vai trò. Demo vai khách thì tạo tài khoản mới (bước 3.3).
- **Chưa có xóa mềm khách thuê**, vì cần thêm cột vào DB.
- **Phòng chưa đặt số người tối đa**, nên không demo được trường hợp phòng đầy.
- **API GET chưa yêu cầu đăng nhập**, ai biết địa chỉ API cũng đọc được dữ liệu. Nên có câu trả lời sẵn nếu bị hỏi về bảo mật.
