using System.Windows.Controls;

namespace QLPT.WPF.Views.Pages;

public partial class TaiSanPage : UserControl
{
    public TaiSanPage()
    {
        InitializeComponent();
        if (QLPT.WPF.Services.Session.IsManager)
        {
            TonKhoColumn.Visibility = System.Windows.Visibility.Collapsed;
            TrongPhongColumn.Header = "Tại cơ sở";
            TrongPhongColumn.Width = new DataGridLength(95);
        }
    }
}
