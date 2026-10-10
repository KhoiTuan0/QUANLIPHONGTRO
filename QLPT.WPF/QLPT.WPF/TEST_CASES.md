# Test case — QLPT (WPF)

Test case đi theo thứ tự menu của ứng dụng. Kết quả mong đợi và câu thông báo lỗi lấy từ code hiện tại
(kiểm tra trong dialog WPF và kiểm tra ở API).

**Quy ước**
- **Loại**: ✅ luồng hợp lệ · ⛔ dữ liệu sai hoặc bị nghiệp vụ chặn · 🎨 giao diện.
- **KQ**: người test điền `Đạt` / `Lỗi` sau khi chạy.
- Dữ liệu mẫu theo DB ngày 10/10/2026 (xem [DEMO.md](DEMO.md)). Mã phòng/mã khách xem ở trang tương ứng.
- Các bước có ghi dữ liệu sẽ ghi thật vào DB. Nên chạy nhóm ⛔ trước (không làm đổi dữ liệu),
  và để các bước không hoàn tác được (thanh lý, xóa) ở cuối.

---

## Công thức nghiệp vụ

Các công thức dưới đây lấy từ code hiện tại (API và WPF). Kết quả mong đợi trong các test case được tính theo đúng các công thức này.

### A. Trạng thái

| Đối tượng | Các trạng thái | Quy tắc |
|---|---|---|
| **Phòng** | Trống · Đã đặt cọc · Đang thuê · Bảo trì | *Đang thuê* ⇔ có đúng 1 hợp đồng *Đang hiệu lực*. *Đã đặt cọc* ⇔ có phiếu đặt *Đã đặt cọc* và chưa có HĐ |
| **Phiếu đặt phòng** | Đã đặt cọc → Đã nhận phòng / Đã hủy | Chỉ đặt được phòng *Trống*; chỉ hủy được phiếu *Đã đặt cọc* |
| **Hợp đồng** | Đang hiệu lực → Đã thanh lý | Mỗi phòng tối đa 1 HĐ hiệu lực; mỗi HĐ chỉ thanh lý 1 lần |
| **Cư trú** | Đang ở · Tạm vắng → Đã rời đi | Một người chỉ *đang ở* (Đang ở / Tạm vắng) tại 1 HĐ hiệu lực. Rời đi thì `Ngày rời ≥ Ngày vào` |
| **Dịch vụ đăng ký** | Đang sử dụng → Đã kết thúc | HĐ đã thanh lý thì không còn dịch vụ đang dùng |
| **Hóa đơn** | Chưa thanh toán · Thanh toán một phần · Đã thanh toán | `Còn lại ≤ 0` → *Đã thanh toán*; `Đã thu > 0` và `Còn lại > 0` → *Một phần*; `Đã thu = 0` → *Chưa thanh toán*. Mỗi HĐ chỉ có 1 hóa đơn cho mỗi kỳ (tháng/năm) |
| **Vi phạm** | Chưa xử lý → Đã xử lý | Chỉ xử lý được 1 lần |
| **Sửa chữa** | Chờ tiếp nhận → Đang sửa → Đã hoàn thành | *Đã hoàn thành* phải có ngày hoàn thành |

### B. Dashboard & Báo cáo

| Chỉ tiêu | Công thức |
|---|---|
| Tổng phòng / Phòng trống / Đang thuê | Đếm phòng theo trạng thái |
| **Khách đang ở** | Số người **khác nhau** trong: { cư trú *Đang ở* / *Tạm vắng* của HĐ hiệu lực } ∪ { người đại diện của HĐ hiệu lực } |
| Hồ sơ khách thuê | Đếm toàn bộ khách thuê (kể cả khách cũ, khách mới chỉ đặt cọc) |
| HĐ hiệu lực | Đếm HĐ *Đang hiệu lực* |
| HĐ sắp hết hạn | HĐ hiệu lực có `Ngày kết thúc ≤ hôm nay + 30 ngày` |
| **Công nợ chưa thu** | `Σ Còn lại` của mọi hóa đơn có trạng thái ≠ *Đã thanh toán* (gồm cả hóa đơn của HĐ đã thanh lý mà khách còn nợ) |
| **Doanh thu tháng này** | `Σ số tiền các lần thanh toán hóa đơn` có ngày thanh toán thuộc tháng hiện tại, **gồm cả** khoản *cấn trừ tiền cọc* khi thanh lý |
| Biểu đồ doanh thu theo kỳ | Theo kỳ hóa đơn (tháng/năm): `Tổng phát hành = Σ Tổng tiền`, `Đã thu = Σ Đã thanh toán`, `Còn nợ = Σ Còn lại`, `Tỷ lệ thu = Đã thu / Tổng phát hành`. Dashboard 6 kỳ, Báo cáo 12 kỳ gần nhất; kỳ không có hóa đơn hiện 0 |
| Biểu đồ tình trạng phòng | `% = số phòng của trạng thái / tổng phòng` |
| Biểu đồ công nợ theo phòng | `Σ Còn lại` của hóa đơn còn nợ, gộp theo phòng, nhiều nhất ở trên; quá 8 phòng thì 7 phòng đầu + "Khác (n phòng)" |
| Hóa đơn quá hạn (Báo cáo) | `Hạn thanh toán < hôm nay` → "Quá hạn N ngày", `N = hôm nay − hạn` |
| Còn lại (ngày) của HĐ sắp hết hạn | `Ngày kết thúc − hôm nay` |

### C. Lập hóa đơn tự động (kỳ tháng *t*/năm *n*)

```
Tiền phòng   = Giá thuê/tháng trên hợp đồng (chưa có thì lấy giá phòng)          × 1
Tiền điện    = max(0, Chỉ số mới − Chỉ số cũ) của kỳ  ×  Đơn giá điện
Tiền nước    = max(0, Chỉ số mới − Chỉ số cũ) của kỳ  ×  Đơn giá nước
Phí dịch vụ  = Đơn giá áp dụng của từng dịch vụ đang dùng (trừ điện, nước)        × 1
Tổng tiền    = Σ thành tiền các dòng            (mỗi dòng làm tròn đến đồng)
Hạn thanh toán = Ngày lập + 7 ngày ;  Đã thu = 0 ;  Còn lại = Tổng tiền
```

