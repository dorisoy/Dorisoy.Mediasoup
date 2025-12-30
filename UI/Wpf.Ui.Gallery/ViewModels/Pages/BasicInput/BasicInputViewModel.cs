

using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.Models;
using Wpf.Ui.Gallery.Views.Pages.BasicInput;

namespace Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

public partial class BasicInputViewModel : ViewModel
{
    [ObservableProperty]
    private ICollection<NavigationCard> _navigationCards = new ObservableCollection<NavigationCard>(
        ControlPages
            .FromNamespace(typeof(BasicInputPage).Namespace!)
            .Select(x => new NavigationCard()
            {
                Name = x.Name,
                Icon = x.Icon,
                Description = x.Description,
                PageType = x.PageType,
            })
    );
}
