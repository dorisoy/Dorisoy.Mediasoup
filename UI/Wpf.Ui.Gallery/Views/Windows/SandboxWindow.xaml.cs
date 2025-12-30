

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ViewModels.Windows;
using Wpf.Ui.Gallery.Views.Pages.Samples;

namespace Wpf.Ui.Gallery.Views.Windows;

public partial class SandboxWindow
{
    public SandboxWindowViewModel ViewModel { get; init; }

    public SandboxWindow(SandboxWindowViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();

        MyTestNavigationView.Loaded += (sender, args) =>
        {
            MyTestNavigationView.SetCurrentValue(
                NavigationView.MenuItemsSourceProperty,
                new ObservableCollection<object>()
                {
                    new NavigationViewItem("Home", SymbolRegular.Home24, typeof(SamplePage1)),
                }
            );

            var configurationBasedLogic = true;

            if (configurationBasedLogic)
            {
                _ = MyTestNavigationView.MenuItems.Add(
                    new NavigationViewItem("Test", SymbolRegular.Home24, typeof(SamplePage2))
                );
            }
        };
    }

    private void OnAutoSuggestBoxTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        Debug.WriteLine(
            $"OnAutoSuggestBoxTextChanged: {sender.Text} (ViewModel.AutoSuggestBoxText: {ViewModel.AutoSuggestBoxText})"
        );
    }
}