- **Đơn giá điện / nước:** dùng đơn giá áp dụng lúc đăng ký dịch vụ; chưa có thì lấy đơn giá trong bảng giá; vẫn chưa có thì dùng mặc định **3.500 đ/kWh** và **20.000 đ/m³**.
- **Bị chặn khi:** HĐ đã đăng ký điện/nước mà chưa ghi chỉ số của kỳ; HĐ không còn hiệu lực; kỳ đã có hóa đơn.
- **Lập tay:** `Tổng tiền > 0` do người dùng nhập.

### D. Tiền & Sổ thu chi

| Nghiệp vụ | Công thức | Ghi vào Sổ thu chi |
|---|---|---|
| **Đặt phòng** (cọc C) | `0 ≤ C` | Phiếu **thu** C — "Thu tiền đặt cọc phòng …" |
| **Hủy giữ chỗ** (hoàn R) | `0 ≤ R ≤ C` | Phiếu **chi** R — "Hoàn tiền đặt cọc phòng … (khách hủy)" |
| **Nhận phòng** (cọc HĐ = H, đã cọc lúc đặt = C) | `Phải thu thêm = H − C` (không có phiếu đặt thì C = 0) | Phiếu **thu** `H − C` nếu > 0 |
| **Thu tiền hóa đơn** (số tiền X) | `0 < X ≤ Còn lại`; `Đã thu += X`; `Còn lại = Tổng − Đã thu`; trạng thái theo mục A | Phiếu **thu** X — "Thu tiền hóa đơn tháng t/n - Phòng …" |
| **Xử lý vi phạm** (phạt P) | `P ≥ 0` | Phiếu **thu** P nếu > 0 — "Thu tiền phạt vi phạm - Phòng …" |
| **Hoàn thành sửa chữa** (chi phí K) | `K ≥ 0`; chỉ ghi ở lần đầu chuyển sang *Đã hoàn thành* | Phiếu **chi** K nếu > 0 — "… (yêu cầu sửa chữa số N)" |
| **Gia hạn** | `Ngày kết thúc mới > Ngày kết thúc hiện tại` và `> hôm nay`; giá mới (nếu có) `> 0` thay giá thuê trên HĐ cho các kỳ sau | — |

### E. Thanh lý hợp đồng (trả phòng)

Đầu vào: **C** = tiền cọc đem quyết toán (`0 ≤ C ≤ tiền cọc HĐ`, mặc định = toàn bộ cọc), **F** = tiền phạt (`≥ 0`).
Server tự tính **N** = `Σ Còn lại` các hóa đơn còn nợ của HĐ (không lấy số client gửi).

```
1. Cấn trừ nợ :  lấy cọc trả các hóa đơn còn nợ, hóa đơn CŨ trước
                 Cấn trừ = min(C, N)      → ghi thanh toán "Cấn trừ tiền cọc" (tính vào doanh thu)
2. Trừ phạt   :  Phạt trừ cọc = min(C − Cấn trừ, F)
3. Hoàn khách :  Hoàn lại = C − Cấn trừ − Phạt trừ cọc   (≥ 0)  → phiếu CHI nếu > 0
4. Phạt thiếu :  F − Phạt trừ cọc  → cộng vào hóa đơn gần nhất của HĐ (dòng "Phạt khi thanh lý…")
                 để nằm trong công nợ
Tổng quyết toán = C − N − F      ( ≥ 0: số hoàn lại khách ;  < 0: số khách còn nợ )
Bản ghi thanh lý lưu: Tiền nợ = N, Tiền phạt = F, Tiền hoàn cọc = Hoàn lại, Tổng quyết toán
```

Sau đó: HĐ → *Đã thanh lý*, phòng → *Trống*, cư trú *Đang ở / Tạm vắng* → *Đã rời đi* (ngày rời = hôm nay), dịch vụ → *Đã kết thúc*, báo trả *Chờ xử lý* → *Đã hoàn tất*, khách nhận thông báo quyết toán.

> Ví dụ: cọc 2.600.000, nợ 2.090.000, phạt 0 → cấn trừ 2.090.000, hoàn lại **510.000**.
> Cọc 5.800.000, nợ 6.487.500, phạt 500.000 → cấn trừ 5.800.000, hoàn 0, phạt thiếu 500.000 cộng vào hóa đơn → khách còn nợ **1.187.500**.

### F. Giới hạn & kiểm tra khác

| Mục | Quy tắc |
|---|---|
| Cư trú | Số người *Đang ở / Tạm vắng* của HĐ `< Số người tối đa` của loại phòng (nếu loại phòng có đặt) |
| Nhắc nhở | Gửi cho khách có tài khoản: hóa đơn còn nợ của HĐ hiệu lực **và/hoặc** HĐ hết hạn trong 30 ngày; mỗi tài khoản tối đa 1 lần nhắc / ngày |
| Thông báo "gửi tất cả" | Chỉ gửi tới tài khoản vai trò *Khách thuê* |
| Mật khẩu | ≥ 6 ký tự, lưu dạng mã hóa bcrypt |
| Mã (phòng, khách, HĐ…) | Số nguyên dương |
| Kỳ | `yyyy-MM`, tháng 01–12, không được là kỳ chưa tới |
| CCCD / SĐT / Email | 12 chữ số / 10 chữ số bắt đầu bằng 0 / dạng `ten@mien.xx` |

---

