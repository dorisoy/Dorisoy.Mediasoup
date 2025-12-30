

using System.Windows.Data;

namespace Wpf.Ui.Converters;

internal class TextToAsteriskConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return new string('*', value?.ToString()?.Length ?? 0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
