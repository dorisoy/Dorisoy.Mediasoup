

using System.Windows.Data;
using Wpf.Ui.Controls;

namespace Wpf.Ui.Converters;

internal class BackButtonVisibilityToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            not NavigationViewBackButtonVisible _ => Visibility.Collapsed,
            NavigationViewBackButtonVisible.Collapsed => Visibility.Collapsed,
            NavigationViewBackButtonVisible.Visible => Visibility.Visible,
            NavigationViewBackButtonVisible.Auto => Visibility.Visible,
            _ => Visibility.Collapsed,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
