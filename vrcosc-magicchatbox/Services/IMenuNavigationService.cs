namespace vrcosc_magicchatbox.Services;

/// <summary>
/// In-app menu/tab navigation — switches pages and activates settings sections.
/// Decouples modules from ViewModel for ActivateSetting calls.
/// </summary>
public interface IMenuNavigationService
{
    /// <summary>
    /// Activates a specific settings section by key (e.g. "Settings_OpenAI").
    /// Switches to the Options page and expands the matching section.
    /// </summary>
    void ActivateSetting(string settingName);

    /// <summary>
    /// Changes the selected menu tab index.
    /// 0=Integrations, 1=Status, 2=Chatting, 3=Options.
    /// </summary>
    void NavigateToPage(int pageIndex);

    /// <summary>
    /// Navigates back to the previous menu tab when history is available.
    /// </summary>
    void NavigateBack();

    /// <summary>
    /// Navigates forward after a back navigation when history is available.
    /// </summary>
    void NavigateForward();

    /// <summary>
    /// Navigates to the Options page and expands the Privacy &amp; Permissions section.
    /// </summary>
    void NavigateToPrivacy();
}
