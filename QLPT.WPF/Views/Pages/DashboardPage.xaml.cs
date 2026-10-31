using System.Windows.Controls;
using System.Windows.Input;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;

namespace QLPT.WPF.Views.Pages;

public partial class DashboardPage : UserControl
{
    public DashboardPage()
    {
        InitializeComponent();
        // LiveCharts không theo DynamicResource: đổi sáng/tối thì vẽ lại màu biểu đồ.
        ChartThemeBinding.Attach(this, () => (DataContext as DashboardViewModel)?.DungBieuDo());
    }

    private void PhongChart_HoveredPointsChanged(IChartView chart,
        IEnumerable<ChartPoint>? newItems, IEnumerable<ChartPoint>? oldItems)
    {
        // LiveCharts có thể gửi null khi con trỏ rời lát hoặc biểu đồ đang vẽ lại.
        (DataContext as DashboardViewModel)?.HienPhongDangRe(
            newItems?.FirstOrDefault()?.Context?.Series?.Name);
    }

    private void PhongChart_MouseLeave(object sender, MouseEventArgs e)
    {
        (DataContext as DashboardViewModel)?.HienPhongDangRe(null);
    }
}
