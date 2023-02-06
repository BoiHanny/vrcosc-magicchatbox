using System.Windows;
using vrcosc_magicchatbox.Classes;
using vrcosc_magicchatbox.ViewModels;
using System.Windows.Threading;
using System;
using System.Windows.Input;
using System.Diagnostics;
using System.Windows.Controls;
using System.Linq;
using System.Collections.ObjectModel;
using Version = vrcosc_magicchatbox.ViewModels.Version;
using System.IO;

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
        public float samplingTime = 1;

        DispatcherTimer backgroundCheck = new DispatcherTimer();

        public MainWindow()
        {
            Closing += SaveDataToDisk;
            _VM = new ViewModel();
            _SPOT = new SpotifyActivity(_VM);
            _DATAC = new DataController(_VM);
            _OSC = new OscController(_VM);
            _STATS = new SystemStats(_VM);
            _ACTIV = new WindowActivity(_VM);

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
            ChangeMenuItem(_VM.CurrentMenuItem);
            scantick();
            _DATAC.CheckForUpdate();
        }

        private void SaveDataToDisk(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.Hide();
            _DATAC.SaveSettingsToXML();
            System.Environment.Exit(1);
        }

        private void Timer(object sender, EventArgs e)
        {
            scantick();
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
            _OSC.BuildOSC();
            _OSC.SentOSCMessage();
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
            _OSC.BuildOSC();
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
                if(count > 22)
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
    }
}