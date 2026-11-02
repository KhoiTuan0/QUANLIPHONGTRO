using System.Windows.Controls;
using QLPT.WPF.ViewModels;

namespace QLPT.WPF.Views.Pages;

public partial class HoaDonPage : UserControl
{
    public HoaDonPage()
    {
        InitializeComponent();
        DataContext = new HoaDonViewModel();
    }
}
