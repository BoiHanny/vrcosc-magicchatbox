using System;
using System.Threading.Tasks;
using System.Windows;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.Classes.Modules;

namespace vrcosc_magicchatbox.UI.Dialogs
{
    /// <summary>
    /// Interaction logic for ManualPulsoidAuth.xaml
    /// </summary>
    public partial class ManualPulsoidAuth : Window
    {
        static PulsoidOAuthHandler oauthHandler;
        public ManualPulsoidAuth()
        {
            InitializeComponent();
        }

        private void Button_close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        

        private static async Task ConnectPulSOidWebTask()
        {
            try
            {
                string state = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                const string clientId = "1d0717d2-6c8c-47c6-9097-e289cb02a92d";
                const string redirectUri = "http://localhost:7384/";
                const string scope = "data:heart_rate:read,profile:read,data:statistics:read";
                var authorizationEndpoint = $"https://pulsoid.net/oauth2/authorize?response_type=token&client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope={scope}&state={state}&response_mode=web_page";

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = authorizationEndpoint,
                    UseShellExecute = true
                });


            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        private void ConnectWithPulsoidWeb_Click(object sender, RoutedEventArgs e)
        {
            _ = ConnectPulSOidWebTask();

            FirstPage.Visibility = Visibility.Hidden;
            SecondPage.Visibility = Visibility.Visible;

            CenterWindowAtBottom();
            Topmost = true;
            Activate();
            Focus();
        }

        private void CenterWindowAtBottom()
        {
            // Determine which screen the mouse cursor is on.
            var currentScreen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);

            // Calculate center and bottom coordinates.
            double centerX = currentScreen.WorkingArea.Left + currentScreen.WorkingArea.Width / 2;
            double bottomY = currentScreen.WorkingArea.Bottom;

            Left = centerX - this.Width / 2;
            Top = bottomY - this.Height;
        }



        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            oauthHandler = PulsoidOAuthHandler.Instance;

            string token = ExtractAccessToken(Token.Password);
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show("Missing access token, please try again.", "Invalid token", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            bool isValidToken = await oauthHandler.ValidateTokenAsync(token);

            if (isValidToken)
            {
                ViewModel.Instance.PulsoidAccessTokenOAuth = token;
                ViewModel.Instance.PulsoidAuthConnected = true;
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
