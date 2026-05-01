using System.Windows;
using Deadpool.UI.Wpf.ViewModels;

namespace Deadpool.UI.Wpf;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(DashboardViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }
}
