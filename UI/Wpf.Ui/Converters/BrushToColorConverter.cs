

using System.Windows.Data;

namespace Wpf.Ui.Converters;

internal class BrushToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SolidColorBrush brush)
        {
            return brush.Color;
        }

        if (value is Color)
        {
            return value;
        }

        // We draw red to visibly see an invalid bind in the UI.
        return new Color
        {
            A = 255,
            R = 255,
            G = 0,
            B = 0,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
