

using System.Windows.Data;

namespace Wpf.Ui.Converters;

internal class RightSplitCornerRadiusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CornerRadius cornerRadius)
        {
            return default(CornerRadius);
        }

        return new CornerRadius(0, cornerRadius.TopRight, cornerRadius.BottomRight, 0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
