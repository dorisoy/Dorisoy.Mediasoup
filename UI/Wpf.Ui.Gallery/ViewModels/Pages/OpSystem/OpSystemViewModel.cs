

using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.Models;
using Wpf.Ui.Gallery.Views.Pages.OpSystem;

namespace Wpf.Ui.Gallery.ViewModels.Pages.OpSystem;

public partial class OpSystemViewModel : ViewModel
{
    [ObservableProperty]
    private ICollection<NavigationCard> _navigationCards = new ObservableCollection<NavigationCard>(
        ControlPages
            .FromNamespace(typeof(OpSystemPage).Namespace!)
            .Select(x => new NavigationCard()
            {
                Name = x.Name,
                Icon = x.Icon,
                Description = x.Description,
                PageType = x.PageType,
            })
    );
}