## 0. Đăng nhập · Đăng ký · Đăng xuất

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| DN-01 | Bỏ trống | Để trống tên hoặc mật khẩu → Đăng nhập | "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu." | ⛔ | |
| DN-02 | Sai mật khẩu | `admin` / `sai123` | "Tên đăng nhập hoặc mật khẩu không đúng." | ⛔ | |
| DN-03 | Sai tên đăng nhập | `khongton` / `Admin@123` | **Cùng** thông báo với DN-02, không lộ là tên hay mật khẩu sai | ⛔ | |
| DN-04 | Admin đăng nhập | `admin` / `Admin@123` | Vào Dashboard, sidebar đủ 5 nhóm menu | ✅ | |
| DN-05 | Khách thuê đăng nhập | Tài khoản Customer có mã khách | Menu chỉ có **Thông tin của tôi** và **Thông báo** | ✅ | |
| DN-06 | Sai địa chỉ API | Sửa ô API URL thành cổng sai → Đăng nhập | "Không kết nối được máy chủ (…). Kiểm tra API đã chạy và đúng địa chỉ." App không bị treo | ⛔ | |
| DN-07 | Đăng xuất | Bấm Đăng xuất | Quay về màn hình đăng nhập, phiên cũ bị xóa | ✅ | |
| DK-01 | Đăng ký thiếu thông tin | Bỏ trống tên đăng nhập, mật khẩu hoặc họ tên | "Vui lòng nhập Tên đăng nhập, Mật khẩu và Họ tên." | ⛔ | |
| DK-02 | CCCD sai | CCCD `12345` | "CCCD phải gồm 12 chữ số." | ⛔ | |
| DK-03 | SĐT sai | SĐT `912345678` (thiếu số 0) | "Số điện thoại phải gồm 10 số, bắt đầu 0." | ⛔ | |
| DK-04 | Email sai | `abc@` | "Email không đúng định dạng." | ⛔ | |
| DK-05 | Trùng tên đăng nhập | Tên `admin` | "Tên đăng nhập đã tồn tại!" | ⛔ | |
| DK-06 | Đăng ký hợp lệ | Thông tin đầy đủ, đúng định dạng | "Đăng ký thành công! Hãy đăng nhập." Màn hình đăng nhập điền sẵn tên vừa đăng ký, đăng nhập được | ✅ | |
| DK-07 | Nhập lại mật khẩu không khớp | Mật khẩu `123456`, nhập lại `123457` | "Mật khẩu nhập lại không khớp." | ⛔ | |
| DK-08 | Mật khẩu ngắn | Mật khẩu `12345` | "Mật khẩu tối thiểu 6 ký tự." | ⛔ | |
| DK-09 | CCCD đã có người dùng | CCCD `079204001234` | "CCCD … đã được đăng ký cho khách thuê khác." | ⛔ | |

---

## 1. Dashboard

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| DB-01 | Thẻ số liệu | Mở Dashboard | Tổng phòng, phòng trống, đang thuê, công nợ… khớp với trang Báo cáo | ✅ | |
| DB-02 | Biểu đồ doanh thu | Rê chuột vào từng cột | Cột chồng "Đã thu" + "Còn nợ" của 6 kỳ, tooltip hiện số tiền đầy đủ (`1.250.000 đ`) | ✅ | |
| DB-03 | Biểu đồ tình trạng phòng | Xem biểu đồ vòng | Tổng ở giữa = tổng phòng, chú thích có số lượng và % từng trạng thái | ✅ | |
| DB-04 | HĐ sắp hết hạn | Xem bảng | Có HĐ#5 – Phòng A203 – Phạm Thị Dung (31/10/2026) | ✅ | |
| DB-05 | Số liệu cập nhật | Thu tiền một hóa đơn (HD-07) → quay lại Dashboard | Công nợ giảm đúng bằng số vừa thu | ✅ | |
| DB-06 | Đổi sáng/tối | Bật chế độ tối | Biểu đồ đổi màu theo, tooltip đọc được | 🎨 | |

---

## 2. VẬN HÀNH

### 2.1 Phòng trọ

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| PH-01 | Thêm phòng | Tên, loại phòng, cơ sở, giá `3500000` | Ô giá hiện `3.500.000 đ`, phòng mới có trong bảng (tự làm mới) | ✅ | |
| PH-02 | Thiếu tên | Bỏ trống tên phòng | "Tên phòng không được để trống." | ⛔ | |
| PH-03 | Sửa phòng | Chọn phòng → Sửa giá | Ô giá điền sẵn đúng định dạng, lưu xong bảng cập nhật | ✅ | |
| PH-04 | Xóa phòng đã có hợp đồng | Xóa Phòng A101 | "Phòng đã có hợp đồng nên không thể xóa. Chuyển trạng thái sang 'Bảo trì'…" | ⛔ | |
| PH-05 | Xóa phòng đang được đặt | Xóa Phòng B102 | "Phòng đang có lượt đặt nên không thể xóa." | ⛔ | |
| PH-06 | Xóa phòng còn tài sản | Xóa phòng mới tạo ở PH-01 sau khi gán tài sản | "Phòng còn tài sản được gán, hãy gỡ tài sản trước." | ⛔ | |
| PH-07 | Xóa phòng trống, chưa có dữ liệu | Xóa phòng tạo ở PH-01 (chưa gán gì) | Hộp xác nhận → xóa thành công | ✅ | |
| PH-08 | Tìm kiếm | Gõ `A10` vào ô tìm kiếm | Chỉ còn các phòng khớp | ✅ | |

### 2.2 Khách thuê

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| KT-01 | Thêm khách | Họ tên, CCCD 12 số, SĐT `0912345678` | Thêm thành công, bảng tự làm mới | ✅ | |
| KT-02 | Thiếu họ tên | Bỏ trống họ tên | "Họ tên không được để trống." | ⛔ | |
| KT-03 | CCCD sai | `0123456789` (10 số) | "CCCD phải gồm đúng 12 chữ số." | ⛔ | |
| KT-04 | SĐT sai | `1234567890` | "SĐT phải gồm 10 chữ số và bắt đầu bằng 0." | ⛔ | |
| KT-05 | Sửa thành CCCD đã có | Sửa khách, nhập CCCD của khách khác | "CCCD … đã thuộc về khách thuê khác." | ⛔ | |
| KT-06 | Tìm kiếm | Gõ một phần họ tên, CCCD, SĐT hoặc email | Lọc đúng, không phân biệt hoa/thường | ✅ | |
| KT-07 | Xóa mềm | Chọn khách → Xóa | ⚠️ **Lỗi đã biết**: API chưa có endpoint `xoa-mem` (cần thêm cột vào DB) → báo lỗi 404 | ⛔ | |

