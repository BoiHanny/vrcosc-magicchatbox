using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
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
