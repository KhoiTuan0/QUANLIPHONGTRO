# Kiểm tra API + WPF — 14/10/2026

Giữ giao diện hiện tại. Đợt kiểm tra này chỉ xử lý các lỗi cụ thể và kiểm chứng hoạt động của API + WPF. Không sửa hoặc build WinForms/mobile trong đợt này; các thay đổi từ đợt trước được giữ nguyên.

## Kết quả

| Kiểm tra | Kết quả |
| --- | --- |
| Build API và WPF | Thành công, 0 lỗi biên dịch |
| `tests/QLPT.API.Checks` | 229 kiểm tra validation, quyền truy cập, ảnh và tính tiền đạt |
| `tests/QLPT.WPF.Checks` | 66 kiểm tra lựa chọn, phiên đăng nhập, tải dữ liệu và nhập tiền đạt |
| `tests/QLPT.Integration.Checks` | 83 kiểm tra HTTP/SQL thật đạt tại `http://localhost:5180` |
| `tests/QLPT.WPF.LiveChecks` | 29 kiểm tra WPF kết nối API/SQL thật đạt, gồm 22 trang quản trị |
| Giao diện tạo hợp đồng | Đã mở và kiểm tra tìm tên không dấu, danh sách khách có sẵn, tên + CCCD/SĐT |
| CSDL demo gốc | Số lượng bản ghi của 29 bảng giữ nguyên trước/sau test |

## Các luồng đã chạy với SQL Server thật

Test ghi dữ liệu dùng bản sao `QLPT_Test_20261014_b4a1ef0d` của CSDL demo, không thay đổi cấu hình kết nối đã lưu và không chạy migration.

- Đăng nhập, cookie phiên, đăng xuất; khách không được đọc hồ sơ/hợp đồng/hóa đơn của người khác.
- Đặt phòng, phòng được giữ chỗ, chặn đặt trùng, nhận phòng đúng khách và phòng, tạo cư trú và điện/nước; tiền cọc không thu hai lần.
- Chặn thay trạng thái phòng để bỏ qua hợp đồng; chặn hóa đơn ngoài kỳ hợp đồng và kỳ không hợp lệ.
- Chặn lập hóa đơn thiếu chỉ số; tính đúng 2.075.000 đồng từ tiền phòng 2.000.000, điện 35.000 và nước 40.000; chặn lập trùng và sửa chỉ số của kỳ đã phát hành.
- Chặn thu vượt nợ; thu một phần cập nhật dư nợ đúng. Hai yêu cầu đồng thời thu 80.000 trên hóa đơn 100.000 chỉ có một lần thành công, sổ thanh toán và dư nợ nhất quán.
- Yêu cầu sửa chữa đúng quyền; cập nhật hoàn thành lặp không tạo thêm phiếu chi; khách chỉ xác nhận khi sửa xong.
- Báo trả phòng, chặn báo lặp, gia hạn, bàn giao, thanh lý; server tính nợ 1.575.000 và hoàn thực tế 375.000 sau khi trừ nợ/phạt từ cọc 2.000.000. Thanh lý lặp bị chặn và phòng được trả về trống.
- Ba loại lịch sử khách, bốn loại báo cáo, dashboard; sao lưu SQL thật bằng COPY_ONLY/CHECKSUM và VERIFYONLY.
- WPF đăng nhập và đăng xuất qua API thật, thông tin khách/hợp đồng/hóa đơn hiển thị tên phòng, tất cả 22 trang quản trị tải không báo lỗi.

## Sửa lỗi trong đợt test này

- Tính phí dịch vụ đúng khoảng ngày sử dụng: đăng ký tương lai không bị tính vào tháng trước, dịch vụ đã ngừng vẫn có mặt trong kỳ sử dụng trước đó; phí cố định không bị nhân đôi khi ngừng/đăng ký lại trong cùng tháng.
- Kiểm tra kỳ hóa đơn nhập tay/OCR và cập nhật số dư thu tiền nguyên tử trong transaction, tránh hai yêu cầu thu dùng chung dư nợ cũ.
- WPF chỉ hoàn tất đăng nhập khi có cookie phiên thật; đăng xuất quay về đăng nhập trong cùng tiến trình, chạy bằng DLL vẫn hoạt động.
- Trang thông tin khách dùng số lượng yêu cầu cố định, cập nhật lại quyền sở hữu hợp đồng và bỏ phản hồi muộn của phiên cũ.
- Nhập tiền không biến số âm/rác thành số dương; giữ hộp thoại trong khi lưu để kết quả không bị bỏ mất do người dùng đóng sớm.