### 2.3 Đặt phòng

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| DP-01 | Đặt phòng trống | Phòng A104, khách #12, cọc `2000000` | Phòng chuyển *Đã đặt cọc*, Sổ thu chi có phiếu thu "Thu tiền đặt cọc phòng …" | ✅ | |
| DP-02 | Đặt phòng không trống | Phòng A101 (đang thuê) | "Phòng A101 đang ở trạng thái 'Đang thuê', chỉ đặt được phòng trống." | ⛔ | |
| DP-03 | Mã phòng sai | Mã phòng `abc` | "Mã phòng phải là số nguyên dương." | ⛔ | |
| DP-04 | Ô tiền chỉ nhận số | Gõ chữ / dấu cách vào ô tiền cọc | Không nhập được, chỉ nhận chữ số | ⛔ | |
| DP-05 | Hủy và hoàn cọc | Chọn phiếu ở DP-01 → Hủy giữ chỗ → hoàn cọc | Phiếu *Đã hủy*, phòng *Trống*, có phiếu chi "(khách hủy)" | ✅ | |
| DP-06 | Hủy phiếu đã hủy | Hủy lại phiếu ở DP-05 | "Chỉ hủy được phiếu đang giữ chỗ (hiện: Đã hủy)." | ⛔ | |
| DP-07 | Hoàn quá tiền cọc | Hủy phiếu, nhập số hoàn lớn hơn tiền cọc | "Số tiền hoàn phải từ 0 đến tiền cọc (…)." | ⛔ | |

### 2.4 Hợp đồng

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| HD-01 | Nhận phòng từ booking | Phòng B102 + khách #11 (Trương Mỹ Linh), giá thuê, cọc | Phòng *Đang thuê*, booking *Đã nhận phòng*, có thêm **cư trú**, **dịch vụ Điện và Nước**, phiếu thu phần cọc còn thiếu | ✅ | |
| HD-02 | Phòng đang giữ chỗ cho khách khác | Phòng có booking, nhập khách khác | "… đang được giữ chỗ cho khách #… Hãy hủy giữ chỗ trước…" | ⛔ | |
| HD-03 | Phòng đã có hợp đồng | Phòng A101 | "Phòng A101 đã có hợp đồng đang hiệu lực." | ⛔ | |
| HD-04 | Phòng bảo trì | Phòng A204 | "Phòng A204 đang bảo trì, chưa thể cho thuê." | ⛔ | |
| HD-05 | Ngày sai | Ngày kết thúc ≤ ngày bắt đầu | "Ngày kết thúc phải sau ngày bắt đầu." | ⛔ | |
| HD-06 | Giá thuê không hợp lệ | Giá thuê `0` | "Giá thuê/tháng không hợp lệ." | ⛔ | |
| HD-07 | Gia hạn | HĐ#5 → Gia hạn đến 31/10/2027, giá mới tùy chọn | Ngày kết thúc cập nhật, khách nhận thông báo | ✅ | |
| HD-08 | Gia hạn lùi ngày | Ngày mới ≤ hôm nay | "Ngày kết thúc mới phải sau ngày hôm nay." | ⛔ | |
| HD-09 | Gia hạn không lùi qua ngày cũ | Ngày mới sau hôm nay nhưng trước ngày kết thúc hiện tại | "Ngày kết thúc mới phải sau ngày kết thúc hiện tại (dd/MM/yyyy)." | ⛔ | |
| HD-10 | Xem sắp hết hạn | Bấm *Sắp hết hạn* | Chỉ hiện hợp đồng hết hạn trong 30 ngày | ✅ | |
| HD-11 | Thanh lý (cọc lớn hơn nợ) | HĐ#2 sau khi thu 1.000.000 đ (còn nợ 2.090.000 đ, cọc 2.600.000 đ) | Cọc cấn trừ hết nợ, **hoàn 510.000 đ** bằng phiếu chi. HĐ *Đã thanh lý*, phòng *Trống*, cư trú và dịch vụ kết thúc | ✅ | |
| HD-12 | Thanh lý lần 2 | Thanh lý lại HĐ ở HD-11 | "Hợp đồng không còn hiệu lực (hiện: Đã thanh lý)." | ⛔ | |
| HD-13 | Hoàn cọc vượt mức | Nhập tiền hoàn > tiền cọc | "Tiền hoàn cọc phải từ 0 đến tiền cọc (…)." | ⛔ | |

### 2.5 Cư trú

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| CT-01 | Danh sách | Mở trang | Hiện mọi cư trú, có cột Mã HĐ | ✅ | |
| CT-02 | Thêm người ở cùng | Chọn dòng của HĐ → Thêm (mã HĐ điền sẵn) → khách chưa ở đâu | Thêm thành công | ✅ | |
| CT-03 | Khách đang ở HĐ khác | Khách #1 vào HĐ#2 | "Khách #1 đang cư trú ở hợp đồng #…" | ⛔ | |
| CT-04 | Khách đã có trong HĐ | Thêm lại đúng khách vào đúng HĐ | "Khách #… đã có trong hợp đồng #… rồi." | ⛔ | |
| CT-05 | HĐ không hiệu lực | HĐ đã thanh lý | "Chỉ thêm người ở cùng vào hợp đồng đang hiệu lực." | ⛔ | |
| CT-06 | Phòng đủ người | Loại phòng có giới hạn số người, thêm vượt | "Phòng đã đủ N người theo loại phòng." | ⛔ | |
| CT-07 | Đổi trạng thái | Khai báo → Tạm vắng → Trở lại → Rời đi | Cột trạng thái đổi theo, bảng tự làm mới | ✅ | |

### 2.6 Bàn giao tài sản

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| BG-01 | Xem tài sản của phòng | Chọn phòng | Danh sách tài sản và tình trạng | ✅ | |
| BG-02 | Cập nhật tình trạng | Chọn tài sản → Cập nhật → chọn tình trạng | Bảng hiển thị tình trạng mới | ✅ | |
| BG-03 | Chưa chọn tình trạng | Mở dialog, không chọn → Lưu | "Vui lòng chọn tình trạng." | ⛔ | |

### 2.7 Báo trả phòng

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| BT-01 | Tạo báo trả | Mã HĐ#2, mã khách, lý do | Có yêu cầu mới trong bảng | ✅ | |
| BT-02 | Thiếu lý do | Bỏ trống lý do | "Vui lòng nhập lý do." | ⛔ | |
| BT-03 | Mã sai | Mã HĐ `abc` | "Mã hợp đồng phải là số nguyên dương." | ⛔ | |
| BT-04 | Hoàn tất sau thanh lý | Thanh lý HĐ (HD-11) | Báo trả của HĐ chuyển *Đã hoàn tất* | ✅ | |

