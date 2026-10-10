using System.Windows.Controls;
using QLPT.WPF.ViewModels;

namespace QLPT.WPF.Views.Pages;

public partial class ChiSoPage : UserControl
{
    public ChiSoPage()
    {
        InitializeComponent();
        DataContext = new ChiSoViewModel();
    }
}
