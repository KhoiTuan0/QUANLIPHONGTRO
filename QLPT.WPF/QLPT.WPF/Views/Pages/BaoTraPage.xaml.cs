using System.Windows.Controls;
using QLPT.WPF.ViewModels;

namespace QLPT.WPF.Views.Pages;

public partial class BaoTraPage : UserControl
{
    public BaoTraPage()
    {
        InitializeComponent();
        DataContext = new BaoTraViewModel();
    }
}
