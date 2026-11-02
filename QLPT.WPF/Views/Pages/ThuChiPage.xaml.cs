using System.Windows.Controls;
using QLPT.WPF.ViewModels;

namespace QLPT.WPF.Views.Pages;

public partial class ThuChiPage : UserControl
{
    public ThuChiPage()
    {
        InitializeComponent();
        DataContext = new ThuChiViewModel();
    }
}
