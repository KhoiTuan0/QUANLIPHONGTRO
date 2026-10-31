# Kiểm tra WPF với dữ liệu giả

Chạy trên Windows có .NET SDK 8 và các package của dự án đã restore. Bộ kiểm tra tạo control WPF ở STA, dùng HTTP handler giả, không mở cửa sổ và không ghi vào API/CSDL thật.

```powershell
dotnet build QLPT.WPF/QLPT.WPF.csproj --no-restore -p:UseAppHost=false -p:MSBuildEnableWorkloadResolver=false -o QLPT.WPF/bin/Validation
dotnet restore tests/QLPT.WPF.Checks/PickerValidation.csproj --source QLPT.WPF/bin/Validation -p:MSBuildEnableWorkloadResolver=false
dotnet run --project tests/QLPT.WPF.Checks/PickerValidation.csproj --no-restore -p:MSBuildEnableWorkloadResolver=false
```

Kiểm tra 100 hồ sơ, tìm tên không dấu/CCCD/SĐT, không lưu người/mã không tồn tại, danh sách rỗng, lỗi và tải lại, lọc phòng đủ điều kiện, nhận phòng đúng khách/cọc/giá, chọn nhiều người nhận và giữ lựa chọn qua tìm kiếm, không tự gửi cho tất cả khi chưa chọn, và tải XAML các hộp thoại đã sửa.
