using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Shell;
using System.Windows.Threading;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.UI.Dialogs;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox
{
    public partial class MainWindow : Window
    {
        private const int WM_ENTERSIZEMOVE = 0x0231;
        private const int WM_EXITSIZEMOVE = 0x0232;
        private ResizeMode previousResizeMode = ResizeMode.CanResize;
        private static List<CancellationTokenSource> _activeCancellationTokens = new List<CancellationTokenSource>();
        private static double _shadowOpacity;
        public static readonly DependencyProperty ShadowOpacityProperty = DependencyProperty.Register(
            "ShadowOpacity",
            typeof(double),
            typeof(MainWindow),
            new PropertyMetadata(0.0));

        DispatcherTimer backgroundCheck = new DispatcherTimer();
        private System.Timers.Timer ChatUpdateTimer;
        private System.Timers.Timer pauseTimer;
        public float samplingTime = 1;
        private DateTime _nextRun = DateTime.Now;
        private bool isProcessing = false;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            IntPtr handle = (new System.Windows.Interop.WindowInteropHelper(this)).Handle;
            System.Windows.Interop.HwndSource.FromHwnd(handle)?.AddHook(WindowProc);

            this.StateChanged += MainWindow_StateChanged;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowChrome.GetWindowChrome(this).GlassFrameThickness = new Thickness(0);
                this.BorderThickness = new Thickness(8);
            }
            else
            {
                WindowChrome.GetWindowChrome(this).GlassFrameThickness = new Thickness(1);
                this.BorderThickness = new Thickness(0);
            }
        }

        private IntPtr WindowProc(IntPtr hwnd, int uMsg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (uMsg)
            {
                case WM_ENTERSIZEMOVE:
                    if (ResizeMode == ResizeMode.CanResize || ResizeMode == ResizeMode.CanResizeWithGrip)
                    {
                        previousResizeMode = ResizeMode;
                        ResizeMode = ResizeMode.NoResize; // Prevent resizing
                        OnStartResize();
                    }
                    break;

                case WM_EXITSIZEMOVE:
                    if (ResizeMode == ResizeMode.NoResize)
                    {
                        ResizeMode = previousResizeMode; // Allow resizing again
                        OnEndResize();
                    }
                    break;
            }

            return IntPtr.Zero;
        }



        private void OnStartResize()
        {
            WindowChrome windowChrome = WindowChrome.GetWindowChrome(this);
            windowChrome.GlassFrameThickness = new Thickness(0);
        }

        private void OnEndResize()
        {
            WindowChrome windowChrome = WindowChrome.GetWindowChrome(this);
            windowChrome.GlassFrameThickness = new Thickness(1);
        }




        public MainWindow()
        {
            InitializeComponent();
            ApplyIntegrationOrder();
            Closing += MainWindow_ClosingAsync;

            backgroundCheck.Tick += Timer;
            backgroundCheck.Interval = TimeSpan.FromMilliseconds(ViewModel.Instance.ScanningInterval * 1000);
            backgroundCheck.Start();

            // Asynchronous Initialization
            Task initTask = InitializeAsync();
            ViewModel.Instance.WhisperModule = new WhisperModule();
            ViewModel.Instance.WhisperModule.TranscriptionReceived += WhisperModule_TranscriptionReceived;
            ViewModel.Instance.WhisperModule.SentChatMessage += WhisperModule_SentChat;

            ViewModel.Instance.AfkModule = new AfkModule();
            ViewModel.Instance.AfkModule.AfkDetected += AfkModule_AfkDetected;


        }

        public void ApplyIntegrationOrder()
        {
            if (IntegrationsList == null)
            {
                return;
            }

            var itemMap = new Dictionary<string, ListBoxItem>(StringComparer.OrdinalIgnoreCase)
            {
                { "Status", StatusItem },
                { "Window", WindowActivityItem },
                { "HeartRate", HeartRateItem },
                { "Component", ComponentStatsItem },
                { "Network", NetworkStatsItem },
                { "Time", TimeItem },
                { "Weather", WeatherItem },
                { "Twitch", TwitchItem },
                { "Soundpad", SoundpadItem },
                { "Spotify", SpotifyItem },
                { "MediaLink", MediaLinkItem }
            };

            IEnumerable<string> orderedKeys = ViewModel.Instance.IntegrationSortOrder?.Count > 0
                ? ViewModel.Instance.IntegrationSortOrder
                : ViewModel.DefaultIntegrationSortOrder;

            var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            IntegrationsList.BeginInit();
            IntegrationsList.Items.Clear();

            foreach (var key in orderedKeys)
            {
                if (itemMap.TryGetValue(key, out var item))
                {
                    IntegrationsList.Items.Add(item);
                    usedKeys.Add(key);
                }
            }

            foreach (var kvp in itemMap)
            {
                if (!usedKeys.Contains(kvp.Key))
                {
                    IntegrationsList.Items.Add(kvp.Value);
                }
            }

            IntegrationsList.EndInit();
        }

        private void ReorderIntegrations_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new UI.Dialogs.ReorderIntegrations
            {
                Owner = this
            };
            dialog.ShowDialog();
        }

        private void WhisperModule_SentChat()
        {
            Dispatcher.Invoke(() =>
            {
                ButtonChattingTxt_Click(null, null);
            });

        }

        private void AfkModule_AfkDetected(object? sender, EventArgs e)
        {
            //throw new NotImplementedException();
        }

        private void WhisperModule_TranscriptionReceived(string newTranscription)
        {
            // Add new transcription to the existing text
            string currentText = ViewModel.Instance.NewChattingTxt + " " + newTranscription;

            // Trim the text to the last 140 characters
            string trimmedText = TrimToLastMaxCharacters(currentText, 140);

            // Update the ViewModel's property
            ViewModel.Instance.NewChattingTxt = trimmedText;
        }

        private string TrimToLastMaxCharacters(string text, int maxCharacters)
        {
            // If the text is already within the limit, no need to trim
            if (text.Length <= maxCharacters) return text;

            // Find the index of the first space, starting from the character at the limit
            // This ensures we don't start in the middle of a word
            int firstSpaceIndex = text.IndexOf(' ', text.Length - maxCharacters);
            if (firstSpaceIndex == -1)
            {
                // If there's no space, it means there's a very long word; trim to the limit directly
                return text.Substring(text.Length - maxCharacters);
            }

            // Return the substring from the first space to the end of the original text
            // This trims out the oldest words
            return text.Substring(firstSpaceIndex).Trim();
        }


        public async Task InitializeAsync()
        {
            SelectTTS();
            SelectTTSOutput();
            ChangeMenuItem(ViewModel.Instance.CurrentMenuItem);
            Task.Run(() => scantick(true));
        }

        public static event EventHandler ShadowOpacityChanged;

        private void AddFav_Click(object sender, RoutedEventArgs e)
        {
            Random random = new Random();
            int randomId = random.Next(10, 99999999);
            bool IsActive = false;
            if (ViewModel.Instance.StatusList.Count() == 0)
            {
                IsActive = true;
            }

            if (ViewModel.Instance.NewStatusItemTxt.Count() > 0 && ViewModel.Instance.NewStatusItemTxt.Count() < 141)
            {
                ViewModel.Instance.StatusList
                    .Add(
                        new StatusItem
                        {
                            CreationDate = DateTime.Now,
                            IsActive = IsActive,
                            IsFavorite = false,
                            msg = ViewModel.Instance.NewStatusItemTxt,
                            MSGID = randomId
                        });
                ViewModel.Instance.StatusList = new ObservableCollection<StatusItem>(
                    ViewModel.Instance.StatusList.OrderByDescending(x => x.CreationDate));
                if (ViewModel.Instance.NewStatusItemTxt.ToLower() == "sr4 series" ||
                    ViewModel.Instance.NewStatusItemTxt.ToLower() == "boihanny")
                {
                    ViewModel.Instance.Egg_Dev = true;
                    MessageBox.Show(
                        "u found the dev egggmoooodeee go to options",
                        "Egg",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                if (ViewModel.Instance.NewStatusItemTxt.ToLower() == "bussyboys")
                {
                    ViewModel.Instance.BussyBoysMode = true;
                    MessageBox.Show(
                        "Bussy Boysss letsss goooo, go to afk options",
                        "Egg",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                ViewModel.Instance.NewStatusItemTxt = string.Empty;
                ViewModel.SaveStatusList();
            }
        }


        private void Button_close_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Hidden;
            this.Close();
        }

        private void Button_minimize_Click(object sender, RoutedEventArgs e)
        { this.WindowState = WindowState.Minimized; }


        private void ButtonChattingTxt_Click(object sender, RoutedEventArgs e)
        {
            string chat = ViewModel.Instance.NewChattingTxt;
            if (chat.Length > 0 && chat.Length <= 141 && ViewModel.Instance.MasterSwitch)
            {
                foreach (ChatItem Chatitem in ViewModel.Instance.LastMessages)
                {
                    Chatitem.CanLiveEdit = false;
                    Chatitem.CanLiveEditRun = false;
                    Chatitem.MsgReplace = string.Empty;
                    Chatitem.IsRunning = false;
                }
                OSCController.CreateChat(true);
                int smalldelay = ViewModel.Instance.ChatAddSmallDelay
                    ? (int)(ViewModel.Instance.ChatAddSmallDelayTIME * 1000)
                    : 0;
                OSCSender.SendOSCMessage(ViewModel.Instance.ChatFX, smalldelay);
                DataController.SaveChatList();
                if (ViewModel.Instance.TTSTikTokEnabled == true)
                {
                    if (DataController.PopulateOutputDevices())
                    {
                        ViewModel.Instance.ChatFeedbackTxt = "Requesting TTS...";
                        TTSGOAsync(chat);
                    }
                    else
                    {
                        ViewModel.Instance.ChatFeedbackTxt = "Error setting output device.";
                    }
                }

                Timer(null, null);
                RecentScroll.ScrollToEnd();
            }
        }

        private void CancelEditbutton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var item = button.Tag as StatusItem;
                item.editMsg = string.Empty;
                item.IsEditing = false;
            }
            catch (Exception)
            {
            }
        }

        private void CancelEditChatbutton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var Button = sender as Button;
                ChatItem? lastsendchat = ViewModel.Instance.LastMessages.FirstOrDefault(x => x.IsRunning);

                if (!string.IsNullOrEmpty(lastsendchat.MainMsg))
                {
                    lastsendchat.CancelLiveEdit = true;
                    lastsendchat.CanLiveEditRun = false;
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }

        }

        private void ChatUpdateTimer_Tick(object sender, System.Timers.ElapsedEventArgs e)
        {
            ChatItem? lastsendchat = new ChatItem();
            lastsendchat = ViewModel.Instance.LastMessages.FirstOrDefault(x => x.IsRunning);
            try
            {
                if (ViewModel.Instance.KeepUpdatingChat && lastsendchat != null)
                {
                    if (lastsendchat.Msg.Length > 0 && lastsendchat.Msg.Length <= 141 && ViewModel.Instance.MasterSwitch)
                    {
                        string Complete_msg = null;
                        if (ViewModel.Instance.PrefixChat == true)
                        {
                            string icon = ViewModel.Instance.GetNextEmoji(true);
                            Complete_msg = icon + " " + lastsendchat.Msg;
                        }
                        else
                        {
                            Complete_msg = lastsendchat.Msg;
                        }
                        ViewModel.Instance.OSCtoSent = Complete_msg;
                        OSCSender.SendOSCMessage(false);
                    }
                }
                else
                {
                    foreach (var item in ViewModel.Instance.LastMessages)
                    {
                        item.CanLiveEdit = false;
                        item.CanLiveEditRun = false;
                        item.MsgReplace = string.Empty;
                        item.IsRunning = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        private void CheckUpdateBtnn_Click(object sender, RoutedEventArgs e) { ManualUpdateCheckAsync(); }

        private void ClearChat_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.LastMessages.Clear();
            DataController.SaveChatList();
            StopChat_Click(null, null);
        }

        private void ClearnupKeepSettings_Click(object sender, RoutedEventArgs e)
        {
            int ItemRemoved = WindowActivityModule.CleanAndKeepAppsWithSettings();
            if (ItemRemoved > 0)
            {
                ViewModel.Instance.DeletedAppslabel = $"Removed {ItemRemoved} apps from history";
            }
            else
            {
                ViewModel.Instance.DeletedAppslabel = $"No apps removed from history";
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var item = button.Tag as StatusItem;
                if (item.msg.ToLower() == "sr4 series" || item.msg.ToLower() == "boihanny")
                {
                    ViewModel.Instance.Egg_Dev = false;
                    MessageBox.Show(
                        "damn u left the dev egggmoooodeee",
                        "Egg",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                if (item.msg.ToLower() == "bussyboys")
                {
                    ViewModel.Instance.BussyBoysMode = false;
                    MessageBox.Show(
                        "damn u left the bussyboys mode",
                        "Egg",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                ViewModel.Instance.StatusList.Remove(item);
                ViewModel.SaveStatusList();
            }
            catch (Exception)
            {
            }
        }

        private void Discord_Click(object sender, RoutedEventArgs e)
        { Process.Start("explorer", "https://discord.gg/ZaSFwBfhvG"); }

        private void Drag_area_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void Editbutton_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as ToggleButton;
                var item = button.Tag as StatusItem;
                if ((bool)button.IsChecked)
                {
                    item.editMsg = item.msg;
                    // Find the TextBox in the same container as the ToggleButton
                    var parent = VisualTreeHelper.GetParent(button);
                    while (!(parent is ContentPresenter))
                    {
                        parent = VisualTreeHelper.GetParent(parent);
                    }
                    var contentPresenter = parent as ContentPresenter;
                    var dataTemplate = contentPresenter.ContentTemplate;
                    var editTextBox = (TextBox)dataTemplate.FindName("EditTextBox", contentPresenter);
                    editTextBox.Focus();
                    // Set the cursor at the end of the text
                    editTextBox.CaretIndex = editTextBox.Text.Length;
                }
                else
                {
                    if (item.editMsg.Count() < 145 && !string.IsNullOrEmpty(item.editMsg))
                    {
                        item.msg = item.editMsg;
                        item.IsEditing = false;
                        item.editMsg = string.Empty;
                        item.LastEdited = DateTime.Now;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void EditChatTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            var textbox = sender as TextBox;
            ChatItem? lastsendchat = ViewModel.Instance.LastMessages.FirstOrDefault(x => x.IsRunning);

            if (ViewModel.Instance.RealTimeChatEdit)
            {
                if (e.Key == Key.Enter)
                {
                    lastsendchat.CanLiveEditRun = false;
                    lastsendchat.MainMsg = textbox.Text;
                    NewChattingTxt.Focus();
                    NewChattingTxt.CaretIndex = NewChattingTxt.Text.Length;
                }
                if (e.Key == Key.Escape)
                {
                    if (!string.IsNullOrEmpty(lastsendchat.MainMsg))
                    {
                        lastsendchat.CancelLiveEdit = true;
                        lastsendchat.CanLiveEditRun = false;
                    }
                }
            }
            else
            {
                if (e.Key == Key.Enter)
                {
                    if (textbox != null && lastsendchat != null)
                    {
                        if (lastsendchat.Msg != textbox.Text)
                        {
                            lastsendchat.MainMsg = textbox.Text;
                            lastsendchat.Msg = textbox.Text;
                            lastsendchat.CanLiveEditRun = false;
                            NewChattingTxt.Focus();
                            NewChattingTxt.CaretIndex = NewChattingTxt.Text.Length;
                        }
                    }
                }
                if (e.Key == Key.Escape)
                {
                    if (!string.IsNullOrEmpty(lastsendchat.MainMsg))
                    {
                        lastsendchat.CancelLiveEdit = true;
                        lastsendchat.CanLiveEditRun = false;
                    }
                }
            }
        }

        private void EditChatTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ViewModel.Instance.RealTimeChatEdit)
            {
                var textbox = sender as TextBox;
                ChatItem? lastsendchat = ViewModel.Instance.LastMessages.FirstOrDefault(x => x.IsRunning);

                if (textbox != null && lastsendchat != null)
                {
                    if (lastsendchat.Msg != textbox.Text)
                    {
                        lastsendchat.Msg = textbox.Text;
                    }
                }
            }
        }

        private void FavBox_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Enter)
                {
                    AddFav_Click(sender, e);
                }
                if (e.Key == Key.Escape)
                {
                    ViewModel.Instance.NewStatusItemTxt = string.Empty;
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        private void Favbutton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is StatusItem item)
            {
                item.UseInCycle = !item.UseInCycle;
                ViewModel.SaveStatusList();
            }
        }

        private void Github_Click(object sender, RoutedEventArgs e)
        { Process.Start("explorer", "https://github.com/BoiHanny/vrcosc-magicchatbox"); }

        private void GitHubChanges_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel.Instance.tagURL == null)
            {
                Process.Start("explorer", "http://github.com/BoiHanny/vrcosc-magicchatbox/releases");
            }
            else
            {
                Process.Start("explorer", ViewModel.Instance.tagURL);
            }
        }


        private void LearnMoreAboutHeartbtn_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start(
                "explorer",
                "https://github.com/BoiHanny/vrcosc-magicchatbox/wiki/%F0%9F%A9%B5-Heart-Rate");
        }


        private void LearnMoreAboutSpotifybtn_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start(
                "explorer",
                "https://github.com/BoiHanny/vrcosc-magicchatbox/wiki/%F0%9F%8E%BC-Music-Display");
        }

        private void LearnMoreAboutTTSbtn_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start(
                "explorer",
                "https://github.com/BoiHanny/vrcosc-magicchatbox/wiki/Play-TTS-Output-of-MagicChatbox-to-Main-Audio-Device-and-Microphone-in-VRChat-Using-VB-Audio-Cable-(Simple-Setup)");
        }

        private async Task ManualUpdateCheckAsync()
        {
            var updateCheckTask = DataController.CheckForUpdateAndWait(true);
            var delayTask = Task.Delay(TimeSpan.FromSeconds(8));

            await Task.WhenAny(updateCheckTask, delayTask);
        }



        private void MasterSwitch_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Instance.MasterSwitch == true)
            {

                backgroundCheck.Start();
            }
            else
            {
                backgroundCheck.Stop();
                OSCSender.SentClearMessage(1000);

            }
        }

        private void MenuButton_0_Click(object sender, RoutedEventArgs e) { ChangeMenuItem(0); }

        private void MenuButton_1_Click(object sender, RoutedEventArgs e) { ChangeMenuItem(1); }

        private void MenuButton_2_Click(object sender, RoutedEventArgs e) { ChangeMenuItem(2); }

        private void MenuButton_3_Click(object sender, RoutedEventArgs e) { ChangeMenuItem(3); }

        private void NewChattingTxt_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ButtonChattingTxt_Click(sender, e);
            }
            if (e.Key == Key.Escape)
            {
                ViewModel.Instance.NewChattingTxt = string.Empty;
            }
        }

        private void NewChattingTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            int count = textBox.Text.Count();
            ViewModel.Instance.ChatBoxCount = $"{count.ToString()}/140";
            if (count > 140)
            {
                int overmax = count - 140;
                ViewModel.Instance.ChatBoxColor = "#FFFF9393";
                ViewModel.Instance.ChatTopBarTxt = $"You're soaring past the 140 char limit by {overmax}.";
            }
            else if (count == 0)
            {
                ViewModel.Instance.ChatBoxColor = "#FF504767";
                ViewModel.Instance.ChatTopBarTxt = string.Empty;
            }
            else
            {
                ViewModel.Instance.ChatBoxColor = "#FF2C2148";
                ViewModel.Instance.ChatTopBarTxt = string.Empty;
            }

            OSCSender.SendTypingIndicatorAsync();
        }

        private void NewFavText_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            int count = textBox.Text.Count();
            ViewModel.Instance.StatusBoxCount = $"{count.ToString()}/140";
            if (count > 140)
            {
                int overmax = count - 140;
                ViewModel.Instance.StatusBoxColor = "#FFFF9393";
                ViewModel.Instance.StatusTopBarTxt = $"You're soaring past the 140 char limit by {overmax}. Reign in that message!";
            }
            else if (count == 0)
            {
                ViewModel.Instance.StatusBoxColor = "#FF504767";
                ViewModel.Instance.StatusTopBarTxt = string.Empty;
            }
            else
            {
                ViewModel.Instance.StatusBoxColor = "#FF2C2148";
                if (count > 22)
                {
                    ViewModel.Instance.StatusTopBarTxt = $"Buckle up! Keep it tight to 20-25 or integrations may suffer.";
                }
                else
                {
                    ViewModel.Instance.StatusTopBarTxt = string.Empty;
                }
            }
        }

        private void NewVersion_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if(ViewModel.Instance.UseCustomProfile)
            {
                Logging.WriteException(new Exception("Cannot update while using a custom profile."), MSGBox: true);
                return;
            }


            if (ViewModel.Instance.CanUpdate)
            {
                ViewModel.Instance.CanUpdate = false;
                ViewModel.Instance.CanUpdateLabel = false;
                UpdateApp updateApp = new UpdateApp(true);
                Task.Run(() => updateApp.PrepareUpdate());
            }
            else
            {
                Process.Start("explorer", "http://github.com/BoiHanny/vrcosc-magicchatbox/releases");
            }
        }



        private void OpenConfigFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataController.CreateIfMissing(ViewModel.Instance.DataPath))
                    Process.Start("explorer.exe", ViewModel.Instance.DataPath);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex);
            }
        }

        private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataController.CreateIfMissing(ViewModel.Instance.LogPath))
                    Process.Start("explorer.exe", ViewModel.Instance.LogPath);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex);
            }
        }

        private void PauseTimer_Tick(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                ChatItem? lastsendchat = ViewModel.Instance.LastMessages.FirstOrDefault(x => x.IsRunning);

                ViewModel.Instance.ScanPauseCountDown--;
                Application.Current.Dispatcher
                    .Invoke(
                        () =>
                        {
                            ViewModel.Instance.ScanPauseCountDown = ViewModel.Instance.ScanPauseCountDown;
                            if (lastsendchat != null)
                            {
                                lastsendchat.CanLiveEdit = ViewModel.Instance.ChatLiveEdit;
                                lastsendchat.LiveEditButtonTxt = ViewModel.Instance.RealTimeChatEdit
                                    ? "Live Edit (" + ViewModel.Instance.ScanPauseCountDown + ")"
                                    : "Edit (" + ViewModel.Instance.ScanPauseCountDown + ")";
                            }
                        });

                if (ViewModel.Instance.ScanPauseCountDown <= 0 || !ViewModel.Instance.ScanPause)
                {
                    ViewModel.Instance.ScanPause = false;
                    pauseTimer.Stop();
                    pauseTimer = null;
                    if (ViewModel.Instance.ScanPauseCountDown != 0)
                    {
                        ViewModel.Instance.ScanPauseCountDown = 0;
                    }

                    OSCController.ClearChat(lastsendchat);
                    OSCSender.SendOSCMessage(false);
                    Timer(null, null);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        private void PlaybackOutputDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        { ViewModel.Instance.RecentPlayBackOutput = ViewModel.Instance.SelectedPlaybackOutputDevice.FriendlyName; }

        private void ResetFavorites_Click(object sender, RoutedEventArgs e)
        {
            string xml = System.IO.Path.Combine(ViewModel.Instance.DataPath, "StatusList.xml");
            if (File.Exists(xml))
            {
                File.Delete(xml);
            }
            ViewModel.Instance.StatusList.Clear();
            DataAndSecurity.DataController.LoadStatusList();
            ViewModel.SaveStatusList();
            ChangeMenuItem(1);
        }

        private void ResetWindowActivity_Click(object sender, RoutedEventArgs e)
        {
            int ItemRemoved = WindowActivityModule.ResetWindowActivity();
            if (ItemRemoved > 0)
            {
                ViewModel.Instance.DeletedAppslabel = "All apps from history";
            }
        }

        private async void MainWindow_ClosingAsync(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Cancel the window closing event temporarily to await the async task
            e.Cancel = true;

            try
            {
                // Optionally hide the window while saving data
                Hide();

                // Await your asynchronous save logic
                await SaveDataToDiskAsync();

                // After the async task completes, close the window
                Application.Current.Shutdown(); // This is equivalent to System.Environment.Exit in WPF
            }
            catch (Exception ex)
            {
                // Handle the exception and optionally log it
                Logging.WriteException(ex, MSGBox: true, exitapp: true);
            }
        }


        public async Task SaveDataToDiskAsync()
        {
            try
            {
                // Perform your async operations here
                await OSCSender.SentClearMessage(1500);
                FireExitSave();
            }
            catch (Exception ex)
            {
                // Log any exceptions encountered during the save process
                Logging.WriteException(ex, MSGBox: true, exitapp: true);
            }
        }

        public static void FireExitSave()
        {
            try
            {
                DataController.ManageSettingsXML(true);
                DataController.SaveAppList();
                DataController.SaveMediaSessions();
                DataController.LoadAndSaveMediaLinkStyles(true);
                HotkeyManagement.Instance.SaveHotkeyConfigurations();
                ViewModel.Instance._statsManager.SaveComponentStats();
                ViewModel.Instance.IntelliChatModule.SaveSettings();
                ViewModel.Instance.WhisperModule.OnApplicationClosing();
                ViewModel.Instance.AfkModule.OnApplicationClosing();
                ViewModel.Instance.HeartRateConnector.Settings.SaveSettings();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false, exitapp: true);
            }
        }

        private void SmartClearnup_Click(object sender, RoutedEventArgs e)
        {
            int ItemRemoved = WindowActivityModule.SmartCleanup();
            if (ItemRemoved > 0)
            {
                ViewModel.Instance.DeletedAppslabel = $"Removed {ItemRemoved} apps from history";
            }
            else
            {
                ViewModel.Instance.DeletedAppslabel = $"No apps removed from history";
            }
        }

        private void SortDate_Click(object sender, RoutedEventArgs e)
        {
            {
                ViewModel.Instance.StatusList = new ObservableCollection<StatusItem>(
                    ViewModel.Instance.StatusList.OrderByDescending(x => x.CreationDate));
            }
        }


        private void SortEdited_Click(object sender, RoutedEventArgs e)
        {
            {
                ViewModel.Instance.StatusList = new ObservableCollection<StatusItem>(
                    ViewModel.Instance.StatusList.OrderByDescending(x => x.LastEdited));
            }
        }

        private void SortFav_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.StatusList = new ObservableCollection<StatusItem>(
                ViewModel.Instance.StatusList.OrderByDescending(x => x.UseInCycle).ThenByDescending(x => x.LastUsed));
        }

        private void SortUsed_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.StatusList = new ObservableCollection<StatusItem>(
                ViewModel.Instance.StatusList.OrderByDescending(x => x.LastUsed));
        }


        private void StopChat_Click(object sender, RoutedEventArgs e)
        {
            ChatItem? lastsendchat = ViewModel.Instance.LastMessages.FirstOrDefault(x => x.IsRunning);
            OSCController.ClearChat(lastsendchat);
            int smalldelay = ViewModel.Instance.ChatAddSmallDelay
                ? (int)(ViewModel.Instance.ChatAddSmallDelayTIME * 1000)
                : 0;
            OSCSender.SendOSCMessage(false, smalldelay);
            Timer(null, null);
            foreach (var token in _activeCancellationTokens)
            {
                token.Cancel();
            }
        }

        private void TikTokTTSVoices_combo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                var selectedVoice = comboBox.SelectedItem as Voice;
                if (selectedVoice != null)
                {
                    ViewModel.Instance.SelectedTikTokTTSVoice = selectedVoice;
                    ViewModel.Instance.RecentTikTokTTSVoice = selectedVoice.ApiName;
                }
            }
        }

        private void Timer(object sender, EventArgs e)
        {
            bool ChatItemActive = ViewModel.Instance.LastMessages != null && ViewModel.Instance.LastMessages.Any(x => x.IsRunning);

            if (ViewModel.Instance.ScanPause && ChatItemActive)
            {
                if (pauseTimer == null)
                {
                    ViewModel.Instance.CountDownUI = false;
                    pauseTimer = new System.Timers.Timer();
                    pauseTimer.Interval = 1000;
                    pauseTimer.Elapsed += PauseTimer_Tick;
                    pauseTimer.Start();
                    if (ViewModel.Instance.KeepUpdatingChat)
                    {
                        if (ChatUpdateTimer == null)
                        {
                            if (ViewModel.Instance.LastMessages != null)
                            {
                                ChatItem? lastsendchat = ViewModel.Instance.LastMessages.FirstOrDefault(x => x.IsRunning);
                                if (lastsendchat != null)
                                {
                                    lastsendchat.LiveEditButtonTxt = "Sending...";
                                }
                                ChatUpdateTimer = new System.Timers.Timer();
                                ChatUpdateTimer.Interval = (int)(ViewModel.Instance.ChattingUpdateRate * 1000);
                                ChatUpdateTimer.Elapsed += ChatUpdateTimer_Tick;
                                ChatUpdateTimer.Start();
                            }
                        }
                    }
                }
            }
            else
            {
                if (pauseTimer != null)
                {
                    pauseTimer.Stop();
                    pauseTimer = null;
                }
                if (ChatUpdateTimer != null)
                {
                    ChatUpdateTimer.Stop();
                    ChatUpdateTimer = null;
                }

                ViewModel.Instance.CountDownUI = true;
                scantick();
            }
        }

        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                ChatItem? lastsendchat = ViewModel.Instance.LastMessages.FirstOrDefault(x => x.IsRunning);
                var button = sender as ToggleButton;
                var item = button.Tag as ChatItem;
                if ((bool)button.IsChecked)
                {
                    item.MsgReplace = item.Msg.EndsWith(" ") ? item.Msg : item.Msg + " ";

                    var parent = VisualTreeHelper.GetParent(button);
                    while (!(parent is ContentPresenter))
                    {
                        parent = VisualTreeHelper.GetParent(parent);
                    }
                    var contentPresenter = parent as ContentPresenter;
                    var dataTemplate = contentPresenter.ContentTemplate;

                    // Access TextBox named "EditChatTextBox"
                    var EditChatTextBox = (TextBox)dataTemplate.FindName("EditChatTextBox", contentPresenter);

                    EditChatTextBox.Focus();
                    EditChatTextBox.CaretIndex = EditChatTextBox.Text.Length;
                    item.Opacity_backup = item.Opacity;
                    item.Opacity = "1";
                }
                else
                {
                    if (ViewModel.Instance.RealTimeChatEdit)
                    {
                        if (item != null && lastsendchat != null)
                        {
                            if (lastsendchat.Msg != item.MsgReplace && !lastsendchat.CancelLiveEdit)
                            {
                                lastsendchat.MainMsg = item.MsgReplace;
                                lastsendchat.Msg = item.MsgReplace;
                                lastsendchat.CanLiveEditRun = false;
                            }
                            else if (lastsendchat.CancelLiveEdit)
                            {
                                lastsendchat.Msg = lastsendchat.MainMsg;
                                lastsendchat.CancelLiveEdit = false;
                            }
                        }
                    }
                    else
                    {
                        if (item != null && lastsendchat != null)
                        {
                            if (lastsendchat.Msg != item.MsgReplace && !lastsendchat.CancelLiveEdit)
                            {
                                lastsendchat.MainMsg = item.MsgReplace;
                                lastsendchat.Msg = item.MsgReplace;
                                lastsendchat.CanLiveEditRun = false;
                            }
                            else if (lastsendchat.CancelLiveEdit)
                            {
                                lastsendchat.CancelLiveEdit = false;
                            }
                        }
                    }

                    item.Opacity = item.Opacity_backup;
                    NewChattingTxt.Focus();
                    NewChattingTxt.CaretIndex = NewChattingTxt.Text.Length;
                }
            }
            catch (Exception)
            {
            }
        }

        private void ToggleVoicebtn_Click(object sender, RoutedEventArgs e) { OSCSender.ToggleVoice(true); }


        private void Update_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.Instance.ScanPause)
            {
                OSCController.BuildOSC();
            }

            DataController.ManageSettingsXML(true);
        }

        private void WeatherSyncNow_Click(object sender, RoutedEventArgs e)
        {
            WeatherService.TriggerManualRefresh();
        }

        private void TwitchSyncNow_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.TwitchModule?.TriggerManualRefresh();
        }

        public static void ChangeMenuItem(int changeINT)
        {
            ViewModel.Instance.CurrentMenuItem = changeINT;
            ViewModel.Instance.MenuItem_0_Visibility = "Hidden";
            ViewModel.Instance.MenuItem_1_Visibility = "Hidden";
            ViewModel.Instance.MenuItem_2_Visibility = "Hidden";
            ViewModel.Instance.MenuItem_3_Visibility = "Hidden";

            if (ViewModel.Instance.CurrentMenuItem == 0)
            {
                ViewModel.Instance.MenuItem_0_Visibility = "Visible";
                return;
            }
            else if (ViewModel.Instance.CurrentMenuItem == 1)
            {
                ViewModel.Instance.MenuItem_1_Visibility = "Visible";
                return;
            }
            else if (ViewModel.Instance.CurrentMenuItem == 2)
            {
                ViewModel.Instance.MenuItem_2_Visibility = "Visible";
                return;
            }
            else if (ViewModel.Instance.CurrentMenuItem == 3)
            {
                ViewModel.Instance.MenuItem_3_Visibility = "Visible";
                return;
            }

            ChangeMenuItem(0);
        }


        public void OnSendAgain(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var item = button.Tag as ChatItem;
                if (ViewModel.Instance.MasterSwitch == false)
                {
                    ViewModel.Instance.ChatFeedbackTxt = "Sent to VRChat is off";
                    return;
                }
                if (button != null)
                {
                    foreach (ChatItem Chatitem in ViewModel.Instance.LastMessages)
                    {
                        Chatitem.CanLiveEdit = false;
                        Chatitem.CanLiveEditRun = false;
                        Chatitem.MsgReplace = string.Empty;
                        Chatitem.IsRunning = false;
                    }
                    item.CanLiveEdit = ViewModel.Instance.ChatLiveEdit;
                    item.MainMsg = item.Msg;
                    item.LiveEditButtonTxt = "Sending...";
                    item.IsRunning = true;
                    string savedtxt = ViewModel.Instance.NewChattingTxt;
                    ViewModel.Instance.NewChattingTxt = item.Msg;
                    OSCController.CreateChat(false);
                    int smalldelay = ViewModel.Instance.ChatAddSmallDelay
                        ? (int)(ViewModel.Instance.ChatAddSmallDelayTIME * 1000)
                        : 0;
                    if (ViewModel.Instance.ChatFX && ViewModel.Instance.ChatSendAgainFX)
                    {
                        OSCSender.SendOSCMessage(false, smalldelay);
                    }
                    else
                    {
                        OSCSender.SendOSCMessage(false, smalldelay);
                    }
                    ViewModel.Instance.NewChattingTxt = savedtxt;

                    if (ViewModel.Instance.TTSTikTokEnabled == true && ViewModel.Instance.TTSOnResendChat)
                    {
                        if (DataController.PopulateOutputDevices())
                        {
                            ViewModel.Instance.ChatFeedbackTxt = "Requesting TTS...";
                            MainWindow.TTSGOAsync(item.Msg, true);
                        }
                        else
                        {
                            ViewModel.Instance.ChatFeedbackTxt = "Error setting output device.";
                        }
                    }
                    else
                    {
                        ViewModel.Instance.ChatFeedbackTxt = "Message sent again";
                    }
                    Timer(null, null);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        private DateTime lastOSCMessageTime = DateTime.MinValue;

        public async void scantick(bool firstrun = false)
        {
            if (isProcessing)
            {
                return;
            }

            isProcessing = true;

            try
            {
                if (DateTime.Now >= _nextRun || firstrun)
                {
                    if (backgroundCheck.Interval != TimeSpan.FromMilliseconds(ViewModel.Instance.ScanningInterval * 1000))
                    {
                        backgroundCheck.Stop();
                        backgroundCheck.Interval = TimeSpan.FromMilliseconds(ViewModel.Instance.ScanningInterval * 1000);
                        backgroundCheck.Start();
                        _nextRun = DateTime.Now.Add(backgroundCheck.Interval);
                        return;
                    }

                    await ExecuteScantickLogicAsync();

                    OSCController.BuildOSC();
                    long nowMs = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    long lastMessageMs = lastOSCMessageTime.Ticks / TimeSpan.TicksPerMillisecond;
                    long allowedOverlapMs = 100; // 0.1 second

                    if ((nowMs - lastMessageMs + allowedOverlapMs) >= ViewModel.Instance.ScanningInterval * 1000)
                    {
                        OSCSender.SendOSCMessage(false);
                        lastOSCMessageTime = DateTime.Now;
                    }
                    else
                    {
                        TimeSpan nextAllowedTime = TimeSpan.FromMilliseconds(ViewModel.Instance.ScanningInterval * 1000);
                        DateTime nextAllowedDateTime = lastOSCMessageTime.Add(nextAllowedTime);
                        Logging.WriteInfo($"OSC message rate-limited, NOW: {DateTime.Now} ALLOWED AFTER: {nextAllowedDateTime}");
                    }

                    // Calculate next run time based on whatever interval you have
                    _nextRun = DateTime.Now.Add(backgroundCheck.Interval);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
            finally
            {
                isProcessing = false;
            }
        }


        public async Task ExecuteScantickLogicAsync()
        {
            try
            {
                // List to hold all tasks to be executed concurrently
                var tasks = new List<Task>
        {
            Task.Run(() => ComponentStatsModule.IsVRRunning())
        };

                if (ViewModel.Instance.IntgrScanSpotify_OLD)
                {
                    tasks.Add(UpdateSpotifyStatusAsync());
                }

                if (ViewModel.Instance.IntgrScanWindowActivity)
                {
                    tasks.Add(UpdateFocusedWindowAsync());
                }

                tasks.Add(Task.Run(() => ComponentStatsModule.TickAndUpdate()));

                if (ViewModel.Instance.IntgrScanWindowTime)
                {
                    tasks.Add(UpdateCurrentTimeAsync());
                }

                // Wait for all tasks to complete
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        private async Task UpdateSpotifyStatusAsync()
        {
            try
            {
                string previousTitle = ViewModel.Instance.PlayingSongTitle;
                bool previousPaused = ViewModel.Instance.SpotifyPaused;
                bool previousActive = ViewModel.Instance.SpotifyActive;

                string newTitle = await Task.Run(() => SpotifyModule.CurrentPlayingSong()).ConfigureAwait(false);
                bool newActive = await Task.Run(() => SpotifyModule.SpotifyIsRunning()).ConfigureAwait(false);

                ViewModel.Instance.PlayingSongTitle = newTitle;
                ViewModel.Instance.SpotifyActive = newActive;

                if (!string.Equals(previousTitle, newTitle, StringComparison.Ordinal) ||
                    previousPaused != ViewModel.Instance.SpotifyPaused ||
                    previousActive != newActive)
                {
                    ViewModel.Instance.SpotifyLastChangeUtc = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        private async Task UpdateFocusedWindowAsync()
        {
            try
            {
                ViewModel.Instance.FocusedWindow = await Task.Run(() => WindowActivityModule.GetForegroundProcessName()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        private async Task UpdateCurrentTimeAsync()
        {
            try
            {
                ViewModel.Instance.CurrentTime = await Task.Run(() => ComponentStatsModule.GetTime()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }




        public void SelectTTS()
        {
            foreach (var voice in TikTokTTSVoices_combo.Items)
            {
                if (voice is Voice && (voice as Voice).ApiName == ViewModel.Instance.SelectedTikTokTTSVoice?.ApiName)
                {
                    TikTokTTSVoices_combo.SelectedItem = voice;
                    break;
                }
            }
        }

        public void SelectTTSOutput()
        {
            foreach (var AudioDevice in PlaybackOutputDeviceComboBox.Items)
            {
                if (AudioDevice is AudioDevice &&
                    (AudioDevice as AudioDevice).FriendlyName ==
                    ViewModel.Instance.SelectedPlaybackOutputDevice?.FriendlyName)
                {
                    PlaybackOutputDeviceComboBox.SelectedItem = AudioDevice;
                    break;
                }
            }
        }

        public static async Task TTSGOAsync(string chat, bool resent = false)
        {
            try
            {
                // If user wants to cut off any existing TTS:
                if (ViewModel.Instance.TTSCutOff)
                {
                    foreach (var tokenSource in _activeCancellationTokens)
                    {
                        tokenSource.Cancel();
                    }
                    _activeCancellationTokens.Clear();
                }

                // 1) Fetch the MP3 bytes from the TikTok TTS server
                byte[] audioFromApi = await TTSModule.GetAudioBytesFromTikTokAPI(chat);
                if (audioFromApi == null)
                {
                    ViewModel.Instance.ChatFeedbackTxt = "Error getting TTS from online servers.";
                    return;
                }

                // 2) Kick off the playback
                var cancellationTokenSource = new CancellationTokenSource();
                _activeCancellationTokens.Add(cancellationTokenSource);

                ViewModel.Instance.ChatFeedbackTxt = "TTS is playing...";

                await TTSModule.PlayTikTokAudioAsSpeechAsync(
                    audioFromApi,
                    ViewModel.Instance.SelectedPlaybackOutputDevice.ID,
                    cancellationTokenSource.Token
                );

                // 3) Wrap up
                if (resent)
                    ViewModel.Instance.ChatFeedbackTxt = "Chat was sent again with TTS.";
                else
                    ViewModel.Instance.ChatFeedbackTxt = "Chat was sent with TTS.";

                _activeCancellationTokens.Remove(cancellationTokenSource);
            }
            catch (OperationCanceledException ex)
            {
                ViewModel.Instance.ChatFeedbackTxt = "TTS cancelled";
                Logging.WriteException(ex, MSGBox: false);
            }
            catch (Exception ex)
            {
                ViewModel.Instance.ChatFeedbackTxt = "Error sending a chat with TTS";
                Logging.WriteException(ex, MSGBox: false);
            }
        }


        public static double ShadowOpacity
        {
            get => _shadowOpacity;
            set
            {
                if (_shadowOpacity != value)
                {
                    _shadowOpacity = value;
                    ShadowOpacityChanged?.Invoke(null, EventArgs.Empty);
                }
            }
        }

        private void ConnectWithPulsoid_Click(object sender, RoutedEventArgs e)
        {
            _ = ConnectPulsoidAsync();

        }

        public async Task ConnectPulsoidAsync()
        {
            try
            {
                ViewModel.Instance.HeartRateConnector.DisconnectSession();
                string state = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                const string clientId = "1d0717d2-6c8c-47c6-9097-e289cb02a92d";
                const string redirectUri = "http://localhost:7384/";
                const string scope = "data:heart_rate:read,profile:read,data:statistics:read";
                var authorizationEndpoint = $"https://pulsoid.net/oauth2/authorize?response_type=token&client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope={scope}&state={state}";

                var oauthHandler = PulsoidOAuthHandler.Instance;
                oauthHandler.StartListeners();
                string fragmentString = await oauthHandler.AuthenticateUserAsync(authorizationEndpoint);

                if (string.IsNullOrEmpty(fragmentString)) return;

                var fragment = PulsoidOAuthHandler.ParseQueryString(fragmentString);
                string accessToken;

                if (fragment.TryGetValue("access_token", out accessToken) && !string.IsNullOrEmpty(accessToken))
                {
                    if (await oauthHandler.ValidateTokenAsync(accessToken))
                    {
                        ViewModel.Instance.PulsoidAccessTokenOAuth = accessToken;
                        ViewModel.Instance.PulsoidAuthConnected = true;

                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
            finally
            {
                PulsoidOAuthHandler.Instance.StopListeners();
            }
        }

        private void DisconnectPulsoid_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.PulsoidAccessTokenOAuth = string.Empty;
            ViewModel.Instance.PulsoidAuthConnected = false;
            ViewModel.Instance.HeartRateConnector.DisconnectSession();
        }

        private void ManualPulsoidAuthBtn_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var PulsoidAuthdialog = new ManualPulsoidAuth();
            PulsoidAuthdialog.DataContext = ViewModel.Instance;

            // Set the owner of the dialog to the current window
            PulsoidAuthdialog.Owner = this;

            // Ensure the dialog is centered on the owner window
            PulsoidAuthdialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ViewModel.Instance.MainWindowBlurEffect = 5;
            PulsoidAuthdialog.ShowDialog();
            ViewModel.Instance.MainWindowBlurEffect = 0;
            Activate();
        }

        private void MediaSessionPausePlay_Click(object sender, RoutedEventArgs e)
        {
            MediaSessionInfo? mediaSession = sender is Button button ? button.Tag as MediaSessionInfo : null;
            if (mediaSession != null)
            {
                MediaLinkModule.MediaManager_PlayPauseAsync(mediaSession);
            }
        }

        private void MediaSessionNext_Click(object sender, RoutedEventArgs e)
        {
            MediaSessionInfo? mediaSession = sender is Button button ? button.Tag as MediaSessionInfo : null;
            if (mediaSession != null)
            {
                MediaLinkModule.MediaManager_NextAsync(mediaSession);
            }
        }

        private void MediaSessionPrevious_Click(object sender, RoutedEventArgs e)
        {
            MediaSessionInfo? mediaSession = sender is Button button ? button.Tag as MediaSessionInfo : null;
            if (mediaSession != null)
            {
                MediaLinkModule.MediaManager_PreviousAsync(mediaSession);
            }
        }

        private void ConnectWithOpenAI_Click(object sender, RoutedEventArgs e)
        {
            var OpenAIAuthdialog = new OpenAIAuth();
            OpenAIAuthdialog.DataContext = ViewModel.Instance;

            // Set the owner of the dialog to the current window
            OpenAIAuthdialog.Owner = this;

            // Ensure the dialog is centered on the owner window
            OpenAIAuthdialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ViewModel.Instance.MainWindowBlurEffect = 5;
            OpenAIAuthdialog.ShowDialog();
            ViewModel.Instance.MainWindowBlurEffect = 0;
            Focus();
        }

        private void DisconnectOpenAI_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.OpenAIAccessToken = string.Empty;
            ViewModel.Instance.OpenAIOrganizationID = string.Empty;
            ViewModel.Instance.OpenAIOrganizationIDEncrypted = string.Empty;
            ViewModel.Instance.OpenAIAccessTokenEncrypted = string.Empty;
            ViewModel.Instance.OpenAIConnected = false;
            OpenAIModule.Instance.OpenAIClient = null;

        }

        private void LearnMoreAboutOpenAIbtn_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start(
                "explorer",
                "https://openai.com/policies/terms-of-use");
        }

        private void RestartApplicationAsAdmin_Click(object sender, RoutedEventArgs e)
        {
            FireExitSave();
            try
            {
                AdminRelauncher();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        private void AdminRelauncher()
        {
            if (!IsRunAsAdmin())
            {
                ProcessStartInfo proc = new ProcessStartInfo();
                proc.UseShellExecute = true;
                proc.WorkingDirectory = Environment.CurrentDirectory;
                proc.FileName = Process.GetCurrentProcess().MainModule.FileName;

                proc.Verb = "runas";

                try
                {
                    Process.Start(proc);

                    Thread.Sleep(1000);


                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("This program must be run as an administrator! \n\n" + ex.ToString());
                }
            }
        }

        private bool IsRunAsAdmin()
        {
            WindowsIdentity id = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(id);

            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void UpdateByZipFile_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Instance.UseCustomProfile)
            {
                Logging.WriteException(new Exception("Cannot update by zip while using a custom profile."), MSGBox: true);
                return;
            }

            UpdateApp updateApp = new UpdateApp(true);
            updateApp.SelectCustomZip();
        }

        private async void SpellingCheck_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.IntelliChatModule.PerformSpellingAndGrammarCheckAsync(ViewModel.Instance.NewChattingTxt);
        }

        private async void RebuildChat_Click(object sender, RoutedEventArgs e)
        {

            ViewModel.Instance.IntelliChatModule.PerformBeautifySentenceAsync(ViewModel.Instance.NewChattingTxt);
        }

        private void TranslateChat_click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.IntelliChatModule.PerformLanguageTranslationAsync(ViewModel.Instance.NewChattingTxt);
        }

        private void NotAcceptIntelliChat_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.IntelliChatModule.RejectIntelliChatSuggestion();
        }

        private void AcceptIntelliChat_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.IntelliChatModule.AcceptIntelliChatSuggestion();
        }

        private void CloseIntelliErrorPanel_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.IntelliChatModule.CloseIntelliErrorPanel();
        }

        private void AcceptAndSentIntelliChat_Click(object sender, RoutedEventArgs e)
        {
            AcceptIntelliChat_Click(sender, e);
            ButtonChattingTxt_Click(sender, e);
        }

        private async void ConvoStarterChat_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.IntelliChatModule.GenerateConversationStarterAsync();
        }

        private async void ShortenChat_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.IntelliChatModule.ShortenTextAsync(ViewModel.Instance.NewChattingTxt);
        }

        private void MyUsageBtn_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start("explorer", "https://platform.openai.com/usage");
        }

        private void NextwordPredict_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.IntelliChatModule.GenerateCompletionOrPredictionAsync(ViewModel.Instance.NewChattingTxt, true);
        }

        private void Record_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.WhisperModule.StartRecording();
        }

        private void StopRecord_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.WhisperModule.StopRecording();

        }

        private void Rollback_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Instance.UseCustomProfile)
            {
                Logging.WriteException(new Exception("Cannot rollback while using a custom profile."), MSGBox: true);
                return;
            }

            UpdateApp updateApp = new UpdateApp(true);
            updateApp.StartRollback();
        }

        private async void MediaProgressbar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            MediaSessionInfo? mediaSession = sender is ProgressBar progressBar ? progressBar.Tag as MediaSessionInfo : null;
            ProgressBar progress = sender as ProgressBar;

            if (progress != null)
            {
                // Calculate the clicked position based on the mouse position
                double clickedPosition = e.GetPosition(progress).X / progress.ActualWidth * progress.Maximum;

                if (mediaSession != null)
                {
                   await MediaLinkModule.MediaManager_SeekTo(mediaSession, clickedPosition);
                }

            }
        }

        private void CreateNewSeekbar_btn_Click(object sender, RoutedEventArgs e)
        {
            DataController.AddNewSeekbarStyle();
        }

        private void DeleteSeekbar_btn_Click(object sender, RoutedEventArgs e)
        {
            DataController.DeleteSelectedSeekbarStyleAndSelectDefault();
        }

        private void Paste_Chatting_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.NewChattingTxt = ViewModel.Instance.NewChattingTxt + Clipboard.GetText();
        }

        private void CleanChatting_btn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.NewChattingTxt = string.Empty;
        }

        private void ResetIP_btn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.OSCIP = "127.0.0.1";
        }

        private void ResetOSCPortOut_btn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.OSCPortOut = 9000;
        }

        private void ApplyLinkPulsoid_btn_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://pulsoid.net/pricing?promo_campaign_id=613e3915-a6ba-40f1-a8d4-9ae68c433c6e",
                UseShellExecute = true
            });

        }

        private void PulsoidLearnMoreAboutDiscount_btn_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/BoiHanny/vrcosc-magicchatbox/wiki/Unlock-a-15%25-Discount-on-Pulsoid's-BRO-Plan",
                UseShellExecute = true
            });

        }

        private void MainDiscoundButton_grid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            ViewModel.Instance.ActivateSetting("Settings_HeartRate");
        }

        private void SoundPadPlay_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.SoundpadModule.TogglePause();
        }

        private void SoundPadPause_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.SoundpadModule.TogglePause();
        }

        private void SoundPadPrevious_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.SoundpadModule.PlayPreviousSound();
        }

        private void SoundPadNext_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.SoundpadModule.PlayNextSound();
        }

        private void SoundPadStop_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.SoundpadModule.StopSound();
        }

        private void SoundPadRandon_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.SoundpadModule.PlayRandomSound();
        }

        private void AddEmojiButton_Click(object sender, RoutedEventArgs e)
        {
            bool added = ViewModel.Instance.AddEmoji(EmojiNew.Text);
            if (added)
                EmojiNew.Clear();
        }

        private void EmojiNew_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddEmojiButton_Click(sender, e);
            }
        }

        private void TextBlock_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {

        }
    }


}
