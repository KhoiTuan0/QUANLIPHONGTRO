using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using QLPT.WPF.Services;

namespace QLPT.WPF.Controls;

/// <summary>
/// Bảng dữ liệu dùng chung cho các trang quản lý: tìm kiếm không dấu,
/// nút Sửa / Xóa ngay trên từng dòng, đếm số dòng, trạng thái rỗng và đang tải.
/// Cột do từng trang khai báo qua <see cref="Columns"/>.
/// </summary>
public partial class CrudTable : UserControl
{
    public CrudTable()
    {
        InitializeComponent();
        Columns.CollectionChanged += OnColumnsChanged;
        Loaded += (_, _) => { SyncColumns(); ApplyFilter(); };
    }

    // ───────────────────────── Cột ─────────────────────────

    /// <summary>Cột hiển thị do trang khai báo; cột thao tác tự động nối vào cuối.</summary>
    public ObservableCollection<DataGridColumn> Columns { get; } = new();

    private void OnColumnsChanged(object? s, NotifyCollectionChangedEventArgs e) => SyncColumns();

    private void SyncColumns()
    {
        if (Grid == null) return;
        Grid.Columns.Clear();
        foreach (var c in Columns) Grid.Columns.Add(c);
        if (ShowEdit || ShowDelete) Grid.Columns.Add(ActionCol);
    }