## Phạm vi và cấu hình

Các kết quả trên xác nhận những kịch bản đã chạy, không phải tuyên bố mọi tổ hợp đầu vào đều đã kiểm thử. Không nâng phiên bản thư viện hoặc đổi thiết kế giao diện.

WPF vẫn có cảnh báo tương thích thư viện OpenTK/SkiaSharp và cảnh báo API LiveCharts/thuộc tính ConfirmDialog có sẵn. NuGet không truy cập được dịch vụ kiểm tra lỗ hổng trong một số lượt restore; build và các test vẫn đạt. Không đổi thư viện trong đợt này.

Sao lưu trong test dùng thư mục mặc định của SQL Server qua cấu hình riêng cho tiến trình test. Bản hiện tại ưu tiên `Backups:SqlServerDirectory` nếu được cấu hình; nếu không thì dùng thư mục sao lưu mặc định do SQL Server báo. Tài khoản dịch vụ SQL vẫn cần quyền ghi.

Chạy demo với cấu hình gốc:

```powershell
dotnet run --project QLPT.API/QLPT.API.csproj --launch-profile http
dotnet run --project QLPT.WPF/QLPT.WPF.csproj
```

Hai lệnh chạy trong hai terminal riêng. API giữ cổng 5180.

Sau test đã đóng cửa sổ/tiến trình kiểm thử và xóa đúng CSDL test riêng. CSDL demo gốc vẫn ONLINE. API đã được khởi động lại bằng cấu hình gốc tại cổng 5180; WPF bình thường đã được mở đến màn hình đăng nhập. Bản sao lưu COPY_ONLY dùng làm đầu vào test được giữ trong thư mục sao lưu SQL Server.

## Kiểm tra bổ sung 18/10/2026

- Đối chiếu đề cương/master map/roadmap và ba file SQL với mã nguồn hiện tại; không chạy SQL trên CSDL demo. Việc gộp phí vi phạm vào hóa đơn chưa đủ căn cứ nghiệp vụ và rủi ro thu trùng, nên giữ luồng hiện tại.
- Đối chiếu commit mới của thành viên khác: commit đó chỉ sửa `mobileui`, không có mã API/WPF để gộp vào bản demo này.
- WPF từng báo `DllNotFoundException: libSkiaSharp` tại cấu hình phông biểu đồ khi khởi động. Bản sửa nạp thư viện native từ thư mục build theo kiến trúc máy và bỏ lệnh tùy chỉnh phông đã lỗi thời. Sau build, mở trực tiếp `QLPT.WPF.exe`, đăng nhập thật tới Dashboard và biểu đồ hiển thị; log lỗi không ghi thêm. Kết quả kiểm thử lại nghiệp vụ ở phần cuối báo cáo.
- Build API và WPF: 0 lỗi. `tests/QLPT.API.Checks`: 229/229 đạt, không kết nối SQL. `tests/QLPT.WPF.Checks`: 77/77 đạt với API giả lập, gồm tạo `MainWindow` và mười trang demo trọng tâm. OCR đạt kiểm tra ảnh số tổng hợp; xem kết quả với ảnh đồng hồ thật bên dưới.
- API thực tại `http://localhost:5180`: `/api/health/live`, `/api/health`, `/swagger/v1/swagger.json` trả 200; health báo kết nối SQL. CSDL demo trên máy này có 7 phòng, chỉ đọc số lượng, không sửa để khớp bản 14 phòng ở máy trường.
- Sau khi người dùng đăng nhập thật, WPF mở Dashboard và hai biểu đồ hiển thị. Trước khi sửa dữ liệu, P102 (mã 2, Cơ sở 1) lưu `Đang thuê` nhưng không có hợp đồng, và p101 (mã 7, chưa gắn cơ sở) lưu `Đã đặt cọc` nhưng không có phiếu đặt phòng còn hiệu lực. Kết quả xử lý ngày 18/10 nằm ở phần cuối báo cáo.
- Kết quả 83 bài HTTP/SQL và 29 bài WPF ở phần đầu là của ngày 14/10; sau đó đã chạy lại trên bản sao mới từ CSDL demo gốc như ghi ở phần cuối. Không restore lên CSDL demo thật.

### Đối chiếu SQL chỉ đọc ngày 18/10/2026

