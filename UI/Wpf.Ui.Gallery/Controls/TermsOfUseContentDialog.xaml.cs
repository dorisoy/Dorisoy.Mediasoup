

using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace Wpf.Ui.Gallery.Controls;

public partial class TermsOfUseContentDialog : ContentDialog
{
    public TermsOfUseContentDialog(ContentPresenter? contentPresenter)
        : base(contentPresenter)
    {
        InitializeComponent();
    }

    protected override void OnButtonClick(ContentDialogButton button)
    {
        if (CheckBox.IsChecked != false)
        {
            base.OnButtonClick(button);
            return;
        }

        TextBlock.SetCurrentValue(VisibilityProperty, Visibility.Visible);
        _ = CheckBox.Focus();
    }
}
