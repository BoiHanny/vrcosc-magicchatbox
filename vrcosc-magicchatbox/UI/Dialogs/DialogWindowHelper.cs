using System.Linq;
using System.Windows;

namespace vrcosc_magicchatbox.UI.Dialogs;

internal static class DialogWindowHelper
{
    public static void PrepareModal(Window dialog, Window? preferredOwner = null)
    {
        var owner = preferredOwner;

        if (owner is null || !owner.IsLoaded || ReferenceEquals(owner, dialog))
        {
            owner = Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(window => window.IsActive && !ReferenceEquals(window, dialog))
                ?? Application.Current?.MainWindow;
        }

        if (owner is not null && owner.IsLoaded && !ReferenceEquals(owner, dialog))
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            return;
        }

        dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }
}
