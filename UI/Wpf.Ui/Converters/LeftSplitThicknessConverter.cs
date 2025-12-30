

using System.Windows.Data;

namespace Wpf.Ui.Converters;

internal class LeftSplitThicknessConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Thickness thickness)
        {
            return default(Thickness);
        }

        return new Thickness(thickness.Left, thickness.Top, 0, thickness.Bottom);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
