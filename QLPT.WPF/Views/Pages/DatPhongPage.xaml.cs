using System.Windows.Controls;
using QLPT.WPF.ViewModels;

namespace QLPT.WPF.Views.Pages;

public partial class DatPhongPage : UserControl
{
    public DatPhongPage()
    {
        InitializeComponent();
        DataContext = new DatPhongViewModel();
    }
}
