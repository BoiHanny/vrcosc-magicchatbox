using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using vrcosc_magicchatbox.Core.Toast;

namespace vrcosc_magicchatbox.UI.Controls;

public partial class ToastOverlay : UserControl
{
    public ToastOverlay()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<IToastService>();
    }
}