- CSDL demo hiện có 29 bảng, trong đó 27 bảng nghiệp vụ trùng danh sách trong `01_Schema.sql`, thêm `ThuChi` cũ và `sysdiagrams`. Có 32 khóa ngoại, không có khóa ngoại/check constraint bị vô hiệu hoặc không trusted.
- CSDL demo có **0 CHECK constraint**; `01_Schema.sql` tham chiếu có 26 CHECK, 9 UNIQUE constraint, 3 unique index riêng và 14 secondary index. Đặc biệt CSDL hiện không có unique index chặn hai hợp đồng hiệu lực cùng phòng, hóa đơn trùng kỳ, hoặc chỉ số điện/nước trùng kỳ. File `KLCN052_QuanLyPhongTro_MASTER.sql (3).sql` chỉ có 23 bảng, là thiết kế cũ; không dùng thay cho schema hiện tại. `themrangbuoc.sql` chưa được áp dụng, cũng chưa cần cho luồng hóa đơn hiện tại.
- Dữ liệu kiểm tra **không có** hóa đơn sai tổng/đã thu/còn nợ, thanh toán lệch tổng đã thu, chi tiết hóa đơn lệch tổng, hóa đơn hoặc chỉ số trùng kỳ, hợp đồng hiệu lực trùng phòng, hợp đồng hiệu lực sai khoảng ngày, hay cư trú đang ở thuộc hợp đồng đã thanh lý. Hai lệch trạng thái phòng nêu trên là kết quả bất thường tìm thấy trước khi sửa.
- 229 bài kiểm tra API không dùng SQL; 83 bài tích hợp trước đó chạy trên một CSDL test riêng và chủ yếu kiểm tra qua API. Kết quả đạt không chứng minh các constraint của `01_Schema.sql` đã có trên CSDL demo. Không chạy các script SQL tham chiếu; chỉ cập nhật có điều kiện hai trạng thái phòng như ghi bên dưới.

### Ảnh đồng hồ điện do người dùng cung cấp ngày 18/10/2026

- Chạy `MeterOcr.Read` trên cả hai ảnh chụp toàn cảnh: đều không đưa ra chỉ số gợi ý; Tesseract không nhận ra dòng chữ số khi mặt đồng hồ chỉ chiếm một phần ảnh. Kiểm tra này chỉ đọc ảnh, không gửi dữ liệu vào API/SQL.
- SQL đã có các cột lưu chỉ số cũ/mới, chỉ số OCR gợi ý, chỉ số người dùng xác nhận và đường dẫn ảnh. Ảnh chụp không cho thấy nhu cầu đổi cấu trúc bảng hoặc dữ liệu. Trên mặt công tơ, chữ số màu đỏ ở vị trí `1/10 kWh`; không được ghép nó vào cuối dãy số đen thành số nguyên lớn gấp 10 lần. WPF cho phép nhập tay và bắt buộc đối chiếu ảnh trước khi lưu; khi demo OCR, cần dùng ảnh cắt sát dãy chữ số và vẫn kiểm tra kết quả bằng mắt. Chưa xác nhận OCR đọc đúng hai ảnh sau khi cắt.

### Sửa lỗi kiểm thử WPF và trạng thái phòng demo ngày 18/10/2026

- Hộp thoại `Lỗi ứng dụng` xuất hiện khi **bài kiểm thử WPF** nạp `QLPT.WPF.dll` cũ từ `bin/Validation`; log của lần tái hiện ghi `DllNotFoundException: libSkiaSharp` tại lệnh `HasGlobalSKTypeface` đã bị loại khỏi mã WPF hiện tại. Đổi test sang `ProjectReference` để luôn build mã hiện tại, và tách `App_Startup` khỏi bài kiểm thử không giao diện để nó không mở thêm cửa sổ đăng nhập. Bỏ cùng lệnh phông lỗi thời khỏi bài kiểm tra live cũ. Chạy `dotnet run --project tests/QLPT.WPF.Checks/PickerValidation.csproj -c Validation`: 77/77 đạt, không xuất hiện hộp thoại. Dùng cấu hình `Validation` khi WPF Debug đang mở vì Windows khóa file DLL/EXE của tiến trình đang chạy; live checks cũ build 0 lỗi nhưng chưa chạy vì fixture SQL riêng trước đây không còn.
- Đọc lại SQL trước khi sửa: phòng mã 2 và 7 đều không có hợp đồng hiệu lực, phiếu đặt cọc còn hiệu lực hoặc cư trú `Đang ở`. Một transaction có điều kiện chỉ đổi `Phong.TrangThai` của mã 2 từ `Đang thuê` và mã 7 từ `Đã đặt cọc` sang `Trống`; không xóa/thêm phòng, không chạy file SQL mẫu. Đọc lại SQL: 7 phòng gồm 6 `Trống`, 1 `Đang thuê`. Tải lại trang Phòng trọ và Dashboard trong WPF thật: hai dòng đã hiện `Trống`, Dashboard hiện 7 tổng/6 trống/1 đang thuê/1 hợp đồng hiệu lực, không báo lỗi.
- API `PutPhong` trước đây kiểm tra trùng tên/cơ sở kể cả khi chỉ đổi trạng thái, nên phòng mã 7 (tên cũ trùng với mã 6 dưới collation SQL) không thể sửa trạng thái qua WPF. Điều chỉnh để chỉ chặn trùng khi tên hoặc cơ sở thực sự đổi. `tests/QLPT.API.Checks` đạt 229/229 sau sửa; API chạy hiện tại cần khởi động lại để nhận mã mới.