    // ───────────────────────── Dependency properties ─────────────────────────

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(CrudTable),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CrudTable)d).ApplyFilter();

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(CrudTable),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public static readonly DependencyProperty ReloadCommandProperty =
        DependencyProperty.Register(nameof(ReloadCommand), typeof(ICommand), typeof(CrudTable));
    public ICommand? ReloadCommand
    {
        get => (ICommand?)GetValue(ReloadCommandProperty);
        set => SetValue(ReloadCommandProperty, value);
    }

    public static readonly DependencyProperty AddCommandProperty =
        DependencyProperty.Register(nameof(AddCommand), typeof(ICommand), typeof(CrudTable));
    public ICommand? AddCommand
    {
        get => (ICommand?)GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    public static readonly DependencyProperty EditCommandProperty =
        DependencyProperty.Register(nameof(EditCommand), typeof(ICommand), typeof(CrudTable));
    public ICommand? EditCommand
    {
        get => (ICommand?)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    public static readonly DependencyProperty DeleteCommandProperty =
        DependencyProperty.Register(nameof(DeleteCommand), typeof(ICommand), typeof(CrudTable));
    public ICommand? DeleteCommand
    {
        get => (ICommand?)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public static readonly DependencyProperty IsBusyProperty =
        DependencyProperty.Register(nameof(IsBusy), typeof(bool), typeof(CrudTable),
            new PropertyMetadata(false));
    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    public static readonly DependencyProperty ErrorMessageProperty =
        DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(CrudTable),
            new PropertyMetadata("", OnErrorMessageChanged));
    public string ErrorMessage
    {
        get => (string)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    private static void OnErrorMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var t = (CrudTable)d;
        if (t.ErrorBar == null) return;
        t.ErrorBar.Visibility = string.IsNullOrWhiteSpace(e.NewValue as string)
            ? Visibility.Collapsed : Visibility.Visible;
    }

    public static readonly DependencyProperty SearchHintProperty =
        DependencyProperty.Register(nameof(SearchHint), typeof(string), typeof(CrudTable),
            new PropertyMetadata("Tìm kiếm…"));
    public string SearchHint
    {
        get => (string)GetValue(SearchHintProperty);
        set => SetValue(SearchHintProperty, value);
    }

    public static readonly DependencyProperty AddLabelProperty =
        DependencyProperty.Register(nameof(AddLabel), typeof(string), typeof(CrudTable),
            new PropertyMetadata("Thêm"));
    public string AddLabel
    {
        get => (string)GetValue(AddLabelProperty);
        set => SetValue(AddLabelProperty, value);
    }

    /// <summary>Icon trên nút chính — mặc định dấu cộng, trang có thể đổi (Gửi, Tạo hóa đơn…).</summary>
    public static readonly DependencyProperty AddIconProperty =
        DependencyProperty.Register(nameof(AddIcon), typeof(MaterialDesignThemes.Wpf.PackIconKind),
            typeof(CrudTable),
            new PropertyMetadata(MaterialDesignThemes.Wpf.PackIconKind.Plus));
    public MaterialDesignThemes.Wpf.PackIconKind AddIcon
    {
        get => (MaterialDesignThemes.Wpf.PackIconKind)GetValue(AddIconProperty);
        set => SetValue(AddIconProperty, value);
    }

    public static readonly DependencyProperty EditLabelProperty =
        DependencyProperty.Register(nameof(EditLabel), typeof(string), typeof(CrudTable),
            new PropertyMetadata("Sửa dòng này"));
    public string EditLabel
    {
        get => (string)GetValue(EditLabelProperty);
        set => SetValue(EditLabelProperty, value);
    }

    public static readonly DependencyProperty DeleteLabelProperty =
        DependencyProperty.Register(nameof(DeleteLabel), typeof(string), typeof(CrudTable),
            new PropertyMetadata("Xóa dòng này"));
    public string DeleteLabel
    {
        get => (string)GetValue(DeleteLabelProperty);
        set => SetValue(DeleteLabelProperty, value);
    }

    public static readonly DependencyProperty ShowAddProperty =
        DependencyProperty.Register(nameof(ShowAdd), typeof(bool), typeof(CrudTable),
            new PropertyMetadata(true));
    public bool ShowAdd
    {
        get => (bool)GetValue(ShowAddProperty);
        set => SetValue(ShowAddProperty, value);
    }

    public static readonly DependencyProperty ShowEditProperty =
        DependencyProperty.Register(nameof(ShowEdit), typeof(bool), typeof(CrudTable),
            new PropertyMetadata(true, OnActionVisibilityChanged));
    public bool ShowEdit
    {
        get => (bool)GetValue(ShowEditProperty);
        set => SetValue(ShowEditProperty, value);
    }

    public static readonly DependencyProperty ShowDeleteProperty =
        DependencyProperty.Register(nameof(ShowDelete), typeof(bool), typeof(CrudTable),
            new PropertyMetadata(true, OnActionVisibilityChanged));
    public bool ShowDelete
    {
        get => (bool)GetValue(ShowDeleteProperty);
        set => SetValue(ShowDeleteProperty, value);
    }

    private static void OnActionVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CrudTable)d).SyncColumns();

    public static readonly DependencyProperty ExtraFilterProperty =
        DependencyProperty.Register(nameof(ExtraFilter), typeof(object), typeof(CrudTable));
    public object? ExtraFilter
    {
        get => GetValue(ExtraFilterProperty);
        set => SetValue(ExtraFilterProperty, value);
    }

    /// <summary>Nút nghiệp vụ riêng của trang, nằm cạnh nút chính (Gia hạn, Thu tiền…).</summary>
    public static readonly DependencyProperty ExtraActionsProperty =
        DependencyProperty.Register(nameof(ExtraActions), typeof(object), typeof(CrudTable));
    public object? ExtraActions
    {
        get => GetValue(ExtraActionsProperty);
        set => SetValue(ExtraActionsProperty, value);
    }

    // ───────────────────────── Tìm kiếm ─────────────────────────

    private static readonly Dictionary<Type, PropertyInfo[]> _searchable = new();

    /// <summary>Các thuộc tính đáng để so khớp: chuỗi và số (mã, giá, số điện thoại…).</summary>
    private static PropertyInfo[] SearchableProps(Type t)
    {
        if (_searchable.TryGetValue(t, out var cached)) return cached;
        var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 &&
                        (p.PropertyType == typeof(string) ||
                         p.PropertyType.IsPrimitive ||
                         p.PropertyType == typeof(decimal)))
            .ToArray();
        _searchable[t] = props;
        return props;
    }

    private static bool Matches(object item, string normalizedQuery)
    {
        foreach (var p in SearchableProps(item.GetType()))
        {
            object? v;
            try { v = p.GetValue(item); } catch { continue; }
            if (v == null) continue;
            if (TextUtil.Normalize(v.ToString()).Contains(normalizedQuery)) return true;
        }
        return false;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { SearchBox.Clear(); e.Handled = true; }
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        SearchBox.Focus();
    }

    private void ApplyFilter()
    {
        if (Grid == null || SearchBox == null) return;

        string raw = SearchBox.Text.Trim();
        string q = TextUtil.Normalize(raw);

        ClearSearchBtn.Visibility = raw.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        int total = 0, shown = 0;
        var view = ItemsSource == null ? null : CollectionViewSource.GetDefaultView(ItemsSource);

        if (view != null)
        {
            view.Filter = q.Length == 0 ? null : o => o != null && Matches(o, q);
            foreach (var _ in (IEnumerable)ItemsSource!) total++;
            foreach (var _ in view) shown++;
        }

        CountLabel.Text = q.Length == 0
            ? (total == 0 ? "" : $"{total} dòng")
            : $"{shown} / {total} dòng";

        bool empty = shown == 0;
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        if (empty)
            EmptyText.Text = q.Length > 0
                ? $"Không tìm thấy kết quả cho “{raw}”"
                : "Chưa có dữ liệu";
    }

    // ───────────────────────── Thao tác trên dòng ─────────────────────────
    // Lệnh Sửa/Xóa ở ViewModel đọc SelectedItem, nên chọn dòng trước rồi mới gọi.

    private void RowEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        Grid.SelectedItem = fe.DataContext;
        if (EditCommand?.CanExecute(null) == true) EditCommand.Execute(null);
    }

    private void RowDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        Grid.SelectedItem = fe.DataContext;
        if (DeleteCommand?.CanExecute(null) == true) DeleteCommand.Execute(null);
    }

    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!ShowEdit || Grid.SelectedItem == null) return;
        if (EditCommand?.CanExecute(null) == true) EditCommand.Execute(null);
    }
}