---

## 3. TÀI CHÍNH

### 3.1 Chỉ số điện nước

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| CS-01 | Ghi chỉ số | Phòng, loại *Điện*, kỳ `2026-10`, cũ ≤ mới | Thêm thành công | ✅ | |
| CS-02 | Chỉ số mới nhỏ hơn cũ | Cũ `500`, mới `400` | "Chỉ số mới phải lớn hơn hoặc bằng chỉ số cũ." | ⛔ | |
| CS-03 | Kỳ sai định dạng | `10/2026` | "Tháng năm không đúng định dạng yyyy-MM (VD: 2026-10, tháng 01–12)." | ⛔ | |
| CS-06 | Tháng không hợp lệ | `2026-13` hoặc `abcd-ef` | Báo sai định dạng, không văng lỗi kỹ thuật | ⛔ | |
| CS-07 | Kỳ chưa tới | Kỳ tháng sau | "Không ghi chỉ số cho kỳ chưa tới." | ⛔ | |
| CS-08 | Chỉ số âm | Chỉ số cũ `-5` | "Chỉ số cũ phải là số không âm." | ⛔ | |
| CS-04 | Chưa chọn loại | Bỏ trống loại chỉ số | "Vui lòng chọn loại chỉ số." | ⛔ | |
| CS-05 | Xóa chỉ số | Chọn dòng → Xóa | Hộp xác nhận → xóa, bảng tự làm mới | ✅ | |

### 3.2 Hóa đơn

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| HO-01 | Bảng có cột kỳ | Mở trang | Cột Tháng/Năm hiển thị đúng, ngày dạng dd/MM/yyyy | ✅ | |
| HO-02 | Tạo tự động | HĐ đã ghi đủ chỉ số điện và nước kỳ `2026-10` → *Tự động* | Có các dòng: "Tiền phòng tháng 10/2026", "Tiền điện tháng 10/2026 (chỉ số A -> B kWh)", nước, "Phí …" | ✅ | |
| HO-03 | Thiếu chỉ số | HĐ chưa ghi chỉ số kỳ đó → *Tự động* | "Chưa ghi chỉ số điện và nước tháng … cho phòng của hợp đồng #…" | ⛔ | |
| HO-04 | Trùng kỳ | Tạo lại hóa đơn ở HO-02 | "Hóa đơn tháng 10/2026 của hợp đồng #… đã tồn tại." | ⛔ | |
| HO-05 | Nhập tay | Chọn *Nhập tay*, tổng tiền `1500000` | Tạo hóa đơn với đúng tổng tiền | ✅ | |
| HO-06 | Nhập tay tổng 0 | Tổng tiền trống hoặc `0` | "Tổng tiền không hợp lệ." | ⛔ | |
| HO-07 | Thu một phần | Hóa đơn HĐ#2 (còn 3.090.000 đ) → Thu `1000000` | *Thanh toán một phần*, có phiếu thu "Thu tiền hóa đơn tháng …", công nợ giảm | ✅ | |
| HO-08 | Thu vượt số còn lại | Thu nhiều hơn số còn lại | "Số tiền thu không được vượt quá số còn lại (…)." | ⛔ | |
| HO-09 | Thu đủ | Thu nốt số còn lại | *Đã thanh toán* | ✅ | |
| HO-10 | Thu hóa đơn đã đủ | Thu tiền hóa đơn ở HO-09 | "Hóa đơn này đã thanh toán đủ." | ⛔ | |

### 3.3 Sổ thu chi

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| TC-01 | Thêm phiếu thu | Loại *Thu*, `500000`, mô tả, ngày | Có phiếu mới đúng ngày đã chọn | ✅ | |
| TC-02 | Thêm phiếu chi | Loại *Chi* | Có phiếu chi | ✅ | |
| TC-03 | Số tiền trống | Bỏ trống số tiền | "Số tiền không hợp lệ." | ⛔ | |
| TC-04 | Thiếu mô tả | Bỏ trống mô tả | "Vui lòng nhập mô tả." | ⛔ | |
| TC-05 | Mã HĐ sai | Mã HĐ `abc` | "Mã hợp đồng phải là số nguyên dương." | ⛔ | |
| TC-06 | Phiếu tự sinh | Sau DP-01, HD-01, HO-07, HD-11 | Có các phiếu thu/chi tương ứng, không phải nhập tay | ✅ | |
| TC-07 | Xóa phiếu | Chọn phiếu → Xóa | Hộp xác nhận → xóa | ✅ | |

### 3.4 Dịch vụ đã đăng ký

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| DV-01 | Danh sách | Mở trang | Hiện mọi đăng ký, không cần nhập mã HĐ | ✅ | |
| DV-02 | Đăng ký dịch vụ | HĐ hiệu lực + dịch vụ chưa dùng | Thêm thành công, đơn giá áp dụng mặc định lấy theo bảng giá | ✅ | |
| DV-03 | Trùng dịch vụ | Đăng ký lại dịch vụ đang dùng | "Hợp đồng #… đang dùng dịch vụ '…' rồi." | ⛔ | |
| DV-04 | HĐ không hiệu lực | HĐ đã thanh lý | "Chỉ đăng ký dịch vụ cho hợp đồng đang hiệu lực." | ⛔ | |
| DV-05 | Ngừng dịch vụ | Chọn dòng → Ngừng | Hộp xác nhận → *Đã kết thúc* | ✅ | |

---

## 4. XỬ LÝ & BÁO CÁO

### 4.1 Sửa chữa

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| SC-01 | Tạo yêu cầu | Mã HĐ, mô tả, ngày | Có yêu cầu *Chờ tiếp nhận*, phòng và khách lấy tự động theo HĐ | ✅ | |
| SC-02 | Thiếu mô tả | Bỏ trống mô tả | "Mô tả yêu cầu không được để trống." | ⛔ | |
| SC-03 | Cập nhật trạng thái | *Chờ tiếp nhận* → *Đang sửa* → *Đã hoàn thành*, chi phí `350000` | Trạng thái và chi phí cập nhật | ✅ | |
| SC-04 | Chi phí điền sẵn | Mở lại dialog cập nhật | Chi phí hiện `350.000 đ` | 🎨 | |
| SC-05 | Xóa | Chọn → Xóa | Hộp xác nhận → xóa | ✅ | |

