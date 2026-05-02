using System.Windows;
using Deadpool.UI.Wpf.Views;
using Deadpool.UI.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Deadpool.UI.Wpf;

public partial class MainWindow : Window
{
    private readonly IServiceProvider? _serviceProvider;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(DashboardViewModel viewModel, IServiceProvider serviceProvider)
        : this()
    {
        DataContext = viewModel;
        _serviceProvider = serviceProvider;
    }

    private void OnOpenRestoreDialogClick(object sender, RoutedEventArgs e)
    {
        if (_serviceProvider == null)
            return;

        using var scope = _serviceProvider.CreateScope();
        var restoreWindow = scope.ServiceProvider.GetRequiredService<RestoreWindow>();
        restoreWindow.Owner = this;
        restoreWindow.ShowDialog();
    }
}
