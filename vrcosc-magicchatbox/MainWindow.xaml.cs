using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using vrcosc_magicchatbox.Classes;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.UI.Dialogs;
using vrcosc_magicchatbox.ViewModels;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

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
            Closing += SaveDataToDisk;

            backgroundCheck.Tick += Timer;
            backgroundCheck.Interval = TimeSpan.FromMilliseconds(ViewModel.Instance.ScanningInterval * 1000);
            backgroundCheck.Start();

            // Asynchronous Initialization
            Task initTask = InitializeAsync();

        }

        public async Task InitializeAsync()
        {
            SelectTTS();
            SelectTTSOutput();
            ChangeMenuItem(ViewModel.Instance.CurrentMenuItem);
            Task.Run(() => scantick(true));
            Task.Run(CheckForUpdates);
        }

        private async Task CheckForUpdates()
        {
            if (ViewModel.Instance.CheckUpdateOnStartup)
            {
                var updateCheckTask = DataController.CheckForUpdateAndWait();
                var delayTask = Task.Delay(TimeSpan.FromSeconds(10));

                var completedTask = await Task.WhenAny(updateCheckTask, delayTask);

                if (completedTask == delayTask)
                {
                    ViewModel.Instance.VersionTxt = "Check update timeout";
                    ViewModel.Instance.VersionTxtColor = "#F36734";
                    ViewModel.Instance.VersionTxtUnderLine = false;
                }
            }
            else
            {
                ViewModel.Instance.VersionTxt = "Check for updates";
                ViewModel.Instance.VersionTxtColor = "#2FD9FF";
                ViewModel.Instance.VersionTxtUnderLine = false;
            }
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
                        "u found the dev egggmoooodeee",
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
                    if (DataAndSecurity.DataController.PopulateOutputDevices(true))
                    {
                        ViewModel.Instance.ChatFeedbackTxt = "Requesting TTS...";
                        TTSGOAsync(chat);
                    } else
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
            } catch (Exception)
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
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
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
                            Complete_msg = "💬 " + lastsendchat.Msg;
                        } else
                        {
                            Complete_msg = lastsendchat.Msg;
                        }
                        ViewModel.Instance.OSCtoSent = Complete_msg;
                        OSCSender.SendOSCMessage(false);
                    }
                } else
                {
                    foreach (var item in ViewModel.Instance.LastMessages)
                    {
                        item.CanLiveEdit = false;
                        item.CanLiveEditRun = false;
                        item.MsgReplace = string.Empty;
                        item.IsRunning = false;
                    }
                }
            } catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
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
            int ItemRemoved = WindowActivity.CleanAndKeepAppsWithSettings();
            if (ItemRemoved > 0)
            {
                ViewModel.Instance.DeletedAppslabel = $"Removed {ItemRemoved} apps from history";
            } else
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
                ViewModel.Instance.StatusList.Remove(item);
                ViewModel.SaveStatusList();
            } catch (Exception)
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
                } else
                {
                    if (item.editMsg.Count() < 145 && !string.IsNullOrEmpty(item.editMsg))
                    {
                        item.msg = item.editMsg;
                        item.IsEditing = false;
                        item.editMsg = string.Empty;
                        item.LastEdited = DateTime.Now;
                    }
                }
            } catch (Exception)
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
            } else
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
            } catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }

        private void Favbutton_Click(object sender, RoutedEventArgs e) { ViewModel.SaveStatusList(); }

        private void Github_Click(object sender, RoutedEventArgs e)
        { Process.Start("explorer", "https://github.com/BoiHanny/vrcosc-magicchatbox"); }

        private void GitHubChanges_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel.Instance.tagURL == null)
            {
                Process.Start("explorer", "http://github.com/BoiHanny/vrcosc-magicchatbox/releases");
            } else
            {
                Process.Start("explorer", ViewModel.Instance.tagURL);
            }
        }


        private void LearnMoreAboutHeartbtn_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start(
                "explorer",
                "https://github.com/BoiHanny/vrcosc-magicchatbox/wiki/How-to-Set-Up-MagicChatbox-with-Pulsoid-for-VRChat-%F0%9F%92%9C");
        }


        private void LearnMoreAboutSpotifybtn_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start(
                "explorer",
                "https://github.com/BoiHanny/vrcosc-magicchatbox/wiki/How-to-Use-the-Spotify-Integration-%F0%9F%8E%B5");
        }

        private void LearnMoreAboutTTSbtn_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start(
                "explorer",
                "https://github.com/BoiHanny/vrcosc-magicchatbox/wiki/Play-TTS-Output-of-MagicChatbox-to-Main-Audio-Device-and-Microphone-in-VRChat-Using-VB-Audio-Cable-(Simple-Setup)");
        }

        private void MakeDataDump_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Logging.ViewModelDump();
                Process.Start("explorer.exe", ViewModel.Instance.LogPath);
            } catch (Exception ex)
            {
                Logging.WriteException(ex);
            }
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
            } else
            {
                backgroundCheck.Stop();
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
            } else if (count == 0)
            {
                ViewModel.Instance.ChatBoxColor = "#FF504767";
                ViewModel.Instance.ChatTopBarTxt = string.Empty;
            } else
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
            } else if (count == 0)
            {
                ViewModel.Instance.StatusBoxColor = "#FF504767";
                ViewModel.Instance.StatusTopBarTxt = string.Empty;
            } else
            {
                ViewModel.Instance.StatusBoxColor = "#FF2C2148";
                if (count > 22)
                {
                    ViewModel.Instance.StatusTopBarTxt = $"Buckle up! Keep it tight to 20-25 or integrations may suffer.";
                } else
                {
                    ViewModel.Instance.StatusTopBarTxt = string.Empty;
                }
            }
        }

        private void NewVersion_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel.Instance.CanUpdate)
            {
                ViewModel.Instance.CanUpdate = false;
                ViewModel.Instance.CanUpdateLabel = false;
                Task.Run(() => UpdateApp.PrepareUpdate());
            } else
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
            } catch (Exception ex)
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
            } catch (Exception ex)
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
            } catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
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
            int ItemRemoved = WindowActivity.ResetWindowActivity();
            if (ItemRemoved > 0)
            {
                ViewModel.Instance.DeletedAppslabel = "All apps from history";
            }
        }


        public void SaveDataToDisk(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                Hide();
                FireExitSave();

                System.Environment.Exit(1);
            } catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: true, MSGBox: true, exitapp: true);
            }
        }

        public static void FireExitSave()
        {
            try
            {
                DataController.ManageSettingsXML(true);
                DataController.SaveAppList();
                DataController.SaveMediaSessions();
                ViewModel.Instance._statsManager.SaveComponentStats();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: true, MSGBox: false, exitapp: true);
            }
        }

        private void SmartClearnup_Click(object sender, RoutedEventArgs e)
        {
            int ItemRemoved = WindowActivity.SmartCleanup();
            if (ItemRemoved > 0)
            {
                ViewModel.Instance.DeletedAppslabel = $"Removed {ItemRemoved} apps from history";
            } else
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
                ViewModel.Instance.StatusList.OrderByDescending(x => x.IsFavorite).ThenBy(x => x.LastUsed));
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
            bool ChatItemActive = ViewModel.Instance.LastMessages.Any(x => x.IsRunning);

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
            } else
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
                } else
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
                            else if(lastsendchat.CancelLiveEdit)
                            {
                                lastsendchat.CancelLiveEdit = false;
                            }
                        }
                    }

                    item.Opacity = item.Opacity_backup;
                    NewChattingTxt.Focus();
                    NewChattingTxt.CaretIndex = NewChattingTxt.Text.Length;
                }
            } catch (Exception)
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
            } else if (ViewModel.Instance.CurrentMenuItem == 1)
            {
                ViewModel.Instance.MenuItem_1_Visibility = "Visible";
                return;
            } else if (ViewModel.Instance.CurrentMenuItem == 2)
            {
                ViewModel.Instance.MenuItem_2_Visibility = "Visible";
                return;
            } else if (ViewModel.Instance.CurrentMenuItem == 3)
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
                    } else
                    {
                        OSCSender.SendOSCMessage(false, smalldelay);
                    }
                    ViewModel.Instance.NewChattingTxt = savedtxt;

                    if (ViewModel.Instance.TTSTikTokEnabled == true && ViewModel.Instance.TTSOnResendChat)
                    {
                        if (DataAndSecurity.DataController.PopulateOutputDevices(true))
                        {
                            ViewModel.Instance.ChatFeedbackTxt = "Requesting TTS...";
                            MainWindow.TTSGOAsync(item.Msg, true);
                        } else
                        {
                            ViewModel.Instance.ChatFeedbackTxt = "Error setting output device.";
                        }
                    } else
                    {
                        ViewModel.Instance.ChatFeedbackTxt = "Message sent again";
                    }
                    Timer(null, null);
                }
            } catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: true, MSGBox: false);
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

                    scantickLogicAsync();

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
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
            finally
            {
                isProcessing = false;
            }
        }


        public async Task scantickLogicAsync()
        {
            try
            {
                var tasks = new List<Task>
        {
            Task.Run(() => ViewModel.Instance.IsVRRunning = SystemStats.IsVRRunning())
        };

                if (ViewModel.Instance.IntgrScanSpotify_OLD)
                {
                    tasks.Add(Task.Run(() => ViewModel.Instance.PlayingSongTitle = SpotifyActivity.CurrentPlayingSong()));
                    tasks.Add(Task.Run(() => ViewModel.Instance.SpotifyActive = SpotifyActivity.SpotifyIsRunning()));
                }

                if (ViewModel.Instance.IntgrScanWindowActivity)
                {
                    tasks.Add(Task.Run(() => ViewModel.Instance.FocusedWindow = WindowActivity.GetForegroundProcessName()));
                }

                tasks.Add(Task.Run(() => SystemStats.TickAndUpdate()));

                if (ViewModel.Instance.IntgrScanWindowTime)
                {
                    tasks.Add(Task.Run(() => ViewModel.Instance.CurrentTime = SystemStats.GetTime()));
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }




        public void SelectTTS()
        {
            foreach(var voice in TikTokTTSVoices_combo.Items)
            {
                if(voice is Voice && (voice as Voice).ApiName == ViewModel.Instance.SelectedTikTokTTSVoice?.ApiName)
                {
                    TikTokTTSVoices_combo.SelectedItem = voice;
                    break;
                }
            }
        }

        public void SelectTTSOutput()
        {
            foreach(var AudioDevice in PlaybackOutputDeviceComboBox.Items)
            {
                if(AudioDevice is AudioDevice &&
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
                if(ViewModel.Instance.TTSCutOff)
                {
                    foreach(var tokenSource in _activeCancellationTokens)
                    {
                        tokenSource.Cancel();
                    }
                    _activeCancellationTokens.Clear();
                }


                byte[] audioFromApi = await TTSController.GetAudioBytesFromTikTokAPI(chat);
                if(audioFromApi != null)
                {
                    var cancellationTokenSource = new CancellationTokenSource();
                    _activeCancellationTokens.Add(cancellationTokenSource);
                    ViewModel.Instance.ChatFeedbackTxt = "TTS is playing...";
                    await TTSController.PlayTikTokAudioAsSpeech(
                        cancellationTokenSource.Token,
                        audioFromApi,
                        ViewModel.Instance.SelectedPlaybackOutputDevice.DeviceNumber);
                    if(resent)
                    {
                        ViewModel.Instance.ChatFeedbackTxt = "Chat was sent again with TTS.";
                    } else
                    {
                        ViewModel.Instance.ChatFeedbackTxt = "Chat was sent with TTS.";
                    }


                    _activeCancellationTokens.Remove(cancellationTokenSource);
                } else
                {
                    ViewModel.Instance.ChatFeedbackTxt = "Error getting TTS from online servers.";
                }
            } catch(OperationCanceledException ex)
            {
                ViewModel.Instance.ChatFeedbackTxt = "TTS cancelled";
                Logging.WriteException(ex, makeVMDump: true, MSGBox: false);
            } catch(Exception ex)
            {
                ViewModel.Instance.ChatFeedbackTxt = "Error sending a chat with TTS";
            }
        }


        public static double ShadowOpacity
        {
            get => _shadowOpacity;
            set
            {
                if(_shadowOpacity != value)
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
                string state = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                const string clientId = "1d0717d2-6c8c-47c6-9097-e289cb02a92d";
                const string redirectUri = "http://localhost:7384/";
                const string scope = "data:heart_rate:read";
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
                        ViewModel.Instance.PulsoidAuthConnected = true;
                        ViewModel.Instance.PulsoidAccessTokenOAuth = accessToken;
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
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
        }

        private void ManualPulsoidAuthBtn_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var dialog = new ManualPulsoidAuth();
            dialog.DataContext = ViewModel.Instance;

            // Set the owner of the dialog to the current window
            dialog.Owner = this;

            // Ensure the dialog is centered on the owner window
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ViewModel.Instance.MainWindowBlurEffect = 5;
            dialog.ShowDialog();
            ViewModel.Instance.MainWindowBlurEffect = 0;
            Focus();
        }


    }
}