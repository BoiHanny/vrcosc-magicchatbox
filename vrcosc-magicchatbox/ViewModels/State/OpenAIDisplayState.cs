using CommunityToolkit.Mvvm.ComponentModel;

namespace vrcosc_magicchatbox.ViewModels.State;

/// <summary>
/// Owns OpenAI connection status and error display state.
/// Extracted from ViewModel to isolate OpenAI runtime display concerns.
/// </summary>
public sealed partial class OpenAIDisplayState : ObservableObject
{
    [ObservableProperty]
    private bool _connected = false;

    [ObservableProperty]
    private string _accessErrorTxt;

    [ObservableProperty]
    private bool _accessError = false;
}
