

/* TODO: It's too hardcoded, we should define better formula. */

using System.Windows.Data;

namespace Wpf.Ui.Converters;

internal class ProgressThicknessConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double height)
        {
            return height / 8;
        }

        return 12.0d;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
