namespace vrcosc_magicchatbox.Services;

public interface IMenuNavigationService
{
    void ActivateSetting(string settingName);

    void NavigateToPage(int pageIndex);

    void NavigateBack();

    void NavigateForward();

    void NavigateToPrivacy();
}
