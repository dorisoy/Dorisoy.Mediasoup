

using System.Windows.Data;

namespace Wpf.Ui.Converters;

internal class FallbackBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            SolidColorBrush brush => brush,
            Color color => new SolidColorBrush(color),
            _ => new SolidColorBrush(Colors.Red),
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
