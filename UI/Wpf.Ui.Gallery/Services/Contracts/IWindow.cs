

namespace Wpf.Ui.Gallery.Services.Contracts;

public interface IWindow
{
    event RoutedEventHandler Loaded;

    void Show();
}
