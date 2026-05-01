using System.Windows.Controls;
using Deadpool.UI.Wpf.ViewModels;

namespace Deadpool.UI.Wpf.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        DataContext = DashboardViewModel.CreateDesignSample();
    }
}
