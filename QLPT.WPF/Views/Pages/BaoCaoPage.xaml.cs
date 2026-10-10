using System.Windows.Controls;
using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;

namespace QLPT.WPF.Views.Pages;

public partial class BaoCaoPage : UserControl
{
    public BaoCaoPage()
    {
        InitializeComponent();
        // LiveCharts không theo DynamicResource: đổi sáng/tối thì vẽ lại màu biểu đồ.
        ChartThemeBinding.Attach(this, () => (DataContext as BaoCaoViewModel)?.DungBieuDo());
    }
}
