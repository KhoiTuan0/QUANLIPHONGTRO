using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using QLPT.WPF.Services;

namespace QLPT.WPF.Controls;

/// <summary>Ô tìm kiếm và danh sách chọn; nội dung gõ tìm kiếm không bao giờ trở thành mã được lưu.</summary>
public partial class EntityPicker : UserControl
{
    private List<LookupOption> _options = new();
    private bool _loading;
    private bool _syncing;
    private bool _loaded;
    private int _loadVersion;
    public bool IsReady { get; private set; }
    public LookupOption? SelectedOption => CbOptions.SelectedItem as LookupOption;
    public event RoutedEventHandler? SelectionChanged;

    public EntityPicker()
    {
        InitializeComponent();
        CbOptions.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(ComboTextChanged));
        Loaded += async (_, _) => { _loaded = true; if (!IsReady) await ReloadAsync(); };
        Unloaded += (_, _) => { _loaded = false; _loadVersion++; _loading = false; };
    }

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(LookupKind), typeof(EntityPicker),
        new PropertyMetadata(LookupKind.KhachThue, (d, e) =>
        {
            var p = (EntityPicker)d;
            p._loadVersion++;
            p._loading = false;
            p.IsReady = false;
            if (p._loaded) _ = p.ReloadAsync();
        }));
    public LookupKind Source { get => (LookupKind)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    public static readonly DependencyProperty HintProperty = DependencyProperty.Register(nameof(Hint), typeof(string), typeof(EntityPicker), new PropertyMetadata("Chọn hồ sơ *"));
    public string Hint { get => (string)GetValue(HintProperty); set => SetValue(HintProperty, value); }
    public static readonly DependencyProperty SearchHintProperty = DependencyProperty.Register(nameof(SearchHint), typeof(string), typeof(EntityPicker), new PropertyMetadata("Tìm theo tên hoặc mã"));
    public string SearchHint { get => (string)GetValue(SearchHintProperty); set => SetValue(SearchHintProperty, value); }

    public static readonly DependencyProperty SelectedIdProperty = DependencyProperty.Register(nameof(SelectedId), typeof(string), typeof(EntityPicker),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((EntityPicker)d).SelectRequested()));
    public string SelectedId { get => (string)GetValue(SelectedIdProperty); set => SetValue(SelectedIdProperty, value); }

    public static readonly DependencyProperty AllowedIdProperty = DependencyProperty.Register(nameof(AllowedId), typeof(string), typeof(EntityPicker),
        new PropertyMetadata("", (d, _) => ((EntityPicker)d).ApplyFilter()));
    public string AllowedId { get => (string)GetValue(AllowedIdProperty); set => SetValue(AllowedIdProperty, value); }

    public static readonly DependencyProperty AllowEmptyProperty = DependencyProperty.Register(nameof(AllowEmpty), typeof(bool), typeof(EntityPicker),
        new PropertyMetadata(false, (d, _) => ((EntityPicker)d).ApplyFilter()));
    public bool AllowEmpty { get => (bool)GetValue(AllowEmptyProperty); set => SetValue(AllowEmptyProperty, value); }

    public static readonly DependencyProperty ReadOnlyProperty = DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(EntityPicker),
        new PropertyMetadata(false, (d, _) => ((EntityPicker)d).UpdateEnabled()));
    public bool ReadOnly { get => (bool)GetValue(ReadOnlyProperty); set => SetValue(ReadOnlyProperty, value); }

    public bool TryGetSelectedId(out int id)
    {
        id = 0;
        return IsReady && SelectedOption != null && int.TryParse(SelectedOption.Id, out id) && id > 0;
    }

    public async Task ReloadAsync()
    {
        if (_loading) return;
        var version = ++_loadVersion;
        _loading = true;
        IsReady = false;
        UpdateEnabled();
        TxtStatus.Text = "Đang tải danh sách…";
        try
        {
            var options = await LookupData.LoadAsync(Source);
            if (version != _loadVersion || !_loaded) return;
            _options = options;
            IsReady = true;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            if (version != _loadVersion || !_loaded) return;
            _options.Clear();
            _syncing = true;
            CbOptions.ItemsSource = null;
            _syncing = false;
            SetCurrentValue(SelectedIdProperty, "");
            TxtStatus.Text = $"Không tải được danh sách: {ex.Message}";
        }
        finally
        {
            if (version == _loadVersion) { _loading = false; UpdateEnabled(); }
        }
    }

    private void UpdateEnabled()
    {
        if (InputPanel == null) return;
        InputPanel.IsEnabled = IsReady && !ReadOnly;
        TxtSearch.Visibility = Visibility.Collapsed;
    }

    private void Search_Changed(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ComboTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing || !IsReady || ReadOnly) return;
        TxtSearch.Text = CbOptions.Text;
        if (!CbOptions.IsDropDownOpen) CbOptions.IsDropDownOpen = true;
    }

    private void ApplyFilter()
    {
        if (CbOptions == null || TxtStatus == null || !IsReady || _syncing) return;
        var selectedId = SelectedId;
        var typed = TxtSearch.Text;
        var query = TextUtil.Normalize(typed.Trim());
        var allowed = _options.Where(x => string.IsNullOrEmpty(AllowedId) || x.Id == AllowedId).ToList();
        var filtered = allowed.Where(x => query.Length == 0 || x.SearchKey.Contains(query)).ToList();
        if (AllowEmpty) filtered.Insert(0, new LookupOption("", "Không gắn hồ sơ", ""));
        _syncing = true;
        CbOptions.ItemsSource = filtered;
        CbOptions.SelectedItem = filtered.FirstOrDefault(x => x.Id == selectedId);
        if (CbOptions.SelectedItem == null && typed.Length > 0) CbOptions.Text = typed;
        _syncing = false;
        SetCurrentValue(SelectedIdProperty, SelectedOption?.Id ?? "");
        TxtStatus.Text = allowed.Count == 0 ? LookupData.EmptyMessage(Source) :
            filtered.Count == (AllowEmpty ? 1 : 0) ? "Không tìm thấy kết quả. Thử tên, mã hoặc thông tin khác." :
            $"{filtered.Count - (AllowEmpty ? 1 : 0)}/{allowed.Count} lựa chọn";
        SelectionChanged?.Invoke(this, new RoutedEventArgs());
    }

    private void SelectRequested()
    {
        if (_syncing || !IsReady) return;
        _syncing = true;
        CbOptions.SelectedItem = CbOptions.Items.Cast<LookupOption>().FirstOrDefault(x => x.Id == SelectedId);
        _syncing = false;
        SelectionChanged?.Invoke(this, new RoutedEventArgs());
    }

    private void Options_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing) return;
        SetCurrentValue(SelectedIdProperty, SelectedOption?.Id ?? "");
        if (SelectedOption != null)
        {
            _syncing = true;
            TxtSearch.Text = "";
            _syncing = false;
            ApplyFilter();
        }
    }
}