### Kiểm thử lại luồng demo trên bản sao CSDL gốc — 18/10/2026

- Tạo bản sao riêng `QLPT_Test_20261018_a4b631e9` từ bản sao lưu `COPY_ONLY` có `CHECKSUM` của `KLCN052_QuanLyPhongTro`; `RESTORE VERIFYONLY` đạt. Bản sao bắt đầu từ đúng trạng thái 7 phòng (6 trống, 1 đang thuê). API kiểm thử dùng cổng 5181 và chuỗi kết nối chỉ trỏ tới bản sao; mã thử kiểm tra dấu hiệu riêng của bản sao trước khi ghi. Không chạy `01_Schema.sql`, `02_SampleData.sql`, `themrangbuoc.sql` hoặc restore lên CSDL gốc.
- `tests/QLPT.Integration.Checks`: **83/83** bước HTTP/SQL đạt trên bản sao, bao gồm đăng nhập/phân quyền, đặt và nhận phòng, hợp đồng/cọc/cư trú, điện nước/hóa đơn/thu một phần và thu đồng thời, sửa chữa, báo trả/gia hạn/bàn giao/thanh lý, báo cáo và tạo/xác minh bản sao lưu. Chạy thêm chế độ `extra`: **30/30** bước đạt cho vi phạm/xử lý lặp, tài sản gán phòng và chặn gán trùng, đăng ký/ngừng dịch vụ và giữ giá/ngày kết thúc, gửi thông báo đến đúng tài khoản. Xử lý vi phạm không tạo phiếu thu khi chưa nhận tiền.
- `tests/QLPT.WPF.LiveChecks`: **29/29** đạt với API 5181 và bản sao, gồm đăng nhập/đăng xuất, tên khách/phòng trên dữ liệu hợp đồng và hóa đơn, tải 22 trang quản trị. Kiểm tra hồi quy riêng: **229/229** API và **77/77** WPF đạt; hai bộ này không ghi SQL thật. Build API/WPF cấu hình `Validation`: 0 lỗi biên dịch. Cảnh báo NU1900 do dịch vụ NuGet không truy cập được; WPF vẫn có cảnh báo tương thích OpenTK/SkiaSharp và tên `ConfirmDialog.Icon`.
- CSDL demo gốc chỉ được `SELECT`: 7 phòng = 6 trống + 1 đang thuê, 1 hợp đồng hiệu lực, 3 hóa đơn (2 đã trả đủ, 1 còn nợ). Không tìm thấy phòng/hợp đồng lệch trạng thái, hợp đồng hiệu lực trùng phòng, chỉ số/hóa đơn trùng kỳ, số dư hóa đơn sai hoặc thanh toán lệch tổng đã thu. Truy vấn lại sau các bài ghi trên bản sao vẫn là 7/6/1; bảng vi phạm, đăng ký dịch vụ và thông báo của CSDL gốc vẫn 0 bản ghi. Không có bằng chứng cần sửa SQL gốc vì chênh lệch 7/14 phòng giữa hai máy.
- Tiến trình API 5180 cũ được phát hiện chạy trước lần sửa mã gần nhất. Đã dừng riêng tiến trình đó, build lại mã hiện tại và chạy API mới ở `http://localhost:5180` với CSDL gốc. `/api/health` trả 200, `database: connected`; `/swagger/v1/swagger.json` trả 200. API kiểm thử 5181 và cửa sổ WPF kiểm thử đã đóng. WPF Debug cũ đã được đóng; WPF bản `Validation` mới đã mở bình thường tới màn hình đăng nhập, sẵn sàng để người dùng đăng nhập demo.
- Theo yêu cầu dừng phần hiệu ứng rê chuột, chỉ giữ thay đổi đã xác nhận trước đó `TooltipPosition="Hidden"` để biểu đồ vòng không đè chữ; không đưa thêm hàng thông tin hover mới vào WPF. Mã nguồn WPF build lại 0 lỗi sau khi bỏ phần thử nghiệm.
- Giới hạn: 29 bài WPF live kiểm tra đăng nhập/tải trang chứ chưa bấm lưu mọi hộp thoại WPF trên SQL thật; 83+30 bài API/SQL kiểm tra các kịch bản tiêu biểu chứ không bao phủ mọi tổ hợp dữ liệu. OCR hai ảnh đồng hồ toàn cảnh vẫn không đưa ra gợi ý số đáng tin cậy. Bản sao CSDL test được giữ riêng để có thể kiểm tra lại, không dùng làm CSDL demo.