### 4.2 Vi phạm

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| VP-01 | Ghi vi phạm | Mã HĐ, loại vi phạm, ngày, mức phạt | Có vi phạm *Chưa xử lý* | ✅ | |
| VP-02 | Thiếu loại vi phạm | Bỏ trống loại | "Nhập loại vi phạm." | ⛔ | |
| VP-03 | Chưa chọn ngày | Bỏ trống ngày | "Chọn ngày vi phạm." | ⛔ | |
| VP-04 | Xử lý | Bút chì ở dòng → *Phạt tiền*, tick gửi thông báo | *Đã xử lý*, khách nhận thông báo | ✅ | |
| VP-05 | Thiếu hình thức xử lý | Bỏ trống hình thức | "Chọn hoặc nhập hình thức xử lý." | ⛔ | |
| VP-06 | Xử lý lần 2 | Xử lý lại vi phạm ở VP-04 | "Vi phạm này đã được xử lý." | ⛔ | |
| VP-07 | Xóa | Chọn → Xóa | Hộp xác nhận → xóa | ✅ | |

### 4.3 Thông báo (Admin)

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| TB-01 | Gửi cho một khách | Tiêu đề, nội dung, mã tài khoản nhận | Gửi thành công, cột trạng thái "Đã đọc 0/1 · 0 phản hồi" | ✅ | |
| TB-02 | Gửi cho tất cả | Bỏ trống ô người nhận → xác nhận | Chỉ gửi tới tài khoản **Customer**, không gửi cho admin | ✅ | |
| TB-03 | Thiếu tiêu đề / nội dung | Bỏ trống | "Nhập tiêu đề." / "Nhập nội dung." | ⛔ | |
| TB-04 | Mã người nhận sai | Nhập `abc` | "Mã tài khoản \"abc\" không hợp lệ…" — **không** gửi cho tất cả | ⛔ | |
| TB-09 | Gửi nhiều người | Nhập `2, 5` | Chỉ 2 tài khoản đó nhận | ✅ | |
| TB-10 | Để trống người nhận | Bỏ trống → Gửi | Hỏi xác nhận "Gửi cho tất cả?" trước khi gửi | ✅ | |
| TB-05 | Nhắc nhở tự động | Bấm *Nhắc nhở* | Gửi nhắc nợ cho khách còn nợ và nhắc HĐ sắp hết hạn | ✅ | |
| TB-06 | Nhắc nhở lần 2 trong ngày | Bấm *Nhắc nhở* lần nữa | Không gửi trùng | ⛔ | |
| TB-07 | Xem phản hồi | Sau KH-04 → bấm *Xem phản hồi* | Danh sách phản hồi kèm tên khách và nội dung | ✅ | |
| TB-08 | Xóa thông báo | Chọn → Xóa | Hộp xác nhận → xóa cả danh sách người nhận | ✅ | |

### 4.4 Báo cáo

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| BC-01 | Tổng quan | Mở tab Tổng quan | 8 thẻ số liệu, biểu đồ doanh thu 12 kỳ, biểu đồ công nợ theo phòng (nợ nhiều nhất ở trên) | ✅ | |
| BC-02 | HĐ sắp hết hạn | Tab 2 | Có cột "Còn lại (ngày)" | ✅ | |
| BC-03 | Hóa đơn chưa thu | Tab 3 | Có Phòng, Khách, Hạn TT; hóa đơn quá hạn hiện "Quá hạn N ngày" | ✅ | |
| BC-04 | Doanh thu theo kỳ | Tab 4 | Số liệu khớp với biểu đồ doanh thu (tổng = đã thu + còn nợ) | ✅ | |
| BC-05 | Xuất Excel | *Xuất Excel* → lưu → *Mở file* | 4 sheet, tiền là số (lọc và sắp xếp được), dòng tổng dùng `SUM`, tỷ lệ thu dạng % | ✅ | |
| BC-06 | File đang mở | Xuất đè lên file đang mở trong Excel | "Không ghi được file — có thể file đang mở trong Excel…" | ⛔ | |
| BC-07 | Không có công nợ | (Khi mọi hóa đơn đã thu đủ) | Biểu đồ công nợ hiện "Không có công nợ — mọi hóa đơn đã thu đủ" | 🎨 | |

---

## 5. THIẾT LẬP

### 5.1 Cơ sở

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| CO-01 | Thêm / sửa | Tên, địa chỉ | Lưu thành công | ✅ | |
| CO-02 | Thiếu tên | Bỏ trống tên | "Tên cơ sở không được để trống." | ⛔ | |
| CO-03 | Xóa | Cơ sở mới tạo → Xóa | Hộp xác nhận → xóa. Nếu còn phòng thuộc cơ sở → báo lỗi rõ ràng, không crash | ✅ | |

### 5.2 Loại phòng

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| LP-01 | Thêm / sửa | Tên, giá mặc định `3000000`, số người tối đa | Lưu thành công, giá hiển thị `3.000.000 đ` khi sửa | ✅ | |
| LP-02 | Thiếu tên | Bỏ trống tên | "Tên loại phòng không được để trống." | ⛔ | |
| LP-03 | Xóa loại đang dùng | Xóa loại có phòng | "Loại phòng đang được dùng bởi ít nhất một phòng nên không thể xóa." | ⛔ | |

### 5.3 Bảng giá dịch vụ

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| GD-01 | Thêm dịch vụ | Tên, đơn vị tính, đơn giá | Lưu thành công | ✅ | |
| GD-02 | Thiếu tên | Bỏ trống tên | "Tên dịch vụ không được để trống." | ⛔ | |
| GD-03 | Xóa | Dịch vụ mới tạo → Xóa | Hộp xác nhận → xóa | ✅ | |

