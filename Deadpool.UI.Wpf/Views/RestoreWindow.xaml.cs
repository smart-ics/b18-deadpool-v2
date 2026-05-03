using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Deadpool.UI.Wpf.ViewModels;
using Microsoft.Extensions.Logging;

namespace Deadpool.UI.Wpf.Views;

public partial class RestoreWindow : Window
{
    private readonly ILogger<RestoreWindow> _logger;

    public RestoreWindow(RestoreViewModel viewModel, ILogger<RestoreWindow> logger)
    {
        InitializeComponent();
        DataContext = viewModel;
        _logger = logger;
    }

    private void OnExecuteRestoreClick(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("Execute Restore button clicked.");
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

public sealed class BoolToValidationTextConverter : IValueConverter
{
    public static BoolToValidationTextConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool isValid && isValid ? "VALID" : "INVALID";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class BoolToBrushConverter : IValueConverter
{
    public static BoolToBrushConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool isValid && isValid
            ? Brushes.LimeGreen
            : Brushes.OrangeRed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
