

using Wpf.Ui.Controls;

namespace Wpf.Ui.Gallery.ViewModels.Pages.StatusAndInfo;

public partial class InfoBadgeViewModel : ViewModel
{
    [ObservableProperty]
    private InfoBadgeSeverity _infoBadgeSeverity = InfoBadgeSeverity.Attention;

    private int _infoBadgeSeverityComboBoxSelectedIndex = 0;

    public int InfoBadgeSeverityComboBoxSelectedIndex
    {
        get => _infoBadgeSeverityComboBoxSelectedIndex;
        set
        {
            _ = SetProperty(ref _infoBadgeSeverityComboBoxSelectedIndex, value);
            InfoBadgeSeverity = ConvertIndexToInfoBadgeSeverity(value);
        }
    }

    private static InfoBadgeSeverity ConvertIndexToInfoBadgeSeverity(int value)
    {
        return value switch
        {
            1 => InfoBadgeSeverity.Informational,
            2 => InfoBadgeSeverity.Success,
            3 => InfoBadgeSeverity.Caution,
            4 => InfoBadgeSeverity.Critical,
            _ => InfoBadgeSeverity.Attention,
        };
    }
}
