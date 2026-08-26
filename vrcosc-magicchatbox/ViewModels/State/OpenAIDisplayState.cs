using CommunityToolkit.Mvvm.ComponentModel;

namespace vrcosc_magicchatbox.ViewModels.State;

public sealed partial class OpenAIDisplayState : ObservableObject
{
    [ObservableProperty]
    private bool _connected = false;

    [ObservableProperty]
    private string _accessErrorTxt = string.Empty;

    [ObservableProperty]
    private bool _accessError = false;
}
