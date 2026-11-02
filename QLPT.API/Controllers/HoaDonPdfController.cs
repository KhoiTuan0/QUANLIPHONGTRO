using System.Reflection.Metadata;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Security.Claims;

namespace QLPT.API.Controllers
{
    [ApiController]
    [Route("api/HoaDon")]
    [Authorize]
    public class HoaDonPdfController : ControllerBase
    {
        private readonly Klcn052QuanLyPhongTroContext _context;

        public HoaDonPdfController(
            Klcn052QuanLyPhongTroContext context)
        {
            _context = context;
        }

        [HttpGet("{id:int}/pdf")]
        public async Task<IActionResult> ExportPdf(int id)
        {
            var hoaDon = await _context.HoaDons
                .AsNoTracking()
                .Include(h => h.ChiTietHoaDons)
                .Include(h => h.MaHopDongNavigation)
                    .ThenInclude(h => h.MaPhongNavigation)
                .Include(h => h.MaHopDongNavigation)
                    .ThenInclude(h => h.MaKhachDaiDienNavigation)
                .FirstOrDefaultAsync(h => h.MaHoaDon == id);

            if (hoaDon == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy hóa đơn."
                });
            }

            // Chủ trọ được xuất tất cả hóa đơn.
            // Khách thuê chỉ được xuất hóa đơn của mình.
            if (!User.IsInRole("Admin"))
            {
                var rawId = User.FindFirst("MaKhach")?.Value;

                if (!int.TryParse(rawId, out int maKhach))
                {
                    return Forbid();
                }

                if (hoaDon.MaHopDongNavigation
                    .MaKhachDaiDien != maKhach)
                {
                    return Forbid();
                }
            }

            var hopDong = hoaDon.MaHopDongNavigation;

            var tenPhong = hopDong.MaPhongNavigation
                ?.TenPhong ?? $"Phòng #{hopDong.MaPhong}";

            var tenKhach = hopDong
                .MaKhachDaiDienNavigation
                ?.HoTen ?? "Khách thuê";

            var culture = new CultureInfo("vi-VN");

            string Tien(decimal? value)
            {
                return (value ?? 0).ToString(
                    "#,##0", culture) + " đ";
            }

            var tongTien = hoaDon.TongTien ?? 0;
            var daThanhToan = hoaDon.DaThanhToan ?? 0;
            var conLai = hoaDon.ConLai
                ?? (tongTien - daThanhToan);

            var trangThai = hoaDon.TrangThai
                ?? "Chưa xác định";

            // Font Unicode để hiển thị tiếng Việt.
            // Đặt file DejaVuSans.ttf trong thư mục Fonts.
            var fontPath = Path.Combine(
                AppContext.BaseDirectory,
                "Fonts",
                "DejaVuSans.ttf"
            );

            if (!System.IO.File.Exists(fontPath))
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Thiếu font Fonts/DejaVuSans.ttf."
                });
            }

            byte[] pdf;

            try
            {
                QuestPDF.Settings.License = LicenseType.Community;
                using var fontStream =
                    System.IO.File.OpenRead(fontPath);
                QuestPDF.Drawing.FontManager.RegisterFont(fontStream);

                pdf = QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(35);
                        page.DefaultTextStyle(x =>
                            x.FontFamily("DejaVu Sans")
                             .FontSize(10));

                        page.Header().Column(column =>
                        {
                            column.Item()
                                .AlignCenter()
                                .Text("HÓA ĐƠN TIỀN THUÊ PHÒNG")
                                .Bold()
                                .FontSize(17)
                                .FontColor("#4338CA");

                            column.Item()
                                .PaddingTop(5)
                                .AlignCenter()
                                .Text(
                                    $"Mã hóa đơn: #{hoaDon.MaHoaDon}"
                                );

                            column.Item()
                                .PaddingTop(5)
                                .AlignCenter()
                                .Text(
                                    $"Kỳ thanh toán: " +
                                    $"{hoaDon.Thang:00}/{hoaDon.Nam}"
                                );
                        });

                        page.Content()
                            .PaddingVertical(25)
                            .Column(column =>
                            {
                                column.Spacing(12);

                                column.Item().Text(
                                    $"Phòng: {tenPhong}"
                                ).Bold();

                                column.Item().Text(
                                    $"Khách thuê: {tenKhach}"
                                );

                                column.Item().Text(
                                    $"Mã hợp đồng: " +
                                    $"#{hoaDon.MaHopDong}"
                                );

                                column.Item().LineHorizontal(1)
                                    .LineColor("#CBD5E1");

                                column.Item().Text(
                                    "CHI TIẾT CÁC KHOẢN THU"
                                ).Bold().FontSize(12);

                                column.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(4);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(2);
                                    });

                                    void CellHeader(string text)
                                    {
                                        table.Cell()
                                            .Background("#EEF2FF")
                                            .Padding(6)
                                            .Text(text)
                                            .Bold()
                                            .FontSize(9);
                                    }

                                    CellHeader("Nội dung");
                                    CellHeader("SL");
                                    CellHeader("Đơn giá");
                                    CellHeader("Thành tiền");

                                    foreach (var ct in
                                        hoaDon.ChiTietHoaDons)
                                    {
                                        table.Cell().Padding(6)
                                            .Text(ct.NoiDung ?? "");

                                        table.Cell().Padding(6)
                                            .Text(
                                                ct.SoLuong.ToString()
                                            );

                                        table.Cell().Padding(6)
                                            .Text(Tien(ct.DonGia));

                                        table.Cell().Padding(6)
                                            .Text(Tien(ct.ThanhTien));
                                    }
                                });

                                column.Item().LineHorizontal(1)
                                    .LineColor("#CBD5E1");

                                column.Item().AlignRight()
                                    .Text(
                                        $"Tổng tiền: {Tien(tongTien)}"
                                    ).Bold().FontSize(12);

                                column.Item().AlignRight()
                                    .Text(
                                        $"Đã thanh toán: " +
                                        $"{Tien(daThanhToan)}"
                                    );

                                column.Item().AlignRight()
                                    .Text(
                                        $"Còn lại: {Tien(conLai)}"
                                    ).Bold();

                                column.Item().Text(
                                    $"Trạng thái: {trangThai}"
                                );

                                column.Item().PaddingTop(20)
                                    .Text(
                                        "Hóa đơn được xuất từ " +
                                        "hệ thống quản lý phòng trọ."
                                    )
                                    .FontSize(9)
                                    .FontColor("#64748B");
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(
                                $"Ngày xuất: " +
                                $"{DateTime.Now:dd/MM/yyyy HH:mm}"
                            )
                            .FontSize(9);
                    });
                }).GeneratePdf();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Không thể tạo PDF: {ex.Message}"
                });
            }

            return File(
                pdf,
                "application/pdf",
                $"HoaDon_{hoaDon.MaHoaDon}.pdf"
            );
        }
    }
}
