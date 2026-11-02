using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;

namespace QLPT.WPF.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private Button? _activeBtn;
    private string _activePage = "Dashboard";

    // Nav model — giữ lại để lọc theo từ khóa
    private sealed class NavItem
    {
        public required Button Button { get; init; }
        public required string Label { get; init; }
        public required string Page { get; init; }
        public required string Group { get; init; }
        public required string SearchKey { get; init; }
    }

    private readonly List<NavItem> _navItems = new();
    private readonly Dictionary<string, TextBlock> _groupHeaders = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        ThemeService.ThemeChanged += OnThemeChanged;

        // Thu gọn / mở menu phải dựng lại để nhãn text theo đúng trạng thái,
        // nếu không nhãn cũ sẽ bị cắt ngang thay vì ẩn hẳn.
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsMenuOpen)) RefreshNavigation();
        };

        BuildNavigation();

        var home = _navItems.FirstOrDefault();
        if (home != null)
        {
            SelectPage(home.Page, home.Group);
            SetActive(home.Button);
        }
    }

    private void OnThemeChanged() => Dispatcher.Invoke(RefreshNavigation);

    /// <summary>Dựng lại menu nhưng giữ nguyên trang đang mở và từ khóa đang lọc.</summary>
    private void RefreshNavigation()
    {
        BuildNavigation();
        var btn = _navItems.FirstOrDefault(n => n.Page == _activePage)?.Button;
        if (btn != null) SetActive(btn);
        ApplyFilter(SearchBox.Text);
    }

    // ───────────────────────── Màu theo theme ─────────────────────────

    // Sidebar mang màu chủ đạo nên chữ/hover/mục chọn đều là lớp trắng trên nền đó,
    // khai báo trong App.xaml (Sidebar*Brush) để chỉnh một chỗ.
    private Brush Res(string key) => (Brush)FindResource(key);

    private Brush NavNormalFg => Res("SidebarItemBrush");
    private Brush NavActiveFg => Res("SidebarTextBrush");
    private Brush NavActiveBg => Res("SidebarActiveBrush");
    private Brush NavHoverBg  => Res("SidebarHoverBrush");
    private Brush NavGroupFg  => Res("SidebarGroupBrush");

    // ───────────────────────── Dựng menu ─────────────────────────
    // Nhóm theo quy trình nghiệp vụ và tần suất dùng, không theo kiểu dữ liệu:
    // việc làm hằng ngày nằm trên, phần cấu hình ít đụng tới nằm dưới cùng.

    private void BuildNavigation()
    {
        _activeBtn = null;
        _navItems.Clear();
        _visuals.Clear();
        _groupHeaders.Clear();
        NavPanel.Children.Clear();

        if (!Session.IsAdmin)
        {
            AddNavItem("Thông tin của tôi", PackIconKind.AccountCircle, "Thông tin của tôi", "");
            AddNavItem("Thông báo",         PackIconKind.Bell,          "Thông báo",         "");
            return;
        }

        // Trang chủ — đứng riêng, không thuộc nhóm nào
        AddNavItem("Dashboard", PackIconKind.ViewDashboard, "Dashboard", "", "tong quan home trang chu");

        // Vòng đời thuê phòng, xếp đúng thứ tự công việc diễn ra
        AddNavGroup("VẬN HÀNH");
        AddNavItem("Phòng trọ",        PackIconKind.Door,          "Phòng trọ",        "VẬN HÀNH");
        AddNavItem("Khách thuê",       PackIconKind.AccountGroup,  "Khách thuê",       "VẬN HÀNH", "nguoi thue tenant");
        AddNavItem("Đặt phòng",        PackIconKind.CalendarCheck, "Đặt phòng",        "VẬN HÀNH", "booking dat coc");
        AddNavItem("Hợp đồng",         PackIconKind.FileDocument,  "Hợp đồng",         "VẬN HÀNH", "contract hd");
        AddNavItem("Cư trú",           PackIconKind.HomeAccount,   "Cư trú",           "VẬN HÀNH", "tam tru khai bao");
        AddNavItem("Bàn giao tài sản", PackIconKind.ClipboardList, "Bàn giao tài sản", "VẬN HÀNH", "ban giao do dac");
        AddNavItem("Báo trả phòng",    PackIconKind.ExitToApp,     "Báo trả phòng",    "VẬN HÀNH", "tra phong check out");

        // Chu kỳ thu tiền hằng tháng — ghi chỉ số, lập hóa đơn, thu tiền
        AddNavGroup("TÀI CHÍNH");
        AddNavItem("Chỉ số điện nước",   PackIconKind.Gauge,       "Chỉ số điện nước", "TÀI CHÍNH", "dien nuoc cong to");
        AddNavItem("Hóa đơn",            PackIconKind.Receipt,     "Hóa đơn",          "TÀI CHÍNH", "bill invoice");
        AddNavItem("Sổ thu chi",         PackIconKind.CashMultiple,"Sổ thu chi",       "TÀI CHÍNH", "thu tien chi tien quy");
        AddNavItem("Dịch vụ đã đăng ký", PackIconKind.Cog,         "Dịch vụ hợp đồng", "TÀI CHÍNH", "dang ky dich vu hop dong");

        // Phát sinh trong kỳ thuê + tổng hợp
        AddNavGroup("XỬ LÝ & BÁO CÁO");
        AddNavItem("Sửa chữa",  PackIconKind.Hammer,      "Sửa chữa",  "XỬ LÝ & BÁO CÁO", "bao tri yeu cau hong");
        AddNavItem("Vi phạm",   PackIconKind.AlertCircle, "Vi phạm",   "XỬ LÝ & BÁO CÁO", "phat loi");
        AddNavItem("Thông báo", PackIconKind.Bell,        "Thông báo", "XỬ LÝ & BÁO CÁO", "gui tin nhan");
        AddNavItem("Báo cáo",   PackIconKind.ChartBar,    "Báo cáo",   "XỬ LÝ & BÁO CÁO", "thong ke doanh thu");

        // Cấu hình một lần, hiếm khi mở lại — để cuối cùng
        AddNavGroup("THIẾT LẬP");
        AddNavItem("Cơ sở",             PackIconKind.Building,           "Cơ sở",      "THIẾT LẬP", "chi nhanh dia diem");
        AddNavItem("Loại phòng",        PackIconKind.FormatListBulleted, "Loại phòng", "THIẾT LẬP", "hang phong");
        AddNavItem("Bảng giá dịch vụ",  PackIconKind.Wrench,             "Dịch vụ",    "THIẾT LẬP", "don gia dien nuoc rac xe");
        AddNavItem("Tài sản",           PackIconKind.Sofa,               "Tài sản",    "THIẾT LẬP", "thiet bi do dac");
        AddNavItem("Tài khoản",         PackIconKind.AccountKey,         "Tài khoản",  "THIẾT LẬP", "user phan quyen");
        AddNavItem("Sao lưu",           PackIconKind.DatabaseExport,     "Sao lưu",    "THIẾT LẬP", "backup du lieu");
    }

    private void AddNavGroup(string label)
    {
        var tb = new TextBlock
        {
            Text       = label,
            FontSize   = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = NavGroupFg,
            Margin     = new Thickness(12, 14, 12, 4),
            Visibility = _vm.IsMenuOpen ? Visibility.Visible : Visibility.Collapsed
        };
        _groupHeaders[label] = tb;
        NavPanel.Children.Add(tb);
    }

    private void AddNavItem(string label, PackIconKind icon, string page, string group, string extraKeywords = "")
    {
        var normalFg = NavNormalFg;
        bool open = _vm.IsMenuOpen;

        var iconCtrl = new PackIcon
        {
            Kind = icon, Width = 18, Height = 18,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = normalFg
        };

        var sp = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            // Thu gọn thì căn icon giữa; mở thì chừa chỗ cho vạch chỉ báo bên trái.
            HorizontalAlignment = open ? HorizontalAlignment.Left : HorizontalAlignment.Center,
            Margin = open ? new Thickness(14, 0, 10, 0) : new Thickness(0)
        };
        sp.Children.Add(iconCtrl);

        TextBlock? text = null;
        if (open)
        {
            text = new TextBlock
            {
                Text = label,
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13,
                // Đặt tường minh: style MaterialDesign mặc định chữ Medium, nếu để kế thừa
                // thì mục chưa từng chọn đậm hơn mục đã từng chọn rồi bỏ chọn.
                FontWeight = FontWeights.Normal,
                Foreground = normalFg,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            sp.Children.Add(text);
        }

        // Vạch chỉ báo mục đang chọn (kiểu NavigationView của Windows 11)
        var indicator = new Border
        {
            Width = 3, Height = 18,
            CornerRadius = new CornerRadius(2),
            Background = NavActiveFg,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(3, 0, 0, 0),
            Visibility = Visibility.Hidden
        };

        var content = new Grid();
        content.Children.Add(sp);
        content.Children.Add(indicator);

        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 1, 0, 1),
            Background = Brushes.Transparent,
            Child = content
        };

        var btn = new Button
        {
            Style = FindResource("MaterialDesignFlatButton") as Style,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            // Stretch để khối nền phủ hết chiều ngang thay vì ôm theo chữ.
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Height = 40,
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            Foreground = NavActiveFg, // màu gợn sóng khi bấm: trắng thay vì xanh chìm vào nền
            Content = border,
            Tag = page,
            ToolTip = label
        };
        MaterialDesignThemes.Wpf.ButtonAssist.SetCornerRadius(btn, new CornerRadius(8));
        _visuals[btn] = new NavVisual(border, indicator, iconCtrl, text);

        btn.MouseEnter += (_, _) =>
        {
            if (btn != _activeBtn) border.Background = NavHoverBg;
        };
        btn.MouseLeave += (_, _) =>
        {
            if (btn != _activeBtn) border.Background = Brushes.Transparent;
        };

        btn.Click += (_, _) =>
        {
            SelectPage(page, group);
            SetActive(btn);
        };

        NavPanel.Children.Add(btn);

        _navItems.Add(new NavItem
        {
            Button    = btn,
            Label     = label,
            Page      = page,
            Group     = group,
            SearchKey = Normalize($"{label} {page} {group} {extraKeywords}")
        });
    }

    private void SelectPage(string page, string group)
    {
        _activePage = page;
        _vm.NavigateToCommand.Execute(page);
        // Hiển thị dưới dạng "Nhóm  ›  Trang"; trang chủ không có nhóm nên để trống.
        _vm.CurrentPageGroup = group.Length == 0 ? "" : $"{ToTitle(group)}   ›   ";
    }

    private static string ToTitle(string upper) =>
        upper.Length == 0 ? "" : char.ToUpper(upper[0]) + upper[1..].ToLower();

    // Các phần tử cần đổi màu khi chọn / bỏ chọn một mục menu.
    private sealed record NavVisual(Border Nen, Border VachChon, PackIcon Icon, TextBlock? Chu);
    private readonly Dictionary<Button, NavVisual> _visuals = new();

    private void SetActive(Button btn)
    {
        if (_activeBtn != null && _visuals.TryGetValue(_activeBtn, out var cu))
            ApDungTrangThai(cu, chon: false);

        _activeBtn = btn;

        if (_visuals.TryGetValue(btn, out var moi))
            ApDungTrangThai(moi, chon: true);
    }

    private void ApDungTrangThai(NavVisual v, bool chon)
    {
        v.Nen.Background     = chon ? NavActiveBg : Brushes.Transparent;
        v.VachChon.Visibility = chon ? Visibility.Visible : Visibility.Hidden;
        v.Icon.Foreground    = chon ? NavActiveFg : NavNormalFg;
        if (v.Chu != null)
        {
            v.Chu.Foreground = chon ? NavActiveFg : NavNormalFg;
            v.Chu.FontWeight = chon ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    // ───────────────────────── Tìm kiếm chức năng ─────────────────────────

    /// <summary>Bỏ dấu tiếng Việt để gõ "hop dong" vẫn ra "Hợp đồng".</summary>
    private static string Normalize(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC)
                 .ToLowerInvariant().Replace('đ', 'd');
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        => ApplyFilter(SearchBox.Text);

    private void ApplyFilter(string query)
    {
        var q = Normalize(query.Trim());

        if (q.Length == 0)
        {
            foreach (var item in _navItems) item.Button.Visibility = Visibility.Visible;
            foreach (var h in _groupHeaders.Values)
                h.Visibility = _vm.IsMenuOpen ? Visibility.Visible : Visibility.Collapsed;
            NoResultLabel.Visibility = Visibility.Collapsed;
            return;
        }

        int hits = 0;
        foreach (var item in _navItems)
        {
            bool match = item.SearchKey.Contains(q);
            item.Button.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
            if (match) hits++;
        }

        // Ẩn tiêu đề nhóm nếu nhóm đó không còn mục nào khớp
        foreach (var (name, header) in _groupHeaders)
        {
            bool groupHasMatch = _navItems.Any(
                n => n.Group == name && n.Button.Visibility == Visibility.Visible);
            header.Visibility = groupHasMatch && _vm.IsMenuOpen
                ? Visibility.Visible : Visibility.Collapsed;
        }

        NoResultLabel.Visibility = hits == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            SearchBox.Clear();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            // Enter mở luôn kết quả đầu tiên
            var first = _navItems.FirstOrDefault(n => n.Button.Visibility == Visibility.Visible);
            if (first != null)
            {
                SelectPage(first.Page, first.Group);
                SetActive(first.Button);
                SearchBox.Clear();
            }
            e.Handled = true;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (!_vm.IsMenuOpen) _vm.IsMenuOpen = true;
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }
}
