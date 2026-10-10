using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace QLPT.WPF.Services;

/// <summary>
/// Ô nhập tiền VNĐ: <c>svc:MoneyInput.IsEnabled="True"</c> trên TextBox.
/// Chỉ nhận chữ số, tự chèn dấu chấm phân cách nghìn khi gõ (1500000 → 1.500.000),
/// giữ đúng vị trí con trỏ, thêm hậu tố "đ". Giá trị đọc lại bằng <see cref="VnMoney.TryParse"/>.
/// </summary>
public static class MoneyInput
{
    private const int SoChuSoToiDa = 15;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(MoneyInput),
            new PropertyMetadata(false, KhiDoi));

    public static bool GetIsEnabled(DependencyObject o) => (bool)o.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject o, bool v) => o.SetValue(IsEnabledProperty, v);

    // Cờ chặn đệ quy khi chính behavior gán lại Text
    private static readonly DependencyProperty DangGhiProperty =
        DependencyProperty.RegisterAttached("DangGhi", typeof(bool), typeof(MoneyInput), new PropertyMetadata(false));

    private static void KhiDoi(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox tb) return;
        if ((bool)e.NewValue)
        {
            tb.PreviewTextInput += ChanKyTu;
            tb.PreviewKeyDown   += XuLyPhim;
            tb.TextChanged      += DinhDangLai;
            DataObject.AddPastingHandler(tb, KhiDan);
            InputMethod.SetIsInputMethodEnabled(tb, false); // bộ gõ tiếng Việt không chen ký tự vào ô số
            tb.IsUndoEnabled = false;                       // undo từng bước định dạng gây rối, tắt đi
            if (string.IsNullOrEmpty(TextFieldAssist.GetSuffixText(tb)))
                TextFieldAssist.SetSuffixText(tb, "đ");
            if (tb.Text.Length > 0) DinhDangLai(tb, null);
        }
        else
        {
            tb.PreviewTextInput -= ChanKyTu;
            tb.PreviewKeyDown   -= XuLyPhim;
            tb.TextChanged      -= DinhDangLai;
            DataObject.RemovePastingHandler(tb, KhiDan);
        }
    }

    private static void ChanKyTu(object sender, TextCompositionEventArgs e)
    {
        var tb = (TextBox)sender;
        if (!e.Text.All(char.IsAsciiDigit)) { e.Handled = true; return; }

        int conLai = DemSo(tb.Text) - DemSo(tb.SelectedText);
        if (conLai + e.Text.Length > SoChuSoToiDa) e.Handled = true;
    }

    private static void XuLyPhim(object sender, KeyEventArgs e)
    {
        var tb = (TextBox)sender;
        if (e.Key == Key.Space) { e.Handled = true; return; }
        if (tb.SelectionLength > 0) return;

        // Con trỏ đứng cạnh dấu chấm: nhảy qua dấu chấm để Backspace/Delete xóa chữ số,
        // nếu không xóa dấu chấm xong nó lại được chèn lại và có vẻ như phím không ăn.
        int i = tb.CaretIndex;
        if (e.Key == Key.Back && i > 0 && tb.Text[i - 1] == '.') tb.CaretIndex = i - 1;
        else if (e.Key == Key.Delete && i < tb.Text.Length && tb.Text[i] == '.') tb.CaretIndex = i + 1;
    }

    private static void KhiDan(object sender, DataObjectPastingEventArgs e)
    {
        var tb = (TextBox)sender;
        e.CancelCommand();
        if (e.DataObject.GetData(DataFormats.UnicodeText) is not string s) return;

        // Dán "1.500.000 đ" hay "1500000.00" đều hiểu theo số tiền, không ghép bừa chữ số.
        string so = VnMoney.TryParse(s, out var v)
            ? Math.Round(Math.Abs(v), MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)
            : LaySo(s);

        int dau = tb.SelectionStart;
        string moi = tb.Text.Remove(dau, tb.SelectionLength).Insert(dau, so);
        Ghi(tb, moi, DemSo(moi[..(dau + so.Length)]));
    }

    private static void DinhDangLai(object sender, TextChangedEventArgs? e)
    {
        var tb = (TextBox)sender;
        if ((bool)tb.GetValue(DangGhiProperty)) return;

        if (tb.IsKeyboardFocused)
        {
            // Người dùng đang gõ: chỉ còn chữ số và dấu chấm do mình chèn → lấy chữ số, nhóm lại.
            Ghi(tb, tb.Text, DemSo(tb.Text[..Math.Min(tb.CaretIndex, tb.Text.Length)]));
        }
        else
        {
            // Code gán sẵn (VD giá trị cũ "1500000.00", "1.500.000 đ"): đọc theo số tiền rồi làm tròn.
            string t = VnMoney.TryParse(tb.Text, out var v)
                ? Math.Round(Math.Abs(v), MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)
                : tb.Text;
            Ghi(tb, t, int.MaxValue);
        }
    }

    /// <summary>Gán text đã nhóm nghìn và đặt con trỏ ngay sau chữ số thứ <paramref name="soTruocConTro"/>.</summary>
    private static void Ghi(TextBox tb, string tho, int soTruocConTro)
    {
        string so = LaySo(tho);
        int boDau = so.Length - so.TrimStart('0').Length;           // 000123 → 123
        if (boDau == so.Length && so.Length > 0) boDau--;           // giữ lại một số 0
        so = so[boDau..];
        soTruocConTro = Math.Max(0, soTruocConTro - boDau);
        if (so.Length > SoChuSoToiDa) so = so[..SoChuSoToiDa];

        string moi = NhomNghin(so);
        if (moi == tb.Text) return;

        tb.SetValue(DangGhiProperty, true);
        try
        {
            tb.Text = moi;
            tb.CaretIndex = ViTriSauChuSo(moi, soTruocConTro);
        }
        finally { tb.SetValue(DangGhiProperty, false); }
    }

    private static string LaySo(string s) => new(s.Where(char.IsAsciiDigit).ToArray());
    private static int DemSo(string s) => s.Count(char.IsAsciiDigit);

    private static string NhomNghin(string so)
    {
        if (so.Length <= 3) return so;
        var sb = new StringBuilder(so.Length + so.Length / 3);
        int dauNhom = so.Length % 3;
        for (int i = 0; i < so.Length; i++)
        {
            if (i > 0 && (i - dauNhom) % 3 == 0) sb.Append('.');
            sb.Append(so[i]);
        }
        return sb.ToString();
    }

    private static int ViTriSauChuSo(string s, int n)
    {
        if (n <= 0) return 0;
        int dem = 0;
        for (int i = 0; i < s.Length; i++)
            if (char.IsAsciiDigit(s[i]) && ++dem == n) return i + 1;
        return s.Length;
    }
}
