using ClosedXML.Excel;

namespace QLPT.WPF.Services;

/// <summary>
/// Xuất báo cáo ra file .xlsx thật: mỗi bảng một sheet, có tiêu đề, giờ xuất,
/// dòng tiêu đề cột cố định khi cuộn, số tiền là SỐ (cộng/lọc/sắp xếp được trong Excel)
/// và dòng tổng dùng công thức SUM.
/// </summary>
public static class ExcelExporter
{
    public enum Kieu { Chu, So, Tien, Ngay, PhanTram }

    public sealed record Cot(string TieuDe, Kieu Kieu = Kieu.Chu);

    public sealed class Bang
    {
        public required string TenSheet { get; init; }
        public required string TieuDe { get; init; }
        public required IReadOnlyList<Cot> Cot { get; init; }
        public required IReadOnlyList<object?[]> Dong { get; init; }
        /// <summary>Thêm dòng "Tổng cộng" cộng các cột kiểu Tiền.</summary>
        public bool DongTong { get; init; }
    }

    private static readonly XLColor MauChuDao = XLColor.FromHtml("#0B57B0");
    private static readonly XLColor MauNenXen = XLColor.FromHtml("#F3F7FC");
    private const string DinhDangTien = "#,##0 \"đ\"";
    private const int DongTieuDeCot = 4;

    public static void Xuat(string duongDan, string tenBaoCao, IEnumerable<Bang> bangs)
    {
        using var wb = new XLWorkbook();
        foreach (var b in bangs) VeSheet(wb.Worksheets.Add(b.TenSheet), tenBaoCao, b);
        wb.SaveAs(duongDan);
    }

    private static void VeSheet(IXLWorksheet ws, string tenBaoCao, Bang b)
    {
        int soCot = b.Cot.Count;

        // Tiêu đề + giờ xuất
        ws.Cell(1, 1).Value = $"{tenBaoCao} — {b.TieuDe}";
        ws.Range(1, 1, 1, soCot).Merge()
          .Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(MauChuDao);
        ws.Cell(2, 1).Value = $"Xuất lúc {DateTime.Now:dd/MM/yyyy HH:mm}";
        ws.Range(2, 1, 2, soCot).Merge()
          .Style.Font.SetItalic().Font.SetFontColor(XLColor.Gray);

        // Dòng tiêu đề cột
        for (int c = 0; c < soCot; c++)
        {
            var o = ws.Cell(DongTieuDeCot, c + 1);
            o.Value = b.Cot[c].TieuDe;
            o.Style.Font.SetBold().Font.SetFontColor(XLColor.White)
                   .Fill.SetBackgroundColor(MauChuDao)
                   .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                   .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                   .Alignment.SetWrapText(true);
        }
        ws.Row(DongTieuDeCot).Height = 22;

        int dauDuLieu = DongTieuDeCot + 1;
        int dong = dauDuLieu;

        if (b.Dong.Count == 0)
        {
            ws.Cell(dong, 1).Value = "Không có dữ liệu";
            ws.Range(dong, 1, dong, soCot).Merge()
              .Style.Font.SetItalic().Font.SetFontColor(XLColor.Gray)
              .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            dong++;
        }

        foreach (var hang in b.Dong)
        {
            for (int c = 0; c < soCot; c++)
                GhiO(ws.Cell(dong, c + 1), c < hang.Length ? hang[c] : null, b.Cot[c].Kieu);
            if ((dong - dauDuLieu) % 2 == 1)
                ws.Range(dong, 1, dong, soCot).Style.Fill.SetBackgroundColor(MauNenXen);
            dong++;
        }
        int cuoiDuLieu = dong - 1;

        // Dòng tổng bằng công thức để người dùng sửa số trong Excel vẫn cộng đúng
        if (b.DongTong && b.Dong.Count > 0)
        {
            ws.Cell(dong, 1).Value = "Tổng cộng";
            for (int c = 0; c < soCot; c++)
            {
                if (b.Cot[c].Kieu != Kieu.Tien) continue;
                string col = ws.Column(c + 1).ColumnLetter();
                var o = ws.Cell(dong, c + 1);
                o.FormulaA1 = $"SUM({col}{dauDuLieu}:{col}{cuoiDuLieu})";
                o.Style.NumberFormat.Format = DinhDangTien;
            }
            ws.Range(dong, 1, dong, soCot).Style.Font.SetBold()
              .Border.SetTopBorder(XLBorderStyleValues.Double);
            dong++;
        }

        // Khung bảng
        var bang = ws.Range(DongTieuDeCot, 1, dong - 1, soCot);
        bang.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                  .Border.SetInsideBorder(XLBorderStyleValues.Thin)
                  .Border.SetInsideBorderColor(XLColor.FromHtml("#D9D9D9"))
                  .Border.SetOutsideBorderColor(XLColor.FromHtml("#BFBFBF"));

        // Độ rộng cột theo nội dung bảng (bỏ qua tiêu đề gộp ở dòng 1–2)
        ws.Columns(1, soCot).AdjustToContents(DongTieuDeCot, Math.Max(DongTieuDeCot, dong - 1));
        foreach (var col in ws.Columns(1, soCot))
            col.Width = Math.Clamp(col.Width + 2, 10, 60);

        ws.SheetView.FreezeRows(DongTieuDeCot);
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.FitToPages(1, 0);
    }

    private static void GhiO(IXLCell o, object? v, Kieu kieu)
    {
        switch (v)
        {
            case null:
                return;
            case DateTime d:
                o.Value = d;
                o.Style.NumberFormat.Format = "dd/MM/yyyy";
                o.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                return;
            case decimal m:
                o.Value = m;
                o.Style.NumberFormat.Format = kieu == Kieu.Tien ? DinhDangTien : "#,##0";
                return;
            case int i:
                o.Value = i;
                if (kieu == Kieu.So) o.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                return;
            case double p when kieu == Kieu.PhanTram:
                o.Value = p;                       // lưu 0.95, Excel hiển thị 95%
                o.Style.NumberFormat.Format = "0%";
                return;
            default:
                o.Value = v.ToString();
                return;
        }
    }
}
