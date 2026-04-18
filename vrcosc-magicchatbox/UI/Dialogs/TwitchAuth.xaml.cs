using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using TwitchLib.Api;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.UI.Dialogs;

/// <summary>
/// Multi-step dialog for entering and validating Twitch API credentials (Client ID + OAuth token).
/// </summary>
public partial class TwitchAuth : Window
{
    private readonly ISettingsProvider<TwitchSettings> _settingsProvider;
    private readonly INavigationService _nav;

    private string _localClientId = string.Empty;
    private string _localToken = string.Empty;
    private bool _testPassed;
    private string _loginFromValidation = string.Empty;

    public TwitchAuth(ISettingsProvider<TwitchSettings> settingsProvider, INavigationService nav)
    {
        InitializeComponent();
        _settingsProvider = settingsProvider;
        _nav = nav;
    }

    private void OpenDevConsole_Click(object sender, RoutedEventArgs e) =>
        _nav.OpenUrl(Constants.TwitchDevConsoleUrl);

    private void ToPage2_Click(object sender, RoutedEventArgs e)
    {
        FirstPage.Visibility = Visibility.Hidden;
        SecondPage.Visibility = Visibility.Visible;
    }

    private void BackToPage1_Click(object sender, RoutedEventArgs e)
    {
        SecondPage.Visibility = Visibility.Hidden;
        FirstPage.Visibility = Visibility.Visible;
    }

    private void ToPage3_Click(object sender, RoutedEventArgs e)
    {
        SecondPage.Visibility = Visibility.Hidden;
        ThirdPage.Visibility = Visibility.Visible;
    }

    private void BackToPage2_Click(object sender, RoutedEventArgs e)
    {
        ThirdPage.Visibility = Visibility.Hidden;
        SecondPage.Visibility = Visibility.Visible;
    }

    private void OpenTokenGenerator_Click(object sender, RoutedEventArgs e) =>
        _nav.OpenUrl(Constants.TwitchTokenGeneratorUrl);

    private void ClientIdBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _localClientId = ClientIdBox.Password;
        ToPage3.IsEnabled = !string.IsNullOrWhiteSpace(_localClientId);
    }

    private void PasteClientId_Click(object sender, RoutedEventArgs e)
    {
        var text = Clipboard.GetText().Trim();
        if (string.IsNullOrEmpty(text)) return;
        ClientIdBox.Password = text;
        _localClientId = text;
        ToPage3.IsEnabled = true;
    }

    private void TokenBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _localToken = TokenBox.Password;
        _testPassed = false;
        SaveAndClose.IsEnabled = false;
        TestConnection.IsEnabled = !string.IsNullOrWhiteSpace(_localToken);
        StatusText.Text = string.Empty;
        StatusText.Foreground = Brushes.LightYellow;
    }

    private void PasteToken_Click(object sender, RoutedEventArgs e)
    {
        var text = Clipboard.GetText().Trim();
        if (string.IsNullOrEmpty(text)) return;
        TokenBox.Password = text;
        _localToken = text;
        _testPassed = false;
        SaveAndClose.IsEnabled = false;
        TestConnection.IsEnabled = true;
        StatusText.Text = string.Empty;
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        TestConnection.IsEnabled = false;
        SaveAndClose.IsEnabled = false;
        _testPassed = false;
        SetStatus("Validating token…", Brushes.LightYellow);

        try
        {
            var api = new TwitchAPI();
            api.Settings.ClientId = _localClientId;
            api.Settings.AccessToken = _localToken;

            var validation = await api.Auth.ValidateAccessTokenAsync(_localToken).ConfigureAwait(false);

            if (validation == null)
            {
                SetStatus("❌ Token invalid — Twitch rejected the token.", Brushes.OrangeRed);
                return;
            }

            _loginFromValidation = validation.Login ?? string.Empty;
            var scopes = validation.Scopes ?? new List<string>();
            bool hasFollower = scopes.Contains("moderator:read:followers", StringComparer.OrdinalIgnoreCase);

            if (!hasFollower)
            {
                SetStatus(
                    $"⚠️ Token valid (user: {_loginFromValidation}) but missing moderator:read:followers — " +
                    "follower count will not work. Stream info and viewer count will still work.",
                    Brushes.Yellow);
            }
            else
            {
                SetStatus($"✅ Connected as {_loginFromValidation} — all required scopes present.", Brushes.LightGreen);
            }

            _testPassed = true;
            SaveAndClose.IsEnabled = true;
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"TwitchAuth test failed: {ex.Message}");
            SetStatus($"❌ Error: {ex.Message}", Brushes.OrangeRed);
        }
        finally
        {
            TestConnection.IsEnabled = true;
        }
    }

    private void SaveAndClose_Click(object sender, RoutedEventArgs e)
    {
        if (!_testPassed) return;

        var settings = _settingsProvider.Value;
        settings.ClientId = _localClientId;
        settings.AccessToken = _localToken;

        if (string.IsNullOrWhiteSpace(settings.ChannelName) && !string.IsNullOrWhiteSpace(_loginFromValidation))
            settings.ChannelName = _loginFromValidation;

        _settingsProvider.Save();
        Close();
    }

    private void SetStatus(string text, Brush color)
    {
        StatusText.Foreground = color;
        StatusText.Text = text;
    }
}
