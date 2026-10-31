using System.Text.Json;
using System.Windows;
using QLPT.WPF.Services;

namespace QLPT.WPF.Views.Dialogs;

public partial class ChonBanGiaoDialog : Window
{
    private sealed record Choice(string Label, JsonElement Record);
    public JsonElement SelectedRecord { get; private set; }

    public ChonBanGiaoDialog(IEnumerable<JsonElement> records)
    {
        InitializeComponent();
        ListRecords.ItemsSource = records.Select(item => new Choice(
            $"{ApiClient.ElStr(item, "loaiBanGiao")} · {ApiClient.ElStr(item, "ngayBanGiao")} · Mã #{ApiClient.ElStr(item, "maBanGiao")}",
            item.Clone())).ToList();
        ListRecords.SelectedIndex = 0;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (ListRecords.SelectedItem is not Choice choice) return;
        SelectedRecord = choice.Record;
        DialogResult = true;
    }
}
