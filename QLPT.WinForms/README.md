# WinForms .NET 8 — Nguyễn Tuấn Khôi

Nguồn: `QLPT_BanChot_NET8_Verified_20261008.zip`, SHA-256 `C04CF6E1E1B28FFC9B3BE9C1FF6009A1AAC0469BECAA47AC14D2F425C7B447B8`. Đây là phần WinForms của bản chốt, không lấy chức năng Tuần 9. Repository này không chứa source API hoặc SQL của bản ZIP.

## Build và khởi động trên Windows

Cài .NET SDK 8 và Windows Desktop Runtime tương ứng. `global.json` chọn SDK 8 với roll-forward trong cùng phiên bản major. Từ thư mục gốc repository:

```powershell
dotnet restore QLPT_TichHop.sln
dotnet build QLPT_TichHop.sln --no-restore --configuration Debug
.\QLPT.WinForms\bin\Debug\net8.0-windows\QLPT.WinForms.exe
```

Solution chỉ chứa `QLPT.WinForms/QLPT.WinForms.csproj`, target `net8.0-windows`, x64. `ocr-data/vie.traineddata` là dữ liệu ngôn ngữ nguồn cần cho OCR, được project chép vào tessdata khi build; xem NGUON.md về nguồn dữ liệu. Không đưa bin/obj, ZIP hoặc log vào Git.

Địa chỉ API mặc định `http://localhost:5180`; có thể nhập địa chỉ khác tại màn hình đăng nhập. Dùng tài khoản do nhóm đã cấu hình ở backend; không có mật khẩu/token/chuỗi kết nối SQL trong project WinForms.

## Hợp đồng Khách thuê và xác thực

- POST `/api/Auth/login`: request `{username,password}`; response phải có `maTaiKhoan` số nguyên dương, `vaiTro` Admin/Customer, `maKhach` dương nếu Customer và cookie `QLPT.Session`. Client không tạo ID giả hoặc bỏ kiểm tra để vượt login legacy. Backend phải kiểm tra mật khẩu và quyền độc lập.
- GET `/api/KhachThue`: mảng JSON khách thuê. GET `/api/KhachThue/{id}`: hồ sơ để sửa. POST `/api/KhachThue`: thêm; PUT `/api/KhachThue/{id}`: sửa. Thuộc tính: `hoTen`, `cccd`, `soDienThoai`, `gioiTinh`, `email`, `diaChi`, `ngaySinh`; ngày sinh hiện có được giữ khi sửa, optional rỗng gửi null.
- Tìm kiếm lọc tên/CCCD/SĐT/email/địa chỉ và mã trên danh sách đã tải, không phụ thuộc query search của API. Làm mới gọi lại API, vẫn giữ từ khóa hiện tại.
- **Hợp đồng xóa mềm đề xuất, chưa có trong API thật:** PUT `/api/KhachThue/{id}/xoa-mem`, body `{}`, response `{maKhach:id,isDeleted:true}`. Backend cần giữ bản ghi, đặt `IsDeleted`/`DeletedAt`, lọc danh sách hoạt động và kiểm tra dữ liệu liên kết. Client chỉ báo thành công khi nhận đúng xác nhận. Không fallback sang DELETE xóa vật lý. Thiếu endpoint sẽ báo lỗi và giữ dữ liệu.
- HTTP lỗi, JSON sai, mất API, timeout và phiên 401 được báo rõ. Form giữ dữ liệu nhập khi lưu thất bại; thao tác đang gửi không được gửi lặp.

## Phạm vi đã kiểm chứng và việc cần Toàn xử lý

Bản ZIP đã build 0 lỗi, còn 2 cảnh báo CS8604 cũ tại `HoaDonForms.cs` dòng 36/38. EXE đã khởi động thật. Với API đi kèm ZIP và SQL Express: đăng nhập, MainForm/menu, tải 8 khách, tìm kiếm và làm mới đã chạy thật. Thêm/sửa/xóa mềm đã kiểm tra bằng HTTP mock và form sản phẩm; chưa xác nhận ghi SQL thật, không gọi kết quả mock là tích hợp thật. Kiểm thử được chạy lại từ repository trước khi đẩy nhánh; log/bộ fixture giữ ngoài Git.

API hiện tại của Toàn còn các sai khác được chứng minh:

1. `Controllers/AuthController.cs`: Login bỏ qua Password, thiếu `maTaiKhoan` và cookie. `Program.cs` chưa đăng ký authentication. Cần xác thực mật khẩu/trạng thái/vai trò, cấp cookie và trả DTO đúng; thêm logout, kiểm tra quyền tại server.
2. `Controllers/KhachThueController.cs`: thiếu PUT sửa và xóa mềm; `Models/KhachThue.cs` chưa có IsDeleted/DeletedAt. Cần bổ sung các endpoint/DTO ở trên, validate/unique độc lập. DELETE trong API đi kèm ZIP đang xóa vật lý nên client không sử dụng để giả xóa mềm.
3. Còn thiếu dashboard và một số endpoint cho menu bản chốt. Cần đối chiếu controller bản chốt, không lấy Tuần 9.
4. `Models/Klcn052QuanLyPhongTroContext.cs`: OnConfiguring hardcode SQL và ghi đè cấu hình DI; cần dùng DefaultConnection thực sự. Backend cần lỗi JSON/health khi SQL không sẵn sàng.
5. Database đang dùng thiếu `GiaHanHopDong.GiaThueCu`; chủ database cần duyệt schema/script nâng cấp hiện có. Không sửa schema hoặc API trong nhánh WinForms này.

Nhánh này phục vụ đối chiếu phần client. Tích hợp đầy đủ, đặc biệt thêm/sửa/xóa mềm với SQL thật, cần kiểm thử lại sau khi backend đáp ứng hợp đồng.
