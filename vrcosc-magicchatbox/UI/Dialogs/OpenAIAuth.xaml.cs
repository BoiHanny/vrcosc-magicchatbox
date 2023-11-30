using System;
using System.Threading.Tasks;
using System.Windows;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes;
using vrcosc_magicchatbox.ViewModels;
using Newtonsoft.Json.Linq;
using vrcosc_magicchatbox.Classes.Modules;

namespace vrcosc_magicchatbox.UI.Dialogs
{
    /// <summary>
    /// Interaction logic for ManualPulsoidAuth.xaml
    /// </summary>
    public partial class OpenAIAuth : Window
    {
        public OpenAIAuth()
        {
            InitializeComponent();
            ViewModel.Instance.OpenAIConnected = false;
            ViewModel.Instance.OpenAIAccessTokenEncrypted = string.Empty;
            ViewModel.Instance.OpenAIOrganizationIDEncrypted = string.Empty;
        }

        private void Button_close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        

        private static async Task CreateOrganizationIDPage()
        {
            try
            {
                var authorizationEndpoint = $"https://platform.openai.com/account/organization";

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

        private static async Task CreateApiKeyPage()
        {
            try
            {
                var authorizationEndpoint = $"https://platform.openai.com/api-keys";

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

        private void ConnectWithOpenAI_Click(object sender, RoutedEventArgs e)
        {
            _ = CreateOrganizationIDPage();

            FirstPage.Visibility = Visibility.Hidden;
            SecondPage.Visibility = Visibility.Visible;
            ThirdPage.Visibility = Visibility.Hidden;

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

        private void NextStep_Click(object sender, RoutedEventArgs e)
        {
            _ = CreateApiKeyPage();

            FirstPage.Visibility = Visibility.Hidden;
            SecondPage.Visibility = Visibility.Hidden;
            ThirdPage.Visibility = Visibility.Visible;

            CenterWindowAtBottom();
            Topmost = true;
        }

        private void OrganizationID_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (OrganizationID.Password.Length > 0 && OrganizationID.Password.StartsWith("org-"))
            {
                NextStep.IsEnabled = true;
            }
            else
            {
                NextStep.IsEnabled = false;
            }
        }

        private void OpenAIToken_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (OpenAIToken.Password.Length > 0 && OpenAIToken.Password.StartsWith("sk-"))
            {
                Connect.IsEnabled = true;
            }
            else
            {
                Connect.IsEnabled = false;
            }
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.OpenAIAccessToken = OpenAIToken.Password;
            ViewModel.Instance.OpenAIOrganizationID = OrganizationID.Password;
            OpenAIModule.Instance.InitializeClient(ViewModel.Instance.OpenAIAccessToken, ViewModel.Instance.OpenAIOrganizationID);
            this.Close();
        }

        private void ClearAndPaste_OrgID_Click(object sender, RoutedEventArgs e)
        {
            OrganizationID.Password = string.Empty;
            OrganizationID.Paste();
        }

        private void ClearAndPasteAPIToken_Click(object sender, RoutedEventArgs e)
        {
            OpenAIToken.Password = string.Empty;
            OpenAIToken.Paste();
        }
    }
}