### Kiểm thử lại sau bổ sung nghiệp vụ API/WPF — 24/10/2026

- OCR WPF: hai ảnh công tơ thật tiếp tục không đọc được khi đưa nguyên khung. Đã thêm khoanh vùng số đen trong WPF, chọn số ô chữ số, phóng lớn/nhị phân hóa từng ô và chỉ nhận gợi ý khi đủ mọi ô với độ tin cậy tối thiểu. Trên ảnh thứ nhất, vùng `x=355,y=342,w=145,h=40` cho `08424` (giá trị `8424`); ảnh thứ hai với vùng `x=738,y=600,w=200,h=42` vẫn không đạt và WPF yêu cầu nhập tay, đối chiếu ảnh. Đây là giới hạn thực tế, không tính hai ảnh thật là 2/2 OCR đạt. Bài ảnh số tổng hợp vẫn đạt; WPF build và 95 bài hành vi WPF đạt sau thay đổi.

- 26/10/2026: WPF thử OCR tại máy trước, rồi gửi vùng số và ảnh tổng thể thu nhỏ cho API đọc bằng OpenAI khi chưa chắc chắn. API chưa dùng CSDL ở bước nhận diện; chỉ endpoint lưu chỉ số mới ghi SQL. Cả hai ảnh thật đã tạo payload PNG/JPEG hợp lệ dưới giới hạn API (ảnh 1: 165.549/110.017 byte; ảnh 2: 106.233/83.639 byte). Ảnh 1 vẫn cho `8424` tại máy, ảnh 2 vẫn từ chối đọc tại máy. API/WPF build 0 lỗi; 253 bài kiểm tra API, gồm key thiếu, kết quả AI thiếu chữ số và kết quả có chữ ngoài dãy số, đạt. Chưa chạy ảnh 2 qua dịch vụ OpenAI thật vì máy API chưa có `OPENAI_API_KEY`; không tính là OCR ảnh thật 2/2 đạt.

- Tạo bản sao riêng `QLPT_Test_20261023_4f0b7a2c` từ bản sao lưu `COPY_ONLY` có `CHECKSUM` của `KLCN052_QuanLyPhongTro`; `RESTORE VERIFYONLY` đạt. API kiểm thử chỉ chạy ở cổng 5181 với chuỗi kết nối trỏ đến bản sao. Không chạy kịch bản ghi trên CSDL demo gốc.
- Cập nhật bài WPF live theo phân quyền hiện tại: tài khoản `Customer` bị chặn trên desktop, còn `Admin` đăng nhập được. **26/26** kiểm tra đạt, gồm tải 22 trang qua API và đăng xuất xóa phiên.
- Cập nhật dữ liệu kiểm thử cơ sở cho đủ địa chỉ/số điện thoại và kiểm tra rằng tài sản không được gán trực tiếp vào phòng đang thuê. Kịch bản HTTP/SQL chính đạt **85/85** bước; kịch bản bổ sung đạt **31/31** bước trên bản sao. Bài kiểm tra API độc lập **245/245**, WPF độc lập **95/95**, build API/WPF 0 lỗi.
- Giới hạn kiểm thử WPF live: bài chạy tự động xác nhận đăng nhập, tải trang và đăng xuất; chưa bấm lưu từng hộp thoại. OCR ảnh toàn cảnh đồng hồ cần đối chiếu thủ công. Những thay đổi dữ liệu của bài tích hợp chỉ nằm trong bản sao SQL. Đã xóa CSDL kiểm thử mới và dừng API cổng 5181 sau khi kiểm tra; file `.bak` COPY_ONLY dùng để tạo bản sao vẫn nằm trong thư mục sao lưu của SQL Server vì tài khoản Windows hiện tại không có quyền xóa tệp ở đó.

