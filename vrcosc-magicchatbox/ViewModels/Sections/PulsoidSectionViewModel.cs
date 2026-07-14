using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels.Sections;

/// <summary>
/// Section ViewModel for Pulsoid integration options.
/// Complete binding surface for PulsoidSection.xaml.
/// </summary>
public partial class PulsoidSectionViewModel : ObservableObject
{
    private readonly Lazy<IModuleHost> _moduleHost;
    private readonly Lazy<PulsoidOAuthHandler> _pulsoidOAuth;
    private readonly IAppState _appState;
    private readonly INavigationService _nav;
    private readonly IToastService _toast;

    public AppSettings AppSettings { get; }
    public IntegrationSettings IntegrationSettings { get; }
    public PulsoidDisplayState Pulsoid { get; }
    public IModuleHost Modules => _moduleHost.Value;

    public PulsoidOAuthHandler PulsoidOAuth => _pulsoidOAuth.Value;
    public INavigationService Navigation => _nav;

    /// <summary>
    /// Initializes the Pulsoid section ViewModel with the heart-rate module, display state,
    /// settings, app-state, and supporting services.
    /// </summary>
    public PulsoidSectionViewModel(
        Lazy<IModuleHost> moduleHost,
        Lazy<PulsoidOAuthHandler> pulsoidOAuth,
        IAppState appState,
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        PulsoidDisplayState pulsoidDisplay,
        INavigationService nav,
        IToastService toast)
    {
        _moduleHost = moduleHost;
        _pulsoidOAuth = pulsoidOAuth;
        _appState = appState;
        AppSettings = appSettingsProvider.Value;
        IntegrationSettings = integrationSettingsProvider.Value;
        Pulsoid = pulsoidDisplay;
        _nav = nav;
        _toast = toast;
    }

    public bool PulsoidAuthConnected
    {
        get => _appState.PulsoidAuthConnected;
        set => _appState.PulsoidAuthConnected = value;
    }

    [RelayCommand]
    private async Task ConnectPulsoidAsync()
    {
        try
        {
            var pulsoid = _moduleHost.Value.Pulsoid;
            if (pulsoid == null) return;

            await pulsoid.DisconnectSession();
            string state = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            const string clientId = Core.Constants.PulsoidClientId;
            const string redirectUri = Core.Constants.PulsoidOAuthRedirectUri;
            const string scope = Core.Constants.PulsoidOAuthScope;
            var authEndpoint = $"{Core.Constants.PulsoidOAuthEndpoint}?response_type=token&client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope={scope}&state={state}";

            var oAuth = PulsoidOAuth;
            try
            {
                oAuth.StartListeners();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                _toast.Show("Pulsoid", "Could not open the local sign-in ports (7384/7385) — another app may be using them.", ToastType.Error, key: "pulsoid-listener-failed");
                return;
            }

            string fragmentString = await oAuth.AuthenticateUserAsync(authEndpoint);

            if (string.IsNullOrEmpty(fragmentString))
            {
                _toast.Show("Pulsoid", "Sign-in was cancelled or timed out. Please try again.", ToastType.Warning, key: "pulsoid-auth-timeout");
                return;
            }

            var fragment = PulsoidOAuthHandler.ParseQueryString(fragmentString);
            if (fragment.TryGetValue("access_token", out string accessToken) && !string.IsNullOrEmpty(accessToken))
            {
                if (await oAuth.ValidateTokenAsync(accessToken))
                {
                    pulsoid.Settings.AccessTokenOAuth = accessToken;
                    // Persist immediately — waiting for shutdown risks losing the token on crash/kill
                    pulsoid.SaveSettings();
                    PulsoidAuthConnected = true;
                }
                else
                {
                    _toast.Show("Pulsoid", "Pulsoid rejected the token or it is missing the required scopes. Please reconnect.", ToastType.Error, key: "pulsoid-token-invalid");
                }
            }
            else
            {
                _toast.Show("Pulsoid", "Pulsoid did not return an access token. Please try again.", ToastType.Error, key: "pulsoid-token-missing");
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            _toast.Show("Pulsoid", "Connecting to Pulsoid failed unexpectedly. Check the log for details.", ToastType.Error, key: "pulsoid-connect-failed");
        }
        finally
        {
            PulsoidOAuth.StopListeners();
        }
    }

    [RelayCommand]
    private async Task DisconnectPulsoidAsync()
    {
        var pulsoid = _moduleHost.Value.Pulsoid;
        if (pulsoid == null) return;
        pulsoid.Settings.AccessTokenOAuth = string.Empty;
        pulsoid.SaveSettings();
        PulsoidAuthConnected = false;
        await pulsoid.DisconnectSession();
    }

    [RelayCommand]
    private void LearnMoreHeartRate()
        => _nav.OpenUrl(Core.Constants.WikiHeartRateUrl);

    [RelayCommand]
    private void PulsoidPricing()
        => _nav.OpenUrl(Core.Constants.PulsoidPricingUrl);

    [RelayCommand]
    private void PulsoidDiscountLearnMore()
        => _nav.OpenUrl(Core.Constants.WikiPulsoidDiscountUrl);
}
