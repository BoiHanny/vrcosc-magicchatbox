using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.UI.Controls;

public partial class UpdateProgressCard : UserControl
{
    public UpdateProgressCard()
    {
        InitializeComponent();

        if (App.Services is not null)
        {
            DataContext = App.Services.GetRequiredService<AppUpdateState>().Progress;
        }
    }
}
