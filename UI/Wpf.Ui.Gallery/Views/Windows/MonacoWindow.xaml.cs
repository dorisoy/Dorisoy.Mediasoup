

using Wpf.Ui.Gallery.ViewModels.Windows;

namespace Wpf.Ui.Gallery.Views.Windows;

public partial class MonacoWindow
{
    public MonacoWindowViewModel ViewModel { get; init; }

    public MonacoWindow(MonacoWindowViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();

        ViewModel.SetWebView(WebView);
    }
}
