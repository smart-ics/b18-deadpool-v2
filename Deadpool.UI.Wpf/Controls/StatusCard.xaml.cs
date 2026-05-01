using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Deadpool.UI.Wpf.Controls;

public partial class StatusCard : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(StatusCard),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(
        nameof(StatusText),
        typeof(string),
        typeof(StatusCard),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DetailTextProperty = DependencyProperty.Register(
        nameof(DetailText),
        typeof(string),
        typeof(StatusCard),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty StatusColorProperty = DependencyProperty.Register(
        nameof(StatusColor),
        typeof(Brush),
        typeof(StatusCard),
        new PropertyMetadata(Brushes.White));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public string DetailText
    {
        get => (string)GetValue(DetailTextProperty);
        set => SetValue(DetailTextProperty, value);
    }

    public Brush StatusColor
    {
        get => (Brush)GetValue(StatusColorProperty);
        set => SetValue(StatusColorProperty, value);
    }

    public StatusCard()
    {
        InitializeComponent();
    }
}