### 5.4 Tài sản

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| TS-01 | Thêm tài sản | Tên, giá trị tham khảo | Lưu thành công | ✅ | |
| TS-02 | Thiếu tên | Bỏ trống tên | "Tên tài sản không được để trống." | ⛔ | |
| TS-03 | Gán vào phòng | Chọn tài sản → Gán, mã phòng, số lượng `2` | Tài sản hiện trong danh sách của phòng | ✅ | |
| TS-04 | Số lượng sai | Số lượng `0` hoặc `-1` | "Số lượng phải là số nguyên dương." | ⛔ | |
| TS-05 | Sửa / gỡ tài sản của phòng | Sửa số lượng, rồi gỡ | Cập nhật đúng | ✅ | |

### 5.5 Tài khoản

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| TK-01 | Tạo tài khoản khách | Tên `linh`, mật khẩu ≥ 6 ký tự, vai trò Customer, mã khách `11` | Tạo thành công, đăng nhập được (DN-05) | ✅ | |
| TK-02 | Mật khẩu ngắn | Mật khẩu `123` | "Mật khẩu tối thiểu 6 ký tự." | ⛔ | |
| TK-03 | Trùng tên đăng nhập | Tên `admin` | "Tên đăng nhập đã tồn tại." | ⛔ | |
| TK-04 | Customer thiếu mã khách | Vai trò Customer, bỏ trống mã khách | "Tài khoản khách thuê phải gắn với một mã khách." | ⛔ | |
| TK-05 | Mã khách đã có tài khoản | Mã khách `1` | "Khách thuê #1 đã có tài khoản." | ⛔ | |
| TK-06 | Mã khách không tồn tại | Mã khách `9999` | "Không tìm thấy khách thuê #9999." | ⛔ | |
| TK-07 | Đổi vai trò | Sửa tài khoản → đổi vai trò | Cập nhật thành công | ✅ | |
| TK-08 | Hạ quyền admin cuối cùng | Sửa `admin` → Customer | "Đây là tài khoản quản trị cuối cùng, không thể hạ quyền." | ⛔ | |
| TK-09 | Đặt lại mật khẩu | Sửa tài khoản khách → nhập mật khẩu mới ≥ 6 ký tự → Lưu | Thông báo "Đã đặt lại mật khẩu". Đăng nhập được bằng mật khẩu mới, mật khẩu cũ không còn dùng được | ✅ | |
| TK-10 | Mật khẩu mới quá ngắn | Mật khẩu mới `123` | "Mật khẩu mới tối thiểu 6 ký tự." | ⛔ | |
| TK-11 | Sửa nhưng để trống mật khẩu | Chỉ đổi vai trò | Mật khẩu cũ vẫn dùng được | ✅ | |

### 5.6 Sao lưu

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| SL-01 | Tạo bản sao lưu | Bấm *Tạo sao lưu* | Có dòng mới trong danh sách | ✅ | |
| SL-02 | Xóa bản sao lưu | Chọn → Xóa | Hộp xác nhận → xóa | ✅ | |
| SL-03 | Bản sao lưu sau khi khởi động lại API | Khởi động lại API → mở trang | ⚠️ **Hạn chế đã biết**: danh sách trống, vì hiện chỉ lưu trong bộ nhớ, chưa sao lưu DB thật | ⛔ | |

---

## 6. Vai trò khách thuê

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| KH-01 | Thông tin của tôi | Đăng nhập tài khoản ở TK-01 | Thấy hợp đồng và hóa đơn **của mình** (Phòng B102), không thấy của người khác | ✅ | |
| KH-02 | Không có menu quản trị | Xem sidebar | Không có Phòng, Hóa đơn, Tài khoản… | ✅ | |
| KH-03 | Đọc thông báo | Mở Thông báo → chọn → Đọc | Đánh dấu đã đọc. Admin thấy "Đã đọc 1/1" | ✅ | |
| KH-04 | Phản hồi | Chọn thông báo → Phản hồi → nhập nội dung | Gửi thành công, admin thấy "1 phản hồi" (TB-07) | ✅ | |
| KH-05 | Phản hồi trống | Bỏ trống nội dung | "Nhập nội dung phản hồi." | ⛔ | |

---

## 7. Giao diện chung

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| UI-01 | Bảng tự làm mới | Thêm / sửa / xóa ở bất kỳ trang nào | Bảng cập nhật ngay, không cần bấm Tải lại | ✅ | |
| UI-02 | Xác nhận khi xóa | Bấm xóa ở bất kỳ trang nào | Hộp xác nhận có tên bản ghi. Bấm Hủy thì không xóa | ✅ | |
| UI-03 | Ô nhập tiền | Gõ `1500000` | Hiện `1.500.000 đ`, con trỏ đứng đúng chỗ | 🎨 | |
| UI-04 | Xóa trong ô tiền | Đặt con trỏ sau dấu chấm → Backspace | Xóa chữ số bên cạnh, không kẹt ở dấu chấm | 🎨 | |
| UI-05 | Dán vào ô tiền | Dán `1.500.000 đ` hoặc `1500000.00` | Ra `1.500.000` | 🎨 | |
| UI-06 | Dialog giữa màn hình | Mở các dialog | Luôn ở giữa màn hình | 🎨 | |
| UI-07 | Dialog không bị che | Mở *Thêm thu chi*, *Tạo hợp đồng*; Windows phóng to chữ 125–150% | Thấy đủ các ô, có thanh cuộn khi cần, nút Hủy/Lưu luôn ở dưới | 🎨 | |
| UI-08 | Thanh tiêu đề dialog | Rê chuột vào nút ✕ | Nền đỏ chữ trắng. Kéo dialog bằng thanh tiêu đề được. Esc và Alt+F4 đóng được | 🎨 | |
| UI-09 | Chế độ tối | Bật chế độ tối → mở các trang và dialog | Nền, chữ, bảng, biểu đồ, dialog đều đổi màu, chữ vẫn đọc được | 🎨 | |
| UI-10 | Tìm menu | Gõ `dien nuoc` / `hop dong` vào ô tìm ở sidebar | Lọc ra đúng menu, không cần gõ dấu | ✅ | |
| UI-11 | Mục menu đang chọn | Bấm qua các menu | Mục đang chọn có nền sáng hơn, vạch chỉ báo bên trái, chữ đậm | 🎨 | |
| UI-12 | Con trỏ trên bảng | Rê chuột lên các dòng của bảng | Hiện bàn tay | 🎨 | |
| UI-13 | Thu gọn sidebar | Bấm nút thu gọn menu | Sidebar thu gọn hoặc mở lại, nội dung giãn theo | 🎨 | |

