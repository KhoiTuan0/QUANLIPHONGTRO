# Hồ sơ bàn giao phần mềm quản lý phòng trọ

Cập nhật: 29/10/2026. Phạm vi: **QLPT.WPF và QLPT.API**. WPF là ứng dụng desktop thay thế WinForms. Không bao gồm phát triển hoặc nghiệm thu mobile, WinForms.

Hồ sơ bàn giao phần mềm khác với **biên bản bàn giao phòng/tài sản**. Biên bản bàn giao là một biểu mẫu nghiệp vụ được xuất từ phần mềm; bộ hồ sơ này hướng dẫn người nhận cài đặt, sử dụng và tiếp quản phần desktop/API.

## Danh mục

Bổ sung 30/10: [SQL chốt hiện tại](SQL_CHOT/README.md) và [hai nghiệp vụ API/WPF để mobile tích hợp](API_MOBILE_20261029.md). Đợt bổ sung có kiểm tra riêng trên bản sao; phần nghiệm thu giao diện/triển khai tổng thể vẫn tách riêng.

| Tài liệu | Nội dung |
| --- | --- |
| [Biểu mẫu và cách xuất/in](BIEU_MAU.md) | Hợp đồng thuê phòng và biên bản nhận/trả phòng; dữ liệu tự điền, phần cần bổ sung, cách lưu PDF |
| [Cài đặt và vận hành](CAI_DAT_VAN_HANH.md) | CSDL, chuỗi kết nối, chạy từ mã nguồn, tạo bản phát hành, bảo quản cấu hình |
| [Hướng dẫn sử dụng và phân quyền](SU_DUNG_PHAN_QUYEN.md) | Chủ trọ, quản lý cơ sở, sao lưu; luồng khách thuê, hợp đồng, tài sản và OCR |
| [Phiếu ghi nhận bàn giao](PHIEU_BAN_GIAO.md) | Danh mục thực tế, thông tin phiên bản, người giao/nhận, phần còn chờ xác nhận |

## Thành phần cần giao

- Mã nguồn `QLPT.API`, `QLPT.WPF`, `ocr-data`, `global.json` và hồ sơ này. Giữ nguyên lịch sử thay đổi trong kho Git nếu bàn giao kho nguồn.
- Bản phát hành API và WPF: tạo theo hướng dẫn khi chuẩn bị triển khai; giao cả thư mục đầu ra, không chỉ riêng `.exe`.
- Bản sao lưu SQL `.bak` đúng CSDL và thời điểm bàn giao, cùng danh sách migration đã áp dụng. Giao riêng qua nơi lưu trữ được phép; không nhúng vào tài liệu công khai.
- Cấu hình máy đích, tài khoản nhận bàn giao và khóa OCR (nếu sử dụng): chuyển riêng, không ghi mật khẩu hoặc khóa vào hồ sơ.

## Trạng thái hồ sơ

Biểu mẫu trong mã nguồn đã được bổ sung nội dung và quy tắc trình bày A4. Tài liệu cài đặt và vận hành được chuẩn bị cho phạm vi desktop/API. **Chưa thực hiện kiểm thử mới, kiểm tra bản in thực tế hoặc triển khai máy khác trong đợt hoàn thiện hồ sơ này**, theo yêu cầu để phần kiểm thử sau.

Kết quả kiểm tra trước đây tham khảo tại [TEST_REPORT_API_WPF.md](../TEST_REPORT_API_WPF.md); không coi đó là kết quả nghiệm thu phiên bản mới hoặc máy đích. Chưa ký xác nhận nghiệm thu chỉ dựa trên việc có hồ sơ.

## Giới hạn cần biết

- Hợp đồng xuất ra phản ánh dữ liệu hiện tại, không thay thế bản hợp đồng đã ký trước đó. Lưu riêng bản ký và phụ lục.
- Chưa có ký điện tử hoặc kho lưu trữ bản hợp đồng đã ký trong WPF.
- OCR là gợi ý; người nhập kiểm tra và xác nhận trước khi lưu. Dịch vụ AI cần cấu hình riêng và có thể không khả dụng.
- Hồ sơ cư trú được xác nhận nội bộ trong ứng dụng; không phải bằng chứng đã nộp cơ quan nhà nước.
- Chức năng phía mobile và việc chạy thành công trên máy khác chưa được xác nhận bởi hồ sơ này.
