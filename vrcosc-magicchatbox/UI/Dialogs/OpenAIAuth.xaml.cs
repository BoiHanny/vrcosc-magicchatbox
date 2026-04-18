using System.Windows;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.UI.Dialogs
{
    /// <summary>
    /// Interaction logic for OpenAIAuth.xaml
    /// </summary>
    public partial class OpenAIAuth : Window
    {
        private readonly OpenAIDisplayState _openAIDisplay;
        private readonly OpenAISettings _openAISettings;
        private readonly OpenAIModule _openAIModule;
        private readonly INavigationService _nav;

        public OpenAIAuth(
            OpenAIDisplayState openAIDisplay,
            ISettingsProvider<OpenAISettings> openAISettingsProvider,
            OpenAIModule openAIModule,
            INavigationService nav)
        {
            InitializeComponent();
            _openAIDisplay = openAIDisplay;
            _openAISettings = openAISettingsProvider.Value;
            _openAIModule = openAIModule;
            _nav = nav;
            _openAIDisplay.Connected = false;
            _openAISettings.AccessTokenEncrypted = string.Empty;
            _openAISettings.OrganizationIDEncrypted = string.Empty;
        }

        private void Button_close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }


        private void OpenOrganizationPage()
        {
            _nav.OpenUrl(Core.Constants.OpenAiOrganizationUrl);
        }

        private void OpenApiKeyPage()
        {
            _nav.OpenUrl(Core.Constants.OpenAiApiKeysUrl);
        }

        private void ConnectWithOpenAI_Click(object sender, RoutedEventArgs e)
        {
            OpenOrganizationPage();

            FirstPage.Visibility = Visibility.Hidden;
            SecondPage.Visibility = Visibility.Visible;
            ThirdPage.Visibility = Visibility.Hidden;

            CenterWindowAtBottom();
            Topmost = true;
        }

        private void CenterWindowAtBottom()
        {
            var currentScreen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);

            double centerX = currentScreen.WorkingArea.Left + currentScreen.WorkingArea.Width / 2;
            double bottomY = currentScreen.WorkingArea.Bottom;

            Left = centerX - this.Width / 2;
            Top = bottomY - this.Height;
        }

        private void NextStep_Click(object sender, RoutedEventArgs e)
        {
            OpenApiKeyPage();

            FirstPage.Visibility = Visibility.Hidden;
            SecondPage.Visibility = Visibility.Hidden;
            ThirdPage.Visibility = Visibility.Visible;

            CenterWindowAtBottom();
            Topmost = true;
        }

        private void OrganizationID_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (OrganizationID.Password.Length > 0 && OrganizationID.Password.StartsWith(Core.Constants.OpenAiOrgIdPrefix))
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
            if (OpenAIToken.Password.Length > 0 && OpenAIToken.Password.StartsWith(Core.Constants.OpenAiApiKeyPrefix))
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
            _openAISettings.AccessToken = OpenAIToken.Password;
            _openAISettings.OrganizationID = OrganizationID.Password;
            _openAIModule.InitializeClient(_openAISettings.AccessToken, _openAISettings.OrganizationID);
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
