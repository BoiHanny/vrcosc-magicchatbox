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
using System.Windows.Input;
using System.Windows.Threading;
using vrcosc_magicchatbox.Classes;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox
{
    public partial class MainWindow : Window
    {
        private ViewModel _VM;
        private SpotifyActivity _SPOT;
        private DataController _DATAC;
        private OscController _OSC;
        private SystemStats _STATS;
        private WindowActivity _ACTIV;
        private TTSController _TTS;
        public float samplingTime = 1;

        DispatcherTimer backgroundCheck = new DispatcherTimer();
        private System.Timers.Timer pauseTimer;
        private System.Timers.Timer typingTimer;
        private List<CancellationTokenSource> _activeCancellationTokens = new List<CancellationTokenSource>();

        public MainWindow()
        {
            Closing += SaveDataToDisk;
            _VM = new ViewModel();
            _SPOT = new SpotifyActivity(_VM);
            _DATAC = new DataController(_VM);
            _OSC = new OscController(_VM);
            _STATS = new SystemStats(_VM);
            _ACTIV = new WindowActivity(_VM);
            _TTS = new TTSController(_VM);

            this.DataContext = _VM;
            InitializeComponent();

            backgroundCheck.Tick += Timer; backgroundCheck.Interval = new TimeSpan(0, 0, _VM.ScanInterval); backgroundCheck.Start();
            _VM.IntgrScanWindowActivity = false;
            _VM.IntgrScanSpotify = true;
            _VM.IntgrScanWindowTime = true;
            _VM.IntgrStatus = true;
            _VM.MasterSwitch = true;
            _DATAC.LoadSettingsFromXML();
            _DATAC.LoadStatusList();
            _VM.TikTokTTSVoices = _DATAC.ReadTkTkTTSVoices();
            SelectTTS();
            _DATAC.PopulateOutputDevices();
            SelectTTSOutput();
            ChangeMenuItem(_VM.CurrentMenuItem);
            scantick();
            _DATAC.CheckForUpdate();
        }

        public void SelectTTS()
        {
            foreach (var voice in TikTokTTSVoices_combo.Items)
            {
                if (voice is Voice && (voice as Voice).ApiName == _VM.SelectedTikTokTTSVoice?.ApiName)
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
                if (AudioDevice is AudioDevice && (AudioDevice as AudioDevice).FriendlyName == _VM.SelectedPlaybackOutputDevice?.FriendlyName)
                {
                    PlaybackOutputDeviceComboBox.SelectedItem = AudioDevice;
                    break;
                }
            }
        }



        private void SaveDataToDisk(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.Hide();
            _DATAC.SaveSettingsToXML();
            System.Environment.Exit(1);
        }

        private void Timer(object sender, EventArgs e)
        {
            if (_VM.ScanPause)
            {
                if (pauseTimer == null)
                {
                    _VM.CountDownUI = false;
                    pauseTimer = new System.Timers.Timer();
                    pauseTimer.Interval = 1000;
                    pauseTimer.Elapsed += PauseTimer_Tick;
                    pauseTimer.Start();
                }
            }
            else
            {
                if (pauseTimer != null)
                {
                    pauseTimer.Stop();
                    pauseTimer = null;
                }

                _VM.CountDownUI = true;
                scantick();
            }

        }

        private void PauseTimer_Tick(object sender, System.Timers.ElapsedEventArgs e)
        {
            _VM.ScanPauseCountDown--;
            Application.Current.Dispatcher.Invoke(() =>
            {
                _VM.ScanPauseCountDown = _VM.ScanPauseCountDown;
            });

            if (_VM.ScanPauseCountDown <= 0 || !_VM.ScanPause)
            {
                _VM.ScanPause = false;
                pauseTimer.Stop();
                pauseTimer = null;
                if (_VM.ScanPauseCountDown != 0)
                {
                    _VM.ScanPauseCountDown = 0;
                }
                _OSC.ClearChat();
                _OSC.SentOSCMessage(false);
                Timer(null, null);
            }
        }

        public void scantick()
        {
            if (_VM.IntgrScanSpotify == true)
            { _VM.PlayingSongTitle = _SPOT.CurrentPlayingSong(); _VM.SpotifyActive = _SPOT.SpotifyIsRunning(); }
            if (_VM.IntgrScanWindowActivity == true)
            { _VM.FocusedWindow = _ACTIV.GetForegroundProcessName(); }
            _VM.IsVRRunning = _ACTIV.IsVRRunning();
            if (_VM.IntgrScanWindowTime == true)
            {
                _VM.CurrentTime = _STATS.GetTime();
            }
            _VM.ChatFeedbackTxt = "";
            _OSC.BuildOSC();
            _OSC.SentOSCMessage(false);
        }

        public void ChangeMenuItem(int changeINT)
        {
            _VM.CurrentMenuItem = changeINT;
            _VM.MenuItem_0_Visibility = "Hidden";
            _VM.MenuItem_1_Visibility = "Hidden";
            _VM.MenuItem_2_Visibility = "Hidden";
            _VM.MenuItem_3_Visibility = "Hidden";
            if (_VM.CurrentMenuItem == 0)
            {
                _VM.MenuItem_0_Visibility = "Visible";
                return;
            }
            else if (_VM.CurrentMenuItem == 1)
            {
                _VM.MenuItem_1_Visibility = "Visible";
                return;
            }
            else if (_VM.CurrentMenuItem == 2)
            {
                _VM.MenuItem_2_Visibility = "Visible";
                return;
            }
            else if (_VM.CurrentMenuItem == 3)
            {
                _VM.MenuItem_3_Visibility = "Visible";
                return;
            }
            ChangeMenuItem(0);
        }


        private void Update_Click(object sender, RoutedEventArgs e)
        {
            if (_VM.ScanPause != true)
            {
                _OSC.BuildOSC();
            }
        }

        private void Drag_area_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }


        private void Button_close_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Hidden;
            this.Close();
        }

        private void Button_minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void NewVersion_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start("explorer", "http://github.com/BoiHanny/vrcosc-magicchatbox/releases");
        }

        private void MasterSwitch_Click(object sender, RoutedEventArgs e)
        {
            if (_VM.MasterSwitch == true)
            {
                backgroundCheck.Start();
            }
            else
            {
                backgroundCheck.Stop();
            }

        }

        private void MenuButton_0_Click(object sender, RoutedEventArgs e)
        {
            ChangeMenuItem(0);
        }

        private void MenuButton_1_Click(object sender, RoutedEventArgs e)
        {
            ChangeMenuItem(1);
        }

        private void MenuButton_2_Click(object sender, RoutedEventArgs e)
        {
            ChangeMenuItem(2);
        }

        private void MenuButton_3_Click(object sender, RoutedEventArgs e)
        {
            ChangeMenuItem(3);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var item = button.Tag as StatusItem;
                _VM.StatusList.Remove(item);
                _VM.SaveStatusList();
            }
            catch (Exception)
            {

            }
        }

        private void SortUsed_Click(object sender, RoutedEventArgs e)
        {
            _VM.StatusList = new ObservableCollection<StatusItem>(_VM.StatusList.OrderByDescending(x => x.LastUsed));
        }

        private void SortFav_Click(object sender, RoutedEventArgs e)
        {
            _VM.StatusList = new ObservableCollection<StatusItem>(_VM.StatusList.OrderByDescending(x => x.IsFavorite).ThenBy(x => x.LastUsed));
        }

        private void SortDate_Click(object sender, RoutedEventArgs e)
        {
            {
                _VM.StatusList = new ObservableCollection<StatusItem>(_VM.StatusList.OrderByDescending(x => x.CreationDate));
            }
        }

        private void FavBox_KeyDown(object sender, KeyEventArgs e)
        {

            if (e.Key == Key.Enter)
            {
                AddFav_Click(sender, e);
            }
            if (e.Key == Key.Escape)
            {
                _VM.NewStatusItemTxt = "";
            }


        }

        private void NewFavText_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            int count = textBox.Text.Count();
            _VM.StatusBoxCount = $"{count.ToString()}/140";
            if (count > 140)
            {
                int overmax = count - 140;
                _VM.StatusBoxColor = "#FFFF9393";
                _VM.StatusTopBarTxt = $"You're soaring past the 140 char limit by {overmax}. Reign in that message!";
            }
            else if (count == 0)
            {
                _VM.StatusBoxColor = "#FF504767";
                _VM.StatusTopBarTxt = $"";
            }
            else
            {
                _VM.StatusBoxColor = "#FF2C2148";
                if (count > 22)
                {
                    _VM.StatusTopBarTxt = $"Buckle up! Keep it tight to 20-25 or integrations may suffer.";
                }
                else
                {
                    _VM.StatusTopBarTxt = $"";
                }
            }
        }

        private void AddFav_Click(object sender, RoutedEventArgs e)
        {
            Random random = new Random();
            int randomId = random.Next(10, 99999999);
            bool IsActive = false;
            if (_VM.StatusList.Count() == 0)
            {
                IsActive = true;
            }

            if (_VM.NewStatusItemTxt.Count() > 0 && _VM.NewStatusItemTxt.Count() < 141)
            {
                _VM.StatusList.Add(new StatusItem { CreationDate = DateTime.Now, IsActive = IsActive, IsFavorite = false, msg = _VM.NewStatusItemTxt, MSGLenght = _VM.NewStatusItemTxt.Count(), MSGID = randomId });
                _VM.StatusList = new ObservableCollection<StatusItem>(_VM.StatusList.OrderByDescending(x => x.CreationDate));
                _VM.NewStatusItemTxt = "";
                _VM.SaveStatusList();
            }
        }

        private void Favbutton_Click(object sender, RoutedEventArgs e)
        {
            _VM.SaveStatusList();
        }

        private void ResetFavorites_Click(object sender, RoutedEventArgs e)
        {
            string xml = Path.Combine(_VM.DataPath, "StatusList.xml");
            if (File.Exists(xml))
            {
                File.Delete(xml);
            }
            _VM.StatusList.Clear();
            _DATAC.LoadStatusList();
            _VM.SaveStatusList();
            ChangeMenuItem(1);
        }

        private void NewChattingTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            int count = textBox.Text.Count();
            _VM.ChatBoxCount = $"{count.ToString()}/140";
            if (count > 140)
            {
                int overmax = count - 140;
                _VM.ChatBoxColor = "#FFFF9393";
                _VM.ChatTopBarTxt = $"You're soaring past the 140 char limit by {overmax}. Reign in that message!";
            }
            else if (count == 0)
            {
                _VM.ChatBoxColor = "#FF504767";
                _VM.ChatTopBarTxt = $"";
            }
            else
            {
                _VM.ChatBoxColor = "#FF2C2148";
                _VM.ChatTopBarTxt = $"";

            }

            _OSC.TypingIndicator(true);


            if (typingTimer != null)
            {
                typingTimer.Stop();
                typingTimer.Start();
            }
            else
            {
                typingTimer = new System.Timers.Timer(2000);
                typingTimer.Elapsed += (s, args) => _OSC.TypingIndicator(false);
                typingTimer.AutoReset = false;
                typingTimer.Enabled = true;
            }

        }

        private void NewChattingTxt_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ButtonChattingTxt_Click(sender, e);
            }
            if (e.Key == Key.Escape)
            {
                _VM.NewChattingTxt = "";
            }
        }

        private void ButtonChattingTxt_Click(object sender, RoutedEventArgs e)
        {
            string chat = _VM.NewChattingTxt;
            if (chat.Length > 0 && chat.Length <= 141)
            {
                _OSC.CreateChat(true);
                _OSC.SentOSCMessage(_VM.ChatFX);
                if (_VM.TTSTikTokEnabled == true)
                {
                    if (_DATAC.PopulateOutputDevices(true))
                    {
                        _VM.ChatFeedbackTxt = "Requesting TTS...";
                        TTSGOAsync(chat);
                    }
                    else
                    {
                        _VM.ChatFeedbackTxt = "Error setting output device.";
                    }

                }

                Timer(null, null);
                RecentScroll.ScrollToEnd();
            }   
        }

        public async Task TTSGOAsync(string chat)
        {
            try
            {
                if (_VM.TTSCutOff)
                {
                    foreach (var tokenSource in _activeCancellationTokens)
                    {
                        tokenSource.Cancel();
                    }
                    _activeCancellationTokens.Clear();
                }


                byte[] audioFromApi = await _TTS.GetAudioBytesFromTikTokAPI(chat);
                
                var cancellationTokenSource = new CancellationTokenSource();
                _activeCancellationTokens.Add(cancellationTokenSource);
                    _VM.ChatFeedbackTxt = "Chat sent with TTS";

                await _TTS.PlayTikTokAudioAsSpeech(cancellationTokenSource.Token, audioFromApi, _VM.SelectedPlaybackOutputDevice.DeviceNumber);

                    _VM.ChatFeedbackTxt = "Chat was sent with TTS";

                _activeCancellationTokens.Remove(cancellationTokenSource);
            }
            catch (OperationCanceledException)
            {
                _VM.ChatFeedbackTxt = "TTS cancelled";
            }
            catch (Exception ex)
            {
                _VM.ChatFeedbackTxt = "Error sending a chat with TTS";
            }
        }




        private void StopChat_Click(object sender, RoutedEventArgs e)
        {
            _OSC.ClearChat();
            _OSC.SentOSCMessage(false);
            Timer(null, null);
            foreach (var token in _activeCancellationTokens)
            {
                token.Cancel();
            }
        }

        private void ClearChat_Click(object sender, RoutedEventArgs e)
        {
            _VM.LastMessages.Clear();
            StopChat_Click(null, null);
        }

        private void TikTokTTSVoices_combo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                var selectedVoice = comboBox.SelectedItem as Voice;
                if (selectedVoice != null)
                {
                    _VM.SelectedTikTokTTSVoice = selectedVoice;
                    _VM.RecentTikTokTTSVoice = selectedVoice.ApiName;
                }
            }

        }

        private void PlaybackOutputDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _VM.RecentPlayBackOutput = _VM.SelectedPlaybackOutputDevice.FriendlyName;
        }
    }
}