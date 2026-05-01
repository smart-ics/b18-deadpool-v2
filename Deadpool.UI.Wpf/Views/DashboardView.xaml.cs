using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Threading;
using Deadpool.UI.Wpf.ViewModels;

namespace Deadpool.UI.Wpf.Views;

public partial class DashboardView : UserControl
{
    private DashboardViewModel? _viewModel;
    private readonly DispatcherTimer _refreshTimer;

    public DashboardView()
    {
        InitializeComponent();
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;

        if (DesignerProperties.GetIsInDesignMode(this))
        {
            DataContext = DashboardViewModel.CreateDesignSample();
            return;
        }

        Loaded += DashboardView_Loaded;
        Unloaded += DashboardView_Unloaded;
    }

    private async void DashboardView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_viewModel == null)
        {
            _viewModel = DataContext as DashboardViewModel;
        }

        if (_viewModel == null)
        {
            return;
        }

        if (!_viewModel.IsLoaded)
        {
            await _viewModel.LoadAsync();
        }

        _refreshTimer.Start();
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        _refreshTimer.Stop();
        try
        {
            await _viewModel.RefreshAsync();
        }
        finally
        {
            _refreshTimer.Start();
        }
    }

    private void DashboardView_Unloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _refreshTimer.Stop();
    }
}
