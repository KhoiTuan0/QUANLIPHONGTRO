using System.Windows.Controls;
using QLPT.WPF.ViewModels;

namespace QLPT.WPF.Views.Pages;

public partial class HopDongPage : UserControl
{
    public HopDongPage()
    {
        InitializeComponent();
        DataContext = new HopDongViewModel();
    }
}
