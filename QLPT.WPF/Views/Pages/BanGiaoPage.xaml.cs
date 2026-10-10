using System.Windows.Controls;
using QLPT.WPF.ViewModels;

namespace QLPT.WPF.Views.Pages;

public partial class BanGiaoPage : UserControl
{
    public BanGiaoPage()
    {
        InitializeComponent();
        DataContext = new BanGiaoViewModel();
    }
}
