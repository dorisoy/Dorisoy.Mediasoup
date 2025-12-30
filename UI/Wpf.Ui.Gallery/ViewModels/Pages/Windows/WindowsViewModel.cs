

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.Models;
using Wpf.Ui.Gallery.Services;
using Wpf.Ui.Gallery.Views.Windows;

namespace Wpf.Ui.Gallery.ViewModels.Pages.Windows;

public partial class WindowsViewModel(WindowsProviderService windowsProviderService) : ViewModel
{
    [ObservableProperty]
    private WindowCard[] _windowCards =
    [
        new("Monaco", "Visual Studio Code in your WPF app.", SymbolRegular.CodeBlock24, "monaco"),
        new("Editor", "Text editor with tabbed background.", SymbolRegular.ScanText24, "editor"),
#if DEBUG
        new("Sandbox", "Sandbox for controls testing.", SymbolRegular.ScanText24, "sandbox"),
#endif
    ];

    [RelayCommand]
    public void OnOpenWindow(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        switch (value)
        {
            case "monaco":
                windowsProviderService.Show<MonacoWindow>();
                break;

            case "editor":
                windowsProviderService.Show<EditorWindow>();
                break;

            case "sandbox":
                windowsProviderService.Show<SandboxWindow>();
                break;
        }
    }
}