## Cập nhật OCR ngày 26/10/2026

- Tích hợp Gemini qua API làm phương án đọc khi OCR tại máy chưa chắc chắn; key chỉ nằm ở máy chạy API. Gemini `gemini-3.6-flash` trả `08424` từ ảnh thật thứ nhất và `08867` từ ảnh thật thứ hai, với vùng khoanh số tương ứng `355,342,145,40` và `738,600,200,42`. Đây là kết quả dịch vụ trực tiếp, chưa phải kiểm thử bấm lưu trọn luồng WPF. Người dùng vẫn phải đối chiếu ảnh trước khi xác nhận; bước OCR không ghi SQL.
- API/WPF build thành công; 259 bài kiểm tra API đạt, gồm kiểm tra phản hồi Gemini và OpenAI giả lập cùng đường đi qua controller OCR. 95 bài kiểm tra hành vi WPF đạt. Các ghi nhận cũ ở trên phản ánh thời điểm **trước** khi cấu hình Gemini. Chưa bấm hoàn tất đọc ảnh trên WPF đang mở, nên không tính bước giao diện trực tiếp là đã đạt.

## Nhập lô và điều chuyển tài sản ngày 27/10/2026

- API/WPF build 0 lỗi; 97 bài kiểm tra hành vi WPF đạt, gồm tải được hộp thoại nhập kho và chuyển kho. Chủ trọ nhập một mẫu tài sản với số lượng 50 để tạo 50 mã hiện vật riêng trong kho, xem tồn kho theo mẫu và chuyển nhiều hiện vật vào phòng trong một lần. Giá mua là giá mỗi chiếc; mã và lịch sử theo từng hiện vật qua các lần điều chuyển.
- Bộ `QLPT.ManagerAsset.Checks` chạy trên bản sao `QLPT_DemoPrep_20261019` trong giao dịch rollback: nhập 50 tạo 50 mã khác nhau, chuyển 20 còn 30 trong kho, mỗi chiếc chuyển có lịch sử riêng; yêu cầu chuyển 31 chiếc tiếp theo bị từ chối. Kiểm tra quyền cho thấy Manager không được nhập, xuất hoặc xem tổng kho chung. Không ghi 50 giường thử vào CSDL demo gốc.
- Chưa kiểm thử thao tác bấm nút trên WPF bằng tay trong phiên này; cần khởi động lại API/WPF để nạp mã mới trước khi thử giao diện.

## Kiểm tra OCR tiếp ngày 27/10/2026

- Gọi Gemini thật với đúng vùng năm ô số đen của hai ảnh công tơ: ảnh thứ nhất trả `08424`, ảnh thứ hai trả `08867`; cả hai đều `Success`. Đây là kết quả đọc trực tiếp từ ảnh, không ghi chỉ số vào SQL.
- Sửa WPF để gợi ý và ô **Chỉ số mới** giữ số `0` ở đầu dãy năm chữ số. **99 bài kiểm tra hành vi WPF đạt**, gồm trường hợp OCR tại máy chưa chắc chắn thì chuyển qua API giả lập, hiện đủ chữ số và bật nút dùng gợi ý. API đang chạy tại cổng 5180 trả `status: ok`, `database: connected`.
- Người dùng đã thử trực tiếp trên WPF với ảnh công tơ thứ hai: chọn ảnh, khoanh sát năm số nền đen (bỏ ô đỏ), bấm **Đọc số từ ảnh**. Giao diện hiện **“AI gợi ý: 08867”** và giữ số `0` đầu; ảnh chụp màn hình xác nhận nút **Dùng số gợi ý** được bật. Chưa bấm **Thêm**, nên bước này không tạo chỉ số hoặc hóa đơn trong CSDL demo. Đây là xác nhận trọn luồng đọc/gợi ý trên giao diện thật; luồng lưu vẫn chưa thử với ảnh này.

## Kiểm tra tài sản trên WPF thật ngày 27/10/2026

