using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;

namespace vrcosc_magicchatbox.ViewModels.Sections;

public partial class TikTokLiveSectionViewModel : ObservableObject
{
    public const long SampleFollowers = 12_300;

    public const long SampleViewers = 842;
    public const long SampleLikes = 15_600;
    public const string SampleProfileName = "yourname";
    public const string SampleDisplayName = "Your Name";
    public const string SampleFan = "newfan";

    private readonly Lazy<IModuleHost> _moduleHost;

    public AppSettings AppSettings { get; }
    public IntegrationSettings IntegrationSettings { get; }
    public TikTokLiveSettings TikTokSettings { get; }
    public IModuleHost Modules => _moduleHost.Value;

    [ObservableProperty] private string _profileSamplePreview = string.Empty;

    [ObservableProperty] private string _liveSamplePreview = string.Empty;

    [ObservableProperty] private string _combinedSamplePreview = string.Empty;

    [ObservableProperty] private string _commentSamplePreview = string.Empty;

    public TikTokLiveDisplayMode[] DisplayModes { get; } =
    [
        TikTokLiveDisplayMode.SummaryOnly,
        TikTokLiveDisplayMode.EventOverlay,
        TikTokLiveDisplayMode.TransientOnly
    ];

    public TikTokOutputOrder[] OutputOrders { get; } =
    [
        TikTokOutputOrder.ProfileThenLive,
        TikTokOutputOrder.LiveThenProfile
    ];

    public TikTokLiveSectionViewModel(
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        ISettingsProvider<TikTokLiveSettings> tikTokSettingsProvider,
        Lazy<IModuleHost> moduleHost)
    {
        AppSettings = appSettingsProvider.Value;
        IntegrationSettings = integrationSettingsProvider.Value;
        TikTokSettings = tikTokSettingsProvider.Value;
        _moduleHost = moduleHost;

        TikTokSettings.PropertyChanged += OnTikTokSettingsChanged;
        RefreshPreviews();
    }

    public static Dictionary<string, string> BuildSampleTokens(TikTokLiveSettings settings)
    {
        string profile = string.IsNullOrWhiteSpace(settings.ProfileUserName)
            ? SampleProfileName
            : settings.ProfileUserName.Trim().TrimStart('@');

        string host = string.IsNullOrWhiteSpace(settings.HostUserName)
            ? profile
            : settings.HostUserName.Trim().TrimStart('@');

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["live"] = "LIVE",
            ["user"] = SampleFan,
            ["unique_id"] = SampleFan,
            ["message"] = "loving the stream",
            ["gift"] = "Rose",
            ["count"] = "3",
            ["amount"] = "3",
            ["total"] = SampleLikes.ToString(CultureInfo.InvariantCulture),
            ["host"] = host,
            ["viewers"] = TikTokLiveOutput.ChatCount(SampleViewers, settings.CompactViewerCount),
            ["viewer_count"] = SampleViewers.ToString(CultureInfo.InvariantCulture),
            ["likes"] = TikTokLiveOutput.ChatCount(SampleLikes, settings.CompactLikeCount),
            ["like_count"] = SampleLikes.ToString(CultureInfo.InvariantCulture),
            ["room"] = "7123456789",
            ["profile"] = profile,
            ["display_name"] = SampleDisplayName,
            ["followers"] = TikTokLiveOutput.ChatCount(SampleFollowers, settings.CompactViewerCount),
            ["follower_count"] = SampleFollowers.ToString(CultureInfo.InvariantCulture),
            ["change"] = "12",
            ["change_count"] = "12",
            ["updated"] = "20:15"
        };
    }

    public static string CombineSample(TikTokLiveSettings settings, string profileLine, string liveLine)
    {
        string first = settings.OutputOrder == TikTokOutputOrder.ProfileThenLive ? profileLine : liveLine;
        string second = settings.OutputOrder == TikTokOutputOrder.ProfileThenLive ? liveLine : profileLine;

        if (string.IsNullOrWhiteSpace(first))
            return second?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(second))
            return first.Trim();

        string separator = string.IsNullOrEmpty(settings.CombinedOutputSeparator)
            ? " | "
            : settings.CombinedOutputSeparator.Replace("\\n", "\n", StringComparison.Ordinal);

        return $"{first.Trim()}{separator}{second.Trim()}";
    }

    [RelayCommand]
    private async Task RefreshProfileAsync()
    {
        var tikTok = Modules.TikTokLive;
        if (tikTok == null)
            return;

        await tikTok.RefreshProfileAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        var tikTokLive = Modules.TikTokLive;
        if (tikTokLive == null)
            return;

        await tikTokLive.StartAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        var tikTokLive = Modules.TikTokLive;
        if (tikTokLive == null)
            return;

        await tikTokLive.StopAsync().ConfigureAwait(false);
    }

    private void OnTikTokSettingsChanged(object? sender, PropertyChangedEventArgs e) => RefreshPreviews();

    private void RefreshPreviews()
    {
        var tokens = BuildSampleTokens(TikTokSettings);

        ProfileSamplePreview = TikTokSettings.ShowProfileSummary
            ? TikTokLiveOutput.Render(TikTokSettings.ProfileTemplate, tokens)
            : string.Empty;

        LiveSamplePreview = TikTokSettings.EnableLiveConnector
            ? TikTokLiveOutput.Render(TikTokSettings.SummaryTemplate, tokens)
            : string.Empty;

        CombinedSamplePreview = TikTokSettings.CombineProfileAndLive
            ? CombineSample(TikTokSettings, ProfileSamplePreview, LiveSamplePreview)
            : string.IsNullOrWhiteSpace(LiveSamplePreview) ? ProfileSamplePreview : LiveSamplePreview;

        CommentSamplePreview = TikTokLiveOutput.Render(TikTokSettings.CommentTemplate, tokens);
    }
}
