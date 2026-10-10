using System.Windows.Controls;
using QLPT.WPF.ViewModels;

namespace QLPT.WPF.Views.Pages;

public partial class CuTruPage : UserControl
{
    public CuTruPage()
    {
        InitializeComponent();
        DataContext = new CuTruViewModel();
    }
}