- Chạy lại bộ `QLPT.ManagerAsset.Checks` trên bản sao `QLPT_DemoPrep_20261019` trong giao dịch rollback: 50 mã hiện vật riêng khi nhập 50 chiếc, chuyển 20 chiếc còn 30 trong kho, ghi lịch sử điều chuyển cho từng chiếc, tổng tồn đúng và không xuất vượt tồn. Các kiểm tra quyền theo cơ sở, lịch sử sửa chữa và ngày/giá mua cũng đạt. Không nhập lô thử vào CSDL demo.
- Lần đầu mở trang Tài sản trên WPF thật, danh sách trống vì tiến trình API cũ trả HTTP 405 cho `GET /api/TaiSan/ton-kho`. Đã dừng và khởi động lại API trong Visual Studio để nạp mã mới; sau khi bấm **Tải lại**, WPF hiển thị 4 mẫu, 10 hiện vật và số tồn/trong phòng đúng dữ liệu demo. Hộp **Nhập kho** nhận số lượng `50` trong một lần; hộp **Chuyển hiện vật từ kho** hiện đúng mẫu, tồn kho và phòng đã chọn; cả hai hộp được hủy trước khi ghi.
- WPF mở được lịch sử hiện vật `TS00000003`, thấy riêng sự kiện sửa chữa và kiểm kê, hiển thị một lần sửa. Người dùng xác nhận đã tự bấm **Lưu kiểm kê** cho hiện vật này; dòng kiểm kê ngày 27/10 trong CSDL demo là thao tác của người dùng và được giữ nguyên. Đã nới cột ngày lịch sử và cột mã hiện vật để bớt cắt chữ. Build WPF thành công, 99 bài kiểm tra hành vi WPF đạt; thay đổi độ rộng cột cần khởi động lại WPF để nhìn thấy nếu Hot Reload chưa áp dụng.

## Phân quyền quản lý cơ sở và giao diện tối ngày 27/10/2026

- Manager được thêm/tiếp nhận khách theo đúng cơ sở được giao, đặt phòng, nhận phòng, tạo/gia hạn/thanh lý hợp đồng của cơ sở; API kiểm tra cả đối tượng lẫn cơ sở, không chỉ ẩn nút WPF. Người đã thuê ở cơ sở A được giữ chỗ tại cơ sở B bằng cùng mã khách; hợp đồng A vẫn hiệu lực cho đến khi kết thúc, và hệ thống chặn ký hợp đồng B nếu A còn hiệu lực. Manager xem và ghi nhận tình trạng hiện vật đang ở phòng của cơ sở mình, nhưng không xem hoặc điều chuyển kho chung, tạo mẫu tài sản, thanh lý hiện vật, cấp tài khoản, xem tài chính toàn hệ thống hay sao lưu. Lịch sử điều chuyển mới có mã phòng và cơ sở để chủ trọ tra lại từng hiện vật.
- Cột `KhachThue.MaCoSoTiepNhan` nullable và chỉ mục đã áp dụng trên bản sao trước, sau đó trên CSDL demo `KLCN052_QuanLyPhongTro`. Đọc lại CSDL demo: 9 hồ sơ khách vẫn nguyên, 0 hồ sơ cũ bị gán cơ sở tiếp nhận tự động. Thao tác kiểm thử tạo/tiếp nhận khách và đặt liên cơ sở chạy trong transaction rollback trên `QLPT_DemoPrep_20261019`, không lưu dữ liệu thử vào bản demo.
- Bộ `QLPT.ManagerAsset.Checks` đạt hết các bước, gồm kiểm tra chặn chéo cơ sở/kho, thêm khách mới và đặt liên cơ sở. API build 0 cảnh báo/0 lỗi; WPF build 0 lỗi, còn cảnh báo tương thích thư viện cũ. Biểu đồ vòng Dashboard được gắn nền động theo theme để tránh nền trắng/chữ nhạt ở dark mode; đã biên dịch, chưa kiểm tra lại bằng ảnh chụp giao diện sau khi khởi động WPF bản mới.

## Kiểm tra giao diện quản lý ngày 28/10/2026

- Người dùng chụp WPF thật: Khách thuê hiện nút thêm/sửa đúng vai trò; Hợp đồng hiện tạo/gia hạn/thanh lý và các nút xuất; biểu đồ vòng ở dark mode có nền và số rõ. Thanh nút Hợp đồng đã tách thành hàng tự xuống dòng ở cửa sổ hẹp; nút Xuất bàn giao được căn ngang với các nút còn lại.
- Ảnh Đặt phòng của Manager hiện thông báo quyền và danh sách trống do `ApiClient` WPF chưa cho `/api/DatPhong` đi qua, dù bộ lọc API đã cho Manager thao tác theo cơ sở. Đã thêm đường dẫn Đặt phòng cùng các đường dẫn đọc Hóa đơn/Bàn giao cần cho xuất biểu mẫu; bộ lọc API vẫn quyết định quyền trên dữ liệu. Kiểm tra WPF giả lập xác nhận Manager mở Đặt phòng và gửi GET thành công, tổng cộng **101 bài đạt**. Sau khi nạp bản mới và đăng nhập `huy04`, người dùng xác nhận dải lỗi đỏ đã hết trên WPF thật.
- Ở trang Tài sản của Manager, cột tồn kho chung trước đây hiện dấu gạch. Bản sửa ẩn cột này và đổi cột số lượng thành **Tại cơ sở**, tính từ các mã hiện vật mà API lọc theo cơ sở quản lý. Khi chưa chọn phòng, hai bảng bên phải để trống theo thiết kế; nút **Xem tất cả hiện vật** sẽ hiện các mã tại cơ sở. WPF build 0 lỗi; người dùng xác nhận cột **Tại cơ sở** đã hiện số trên WPF thật.
# Bổ sung 30/10/2026 — yêu cầu hủy, phạt hóa đơn và SQL bàn giao

