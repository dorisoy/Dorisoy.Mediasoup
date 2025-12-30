

namespace Wpf.Ui.Gallery.ViewModels.Pages.OpSystem;

public partial class ClipboardViewModel : ViewModel
{
    [ObservableProperty]
    private string _textToCopy = "This text will be copied to the clipboard.";

    [ObservableProperty]
    private string _clipboardContent = "Click the button!";

    [ObservableProperty]
    private Visibility _textCopiedVisibility = Visibility.Collapsed;

    [RelayCommand]
    private async Task OnCopyTextToClipboard()
    {
        try
        {
            Clipboard.Clear();
            Clipboard.SetText(TextToCopy);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }

        if (TextCopiedVisibility == Visibility.Visible)
        {
            return;
        }

        TextCopiedVisibility = Visibility.Visible;

        await Task.Delay(5000);

        TextCopiedVisibility = Visibility.Collapsed;
    }

    [RelayCommand]
    private void OnParseTextFromClipboard()
    {
        try
        {
            ClipboardContent = Clipboard.GetText();
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }
}
