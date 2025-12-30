

using Wpf.Ui.Controls;

namespace Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

public partial class ThumbRateViewModel : ViewModel
{
    [ObservableProperty]
    private string _thumRateStateText = "Liked";

    [ObservableProperty]
    private string _thumRateStateCodeText = "<ui:ThumbRate State=\"Liked\" />";

    private ThumbRateState _thumbRateState = ThumbRateState.Liked;

    public ThumbRateState ThumbRateState
    {
        get => _thumbRateState;
        set
        {
            ThumRateStateText = value switch
            {
                ThumbRateState.Liked => "Liked",
                ThumbRateState.Disliked => "Disliked",
                _ => "None",
            };

            ThumRateStateCodeText = $"<ui:ThumbRate State=\"{ThumRateStateText}\" />";
            _ = SetProperty(ref _thumbRateState, value);
        }
    }
}