---

## 8. Kiểm tra dữ liệu nhập (bổ sung)

| ID | Tình huống | Các bước / dữ liệu | Kết quả mong đợi | Loại | KQ |
|---|---|---|---|---|---|
| VD-01 | Mã bằng 0 / âm | Ô mã phòng, mã khách, mã HĐ… nhập `0` hoặc `-5` | "… phải là số nguyên dương." | ⛔ | |
| VD-02 | Gõ quá dài | Dán đoạn rất dài vào tên phòng, họ tên, mô tả… | Không gõ thêm được quá giới hạn của DB (VD tên phòng 50, mô tả 300 ký tự) | ⛔ | |
| VD-03 | Họ tên có số / ký tự lạ | `Nguyen 123`, `@@abc` | "Họ tên chỉ gồm chữ cái và khoảng trắng (2–100 ký tự)." | ⛔ | |
| VD-04 | Email khách thuê sai | `abc@` | "Email không đúng định dạng (VD: ten@gmail.com)." | ⛔ | |
| VD-05 | SĐT cơ sở sai | `12345` | "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0." | ⛔ | |
| VD-06 | Loại phòng bỏ trống số người | Để trống *Số người tối đa* | "Số người tối đa phải là số nguyên từ 1 đến 20." | ⛔ | |
| VD-07 | Diện tích sai | `0` hoặc `abc` | "Diện tích phải là số lớn hơn 0 và không quá 1000 m²." | ⛔ | |
| VD-08 | Phòng: mã loại / cơ sở sai | Mã loại phòng `abc` | Báo lỗi, **không** tự đổi thành 1 như trước | ⛔ | |
| VD-09 | Phòng: mã loại không tồn tại | Mã loại phòng `9999` | "Không có loại phòng mã 9999." | ⛔ | |
| VD-10 | Ngày trong tương lai | Ngày vi phạm / ngày yêu cầu sửa chữa / ngày ghi nhận thu chi = ngày mai | "… không được sau hôm nay." | ⛔ | |
| VD-11 | Sửa chữa hoàn thành thiếu ngày | Trạng thái *Đã hoàn thành*, không chọn ngày | "Chọn ngày hoàn thành." | ⛔ | |
| VD-12 | Thanh lý hoàn cọc vượt cọc | Tiền hoàn > tiền cọc | "Tiền hoàn cọc không được vượt quá tiền cọc (…)." (chặn ngay trong dialog) | ⛔ | |
| VD-13 | Tên đăng nhập có dấu / khoảng trắng | `khôi`, `tuan khoi`, `ab` | "Tên đăng nhập 3–50 ký tự, chỉ gồm chữ không dấu, số, dấu chấm hoặc gạch dưới." | ⛔ | |
| VD-14 | Tài khoản khách sai mã khách | Mã khách `abc` | "Mã khách thuê phải là số nguyên dương…" | ⛔ | |
| VD-15 | Thông báo lỗi dễ hiểu | Gây một lỗi từ máy chủ bất kỳ | Không còn dạng `HTTP 400 POST /api/...` hay tiếng Anh kỹ thuật; chi tiết ghi ở `%TEMP%\qlpt_wpf_api_errors.txt` | 🎨 | |

---

## 9. Số liệu phản ánh đúng sau mỗi nghiệp vụ

So Dashboard, Báo cáo và Sổ thu chi **trước và sau** mỗi thao tác. Ngoài cột thay đổi kỳ vọng, các con số khác phải giữ nguyên.

| ID | Thao tác | Thay đổi kỳ vọng | Loại | KQ |
|---|---|---|---|---|
| PA-01 | Đặt phòng trống, cọc C | Phòng trống −1, Đã đặt cọc +1, Sổ thu +C | ✅ | |
| PA-02 | Nhận phòng từ phiếu đặt (cọc HĐ = H, đã cọc C) | Đang thuê +1, HĐ hiệu lực +1, Khách đang ở +1, Đã đặt cọc −1, Sổ thu +(H − C), tự đăng ký Điện + Nước | ✅ | |
| PA-03 | Lập HĐ cho người đang ở HĐ khác | Bị chặn: "… đang cư trú ở hợp đồng #… Cập nhật cư trú bên đó sang "Rời đi"…" | ⛔ | |
| PA-04 | Thu tiền hóa đơn số tiền X | Công nợ −X, Doanh thu tháng +X, Sổ thu +X | ✅ | |
| PA-05 | Xử lý vi phạm, phạt tiền P | Sổ thu chi có phiếu thu "Thu tiền phạt vi phạm - Phòng …" +P | ✅ | |
| PA-06 | Sửa chữa → Đã hoàn thành, chi phí K | Sổ thu chi có phiếu chi "… (yêu cầu sửa chữa số N)" +K. Lưu lại lần 2 thì **không** ghi trùng | ✅ | |
| PA-07 | Thanh lý khi cọc dư (cọc C, nợ N, phạt F, C > N + F) | Đang thuê −1, Phòng trống +1, HĐ hiệu lực −1, Khách đang ở −(số người của HĐ), Công nợ −N, Doanh thu +N (cấn trừ cọc), phiếu chi hoàn (C − N − F); bản ghi thanh lý ghi *hoàn cọc* = C − N − F | ✅ | |
| PA-08 | Thanh lý khi cọc **không đủ** (C < N + F) | Như SL-17 nhưng không có phiếu chi. Phần phạt chưa trừ được vào cọc được **cộng vào hóa đơn** ("Phạt khi thanh lý…") nên vẫn nằm trong công nợ; *hoàn cọc* = 0 | ✅ | |
| PA-09 | Hủy giữ chỗ, hoàn cọc C | Phòng trống +1, Đã đặt cọc −1, Sổ chi +C | ✅ | |
| PA-10 | Cư trú → "Rời đi" (người ở cùng) | Khách đang ở −1, các số khác giữ nguyên | ✅ | |