- Migration `20261029_huy_dat_phong_va_phat_hoa_don.sql` đã áp dụng trên bản sao trước, rồi trên `KLCN052_QuanLyPhongTro` sau backup COPY_ONLY/CHECKSUM. Cấu hình sqlcmd UTF-8 được dùng để bảo toàn các trạng thái tiếng Việt.
- `tests/QLPT.BookingFine.Checks`: 22 kiểm tra đạt trên `QLPT_DemoPrep_20261019`, tất cả dữ liệu fixture rollback. Bao gồm quyền sở hữu/cơ sở, chặn duyệt của Customer, từ chối không hoàn tiền, gửi lại sau từ chối, hoàn cọc một phần đúng một lần, chặn vượt cọc/duyệt lặp/hủy trực tiếp khi chờ; khoản phạt đúng khách và kỳ, tăng công nợ không thu ảo, chặn tính trùng và hóa đơn đã thu một phần.
- API và WPF build thành công. WPF vẫn có 8 cảnh báo đã giải thích (thư viện tương thích và tên Icon); chưa xác nhận thao tác hai hộp thoại mới trên WPF thật hoặc giao diện mobile.
- Bản SQL full được tạo lại trên database riêng và đối chiếu từng bảng: 34 bảng, 198 dòng, 0 bảng khác dữ liệu; 46 FK, 7 CHECK, 50 index đều khớp. Đối chiếu lại với CSDL chính ngày 30/10 không thấy khác biệt dữ liệu. File giao: `HO_SO_BAN_GIAO/SQL_CHOT/KLCN052_QuanLyPhongTro_CHOT_20261030.sql`.
- Các kết quả này chỉ thuộc thay đổi mới; không tự coi là nghiệm thu triển khai WPF trên máy khác.

## Bổ sung 31/10/2026 — thuê hộ và lịch sử sửa khách

- Người đứng tên có thể đặt thêm phòng còn trống, kể cả cùng cơ sở. Khi lập hợp đồng WPF yêu cầu chọn người ở thực tế; API chỉ ghi người đó vào cư trú và từ chối nếu họ đang cư trú ở hợp đồng hiệu lực khác. Phòng đang thuê/đặt cọc vẫn bị chặn.
- Thay đổi tên, CCCD, ngày sinh, giới tính, SĐT, email hoặc địa chỉ khách tạo các dòng lịch sử giá trị cũ/mới, UTC và tài khoản sửa. CSDL chính đã áp dụng `migrations/20261031_lich_su_sua_khach.sql` sau backup CHECKSUM; kiểm tra INSERT trong transaction rollback để lại 0 dòng thử.
- `tests/QLPT.BookingFine.Checks`: **28/28** đạt trên bản sao `QLPT_DemoPrep_20261019`, tất cả dữ liệu thử rollback. Sáu kiểm tra mới gồm đặt thêm tại cùng cơ sở, chặn cư trú kép, hợp đồng người ký khác người ở, ghi đúng cư trú, cập nhật SĐT và lịch sử trước/sau.
- API build 0 lỗi/0 cảnh báo; WPF build 0 lỗi, còn 8 cảnh báo thư viện/tên Icon đã biết. Chưa thao tác trực tiếp hộp thoại thuê hộ và lịch sử trên WPF đang chạy; cần khởi động lại API/WPF từ mã mới.
- Bản SQL chốt ngày 31/10 phục hồi thành công vào `QLPT_Verify_20261031`: **35 bảng, 202 dòng**, đối chiếu `EXCEPT` hai chiều 0 khác biệt với bản sao nguồn; 48 FK, 7 CHECK, 52 index đều khớp. File giao ở `HO_SO_BAN_GIAO/SQL_CHOT/KLCN052_QuanLyPhongTro_CHOT_20261031.sql`.
