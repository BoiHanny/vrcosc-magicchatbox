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
                const string scope = "data:heart_rate:read";
                var authorizationEndpoint = $"https://pulsoid.net/oauth2/authorize?response_type=token&client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope={scope}&state={state}&response_mode=web_page";

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = authorizationEndpoint,
                    UseShellExecute = true
                });


            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }

        private void ConnectWithPulsoidWeb_Click(object sender, RoutedEventArgs e)
        {
            _ = ConnectPulSOidWebTask();

            FirstPage.Visibility = Visibility.Hidden;
            SecondPage.Visibility = Visibility.Visible;

            CenterWindowAtBottom();
            Topmost = true;
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

            bool isValidToken = await oauthHandler.ValidateTokenAsync(Token.Password);

            if (isValidToken)
            {
                ViewModel.Instance.PulsoidAccessTokenOAuth = Token.Password;
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
            string password = Token.Password;

            if (IsValidGuid(password))
            {
                Connect.IsEnabled = true;
            }
            else
            {
                Connect.IsEnabled = false;
            }
        }

        private bool IsValidGuid(string str)
        {
            Guid guidOutput;
            return Guid.TryParse(str, out guidOutput);
        }


        private void ClearAndPaste_Click(object sender, RoutedEventArgs e)
        {
            Token.Clear();
            Token.Paste();
        }
    }
}
