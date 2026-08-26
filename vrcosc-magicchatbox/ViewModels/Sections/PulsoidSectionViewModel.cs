using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
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

public static class PulsoidPreview
{
    public const int SampleHeartRate = 88;

    private static readonly PulsoidStatisticsResponse SampleStats = new()
    {
        average_beats_per_minute = 82,
        calories_burned_in_kcal = 417,
        maximum_beats_per_minute = 143,
        minimum_beats_per_minute = 54,
        streamed_duration_in_seconds = 8_130,
    };

    public static string Render(PulsoidModuleSettings live)
        => live is null
            ? string.Empty
            : PulsoidModule.BuildHeartRateString(Project(live), SampleHeartRate, deviceOnline: true, SampleStats);

    private static PulsoidModuleSettings Project(PulsoidModuleSettings live)
    {
        var symbols = live.SelectedPulsoidTrendSymbol ?? new PulsoidTrendSymbolSet();

        return new PulsoidModuleSettings
        {
            MagicHeartIconPrefix = live.MagicHeartIconPrefix,
            HeartRateIcon = live.HeartRateIcon,
            ShowTemperatureText = live.ShowTemperatureText,
            LowTemperatureThreshold = live.LowTemperatureThreshold,
            LowHeartRateText = live.LowHeartRateText,
            HighTemperatureThreshold = live.HighTemperatureThreshold,
            HighHeartRateText = live.HighHeartRateText,
            ShowBPMSuffix = live.ShowBPMSuffix,
            HeartRateTitle = live.HeartRateTitle,
            CurrentHeartRateTitle = live.CurrentHeartRateTitle,
            SeparateTitleWithEnter = live.SeparateTitleWithEnter,
            PulsoidStatsEnabled = live.PulsoidStatsEnabled,
            HideCurrentHeartRate = live.HideCurrentHeartRate,
            ShowCalories = live.ShowCalories,
            ShowAverageHeartRate = live.ShowAverageHeartRate,
            ShowMaximumHeartRate = live.ShowMaximumHeartRate,
            ShowMinimumHeartRate = live.ShowMinimumHeartRate,
            ShowDuration = live.ShowDuration,
            ShowStatsTimeRange = live.ShowStatsTimeRange,
            SelectedStatisticsTimeRange = live.SelectedStatisticsTimeRange,
            ShowHeartRateTrendIndicator = live.ShowHeartRateTrendIndicator,
            TrendIndicatorBehindStats = live.TrendIndicatorBehindStats,
            SelectedPulsoidTrendSymbol = symbols,

            HeartRateTrendIndicator = symbols.UpwardTrendSymbol,

            EnableHeartRateOfflineCheck = false,
        };
    }
}

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

    [ObservableProperty] private string _previewLine = string.Empty;

    private PulsoidModuleSettings? _watchedSettings;

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

    public void AttachPreview()
    {
        var settings = _moduleHost.Value.Pulsoid?.Settings;
        if (settings == null || ReferenceEquals(settings, _watchedSettings))
            return;

        if (_watchedSettings != null)
            _watchedSettings.PropertyChanged -= OnPulsoidSettingsChanged;

        _watchedSettings = settings;
        settings.PropertyChanged += OnPulsoidSettingsChanged;
        RefreshPreview(settings);
    }

    private void OnPulsoidSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is PulsoidModuleSettings settings)
            RefreshPreview(settings);
    }

    private void RefreshPreview(PulsoidModuleSettings settings)
        => PreviewLine = PulsoidPreview.Render(settings);

    public bool PulsoidAuthConnected
    {
        get => _appState.PulsoidAuthConnected;
        set => _appState.PulsoidAuthConnected = value;
    }

    public PulsoidAuthState PulsoidAuthState
    {
        get => _appState.PulsoidAuthState;
        set => _appState.PulsoidAuthState = value;
    }

    [RelayCommand]
    private async Task ConnectPulsoidAsync()
    {
        try
        {
            var pulsoid = _moduleHost.Value.Pulsoid;
            if (pulsoid == null) return;

            await pulsoid.DisconnectSession();
            string state = Guid.NewGuid().ToString("N");
            const string clientId = Core.Constants.PulsoidClientId;
            const string redirectUri = Core.Constants.PulsoidOAuthRedirectUri;
            const string scope = Core.Constants.PulsoidOAuthScope;
            var authEndpoint = $"{Core.Constants.PulsoidOAuthEndpoint}?response_type=token&client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope={scope}&state={Uri.EscapeDataString(state)}";

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

            string? fragmentString = await oAuth.AuthenticateUserAsync(authEndpoint, state);

            if (string.IsNullOrEmpty(fragmentString))
            {
                _toast.Show("Pulsoid", "Sign-in was cancelled or timed out. Please try again.", ToastType.Warning, key: "pulsoid-auth-timeout");
                return;
            }

            var fragment = PulsoidOAuthHandler.ParseQueryString(fragmentString);
            if (fragment.TryGetValue("access_token", out string? accessToken) && !string.IsNullOrEmpty(accessToken))
            {
                var validation = await oAuth.ValidateTokenAsync(accessToken);
                if (validation == PulsoidTokenValidation.Invalid)
                {
                    _toast.Show("Pulsoid", "Pulsoid rejected the token or it is missing the heart-rate scope. Please reconnect.", ToastType.Error, key: "pulsoid-token-invalid");
                    return;
                }

                pulsoid.Settings.AccessTokenOAuth = accessToken;
                pulsoid.SaveSettings();

                if (validation == PulsoidTokenValidation.Unknown)
                {
                    PulsoidAuthState = PulsoidAuthState.Unreachable;
                    _toast.Show("Pulsoid", "Signed in, but Pulsoid could not be reached to confirm the token. It has been saved and will be retried.", ToastType.Warning, key: "pulsoid-token-unverified");
                }
                else
                {
                    PulsoidAuthState = PulsoidAuthState.Authenticated;
                }

                if (pulsoid.Settings.TokenEncryptionFailed)
                {
                    _toast.Show("Pulsoid", "Signed in, but Windows could not encrypt the token for storage — heart rate works now, and you will need to reconnect after a restart.", ToastType.Warning, key: "pulsoid-token-protect-failed");
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
        pulsoid.Settings.ClearStoredToken();
        pulsoid.SaveSettings();
        PulsoidAuthState = PulsoidAuthState.NoToken;
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
