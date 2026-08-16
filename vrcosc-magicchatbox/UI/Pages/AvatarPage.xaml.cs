using System.Windows;
using System.Windows.Controls;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.UI.Pages;

public partial class AvatarPage : UserControl
{
    public AvatarPage()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is not AvatarPageViewModel vm)
            return;

        if (e.NewValue is true)
            vm.Activate();
        else
            vm.Deactivate();
    }
}
