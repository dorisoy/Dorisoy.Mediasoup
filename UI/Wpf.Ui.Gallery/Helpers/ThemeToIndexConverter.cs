

using Wpf.Ui.Appearance;

namespace Wpf.Ui.Gallery.Helpers;

internal sealed class ThemeToIndexConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ApplicationTheme.Dark)
        {
            return 1;
        }

        if (value is ApplicationTheme.HighContrast)
        {
            return 2;
        }

        return 0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is 1)
        {
            return ApplicationTheme.Dark;
        }

        if (value is 2)
        {
            return ApplicationTheme.HighContrast;
        }

        return ApplicationTheme.Light;
    }
}
