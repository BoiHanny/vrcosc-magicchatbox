using System;
using System.Windows;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.UI.Dialogs
{
    /// <summary>
    /// Interaction logic for ManualPulsoidAuth.xaml
    /// </summary>
    public partial class ManualPulsoidAuth : Window
    {
        private readonly PulsoidOAuthHandler _oauthHandler;
        private readonly PulsoidModule _heartRateConnector;
        private readonly Action<bool> _setPulsoidAuth;
        private readonly INavigationService _nav;

        public ManualPulsoidAuth(PulsoidModule heartRateConnector, Action<bool> setPulsoidAuth, PulsoidOAuthHandler oauthHandler, INavigationService nav)
        {
            InitializeComponent();
            _heartRateConnector = heartRateConnector;
            _setPulsoidAuth = setPulsoidAuth;
            _oauthHandler = oauthHandler;
            _nav = nav;
        }

        private void Button_close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }


        private void ConnectWithPulsoidWeb_Click(object sender, RoutedEventArgs e)
        {
            string state = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            const string clientId = Core.Constants.PulsoidClientId;
            const string redirectUri = Core.Constants.PulsoidOAuthRedirectUri;
            const string scope = Core.Constants.PulsoidOAuthScope;
            var authorizationEndpoint = $"{Core.Constants.PulsoidOAuthEndpoint}?response_type=token&client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope={scope}&state={state}&response_mode=web_page";

            _nav.OpenUrl(authorizationEndpoint);

            FirstPage.Visibility = Visibility.Hidden;
            SecondPage.Visibility = Visibility.Visible;

            CenterWindowAtBottom();
            Topmost = true;
            Activate();
            Focus();
        }

        private void CenterWindowAtBottom()
        {
            var currentScreen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);

            double centerX = currentScreen.WorkingArea.Left + currentScreen.WorkingArea.Width / 2;
            double bottomY = currentScreen.WorkingArea.Bottom;

            Left = centerX - this.Width / 2;
            Top = bottomY - this.Height;
        }



        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            string token = ExtractAccessToken(Token.Password);
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show("Missing access token, please try again.", "Invalid token", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            bool isValidToken = await _oauthHandler.ValidateTokenAsync(token);

            if (isValidToken)
            {
                _heartRateConnector.Settings.AccessTokenOAuth = token;
                _setPulsoidAuth(true);
                this.Close();
            }
            else
            {
                MessageBox.Show("Invalid token, please try again.", "Invalid token", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void Token_PasswordChanged(object sender, RoutedEventArgs e)
        {
            string token = ExtractAccessToken(Token.Password);
            Connect.IsEnabled = !string.IsNullOrWhiteSpace(token);
        }

        private static string ExtractAccessToken(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            string trimmed = input.Trim();

            int hashIndex = trimmed.IndexOf('#');
            if (hashIndex >= 0 && hashIndex < trimmed.Length - 1)
            {
                trimmed = trimmed.Substring(hashIndex + 1);
            }

            int queryIndex = trimmed.IndexOf('?');
            if (queryIndex >= 0 && queryIndex < trimmed.Length - 1)
            {
                trimmed = trimmed.Substring(queryIndex + 1);
            }

            int tokenIndex = trimmed.IndexOf("access_token=", StringComparison.OrdinalIgnoreCase);
            if (tokenIndex >= 0)
            {
                trimmed = trimmed.Substring(tokenIndex + "access_token=".Length);
            }

            int ampIndex = trimmed.IndexOf('&');
            if (ampIndex >= 0)
            {
                trimmed = trimmed.Substring(0, ampIndex);
            }

            return trimmed.Trim();
        }


        private void ClearAndPaste_Click(object sender, RoutedEventArgs e)
        {
            Token.Clear();
            Token.Paste();
        }
    }
}
