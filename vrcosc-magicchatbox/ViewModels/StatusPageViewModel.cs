using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels
{
    /// <summary>
    /// Page-specific ViewModel for the Status page. Owns status add/delete/edit,
    /// activation, sorting, persistence, and character counting logic.
    /// Used as DataContext for StatusPage.xaml.
    /// </summary>
    public partial class StatusPageViewModel : ObservableObject
    {
        private readonly ChatStatusDisplayState _chatStatus;
        private readonly IAppState _appState;
        private readonly IStatusListService _statusListService;
        private readonly IMenuNavigationService _menuNav;

        public ChatStatusDisplayState ChatStatus { get; }
        public AppSettings AppSettings { get; }

        /// <summary>
        /// Initializes the status page ViewModel with chat state, app state, status module,
        /// navigation, and settings services.
        /// </summary>
        public StatusPageViewModel(
            ChatStatusDisplayState chatStatus,
            IAppState appState,
            IStatusListService statusListService,
            IMenuNavigationService menuNav,
            ISettingsProvider<AppSettings> appSettingsProvider)
        {
            _chatStatus = chatStatus;
            _appState = appState;
            _statusListService = statusListService;
            _menuNav = menuNav;
            ChatStatus = chatStatus;
            AppSettings = appSettingsProvider.Value;
        }

        [RelayCommand]
        private void ActivateSetting(string settingName)
            => _menuNav.ActivateSetting(settingName);

        [RelayCommand]
        private void ClearStatusInput()
            => _chatStatus.NewStatusItemTxt = string.Empty;

        [RelayCommand]
        private void SortStatusByDate()
            => _chatStatus.StatusList = new ObservableCollection<StatusItem>(
                _chatStatus.StatusList.OrderByDescending(x => x.CreationDate));

        [RelayCommand]
        private void SortStatusByEdited()
            => _chatStatus.StatusList = new ObservableCollection<StatusItem>(
                _chatStatus.StatusList.OrderByDescending(x => x.LastEdited));

        [RelayCommand]
        private void SortStatusByFav()
            => _chatStatus.StatusList = new ObservableCollection<StatusItem>(
                _chatStatus.StatusList.OrderByDescending(x => x.UseInCycle)
                    .ThenByDescending(x => x.LastUsed));

        [RelayCommand]
        private void SortStatusByUsed()
            => _chatStatus.StatusList = new ObservableCollection<StatusItem>(
                _chatStatus.StatusList.OrderByDescending(x => x.LastUsed));

        public void SaveStatusList() => _statusListService.SaveStatusList();

        [RelayCommand]
        public void AddStatus()
        {
            string text = _chatStatus.NewStatusItemTxt;
            if (text.Length <= 0 || text.Length >= Core.Constants.MaxChatMessageLength) return;

            Random random = new Random();
            bool isActive = _chatStatus.StatusList.Count == 0;

            _chatStatus.StatusList.Add(new StatusItem
            {
                CreationDate = DateTime.Now,
                IsActive = isActive,
                IsFavorite = false,
                msg = text,
                MSGID = random.Next(Core.Constants.StatusRandomIdMin, Core.Constants.StatusRandomIdMax)
            });

            _chatStatus.StatusList = new ObservableCollection<StatusItem>(
                _chatStatus.StatusList.OrderByDescending(x => x.CreationDate));

            // Easter eggs
            string lower = text.ToLower();
            if (lower == "sr4 series" || lower == "boihanny")
            {
                _appState.Egg_Dev = true;
                MessageBox.Show("u found the dev egggmoooodeee go to options", "Egg",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            if (lower == "bussyboys")
            {
                _appState.BussyBoysMode = true;
                MessageBox.Show("Bussy Boysss letsss goooo, go to afk options", "Egg",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            _chatStatus.NewStatusItemTxt = string.Empty;
            SaveStatusList();
        }

        [RelayCommand]
        public void DeleteStatus(StatusItem? item)
        {
            if (item == null) return;
            try
            {
                string lower = item.msg.ToLower();
                if (lower == "sr4 series" || lower == "boihanny")
                {
                    _appState.Egg_Dev = false;
                    MessageBox.Show("damn u left the dev egggmoooodeee", "Egg",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                if (lower == "bussyboys")
                {
                    _appState.BussyBoysMode = false;
                    MessageBox.Show("damn u left the bussyboys mode", "Egg",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                _chatStatus.StatusList.Remove(item);
                SaveStatusList();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        [RelayCommand]
        public void CancelEdit(StatusItem? item)
        {
            if (item == null) return;
            item.editMsg = string.Empty;
            item.IsEditing = false;
        }

        [RelayCommand]
        public void ToggleFavorite(StatusItem? item)
        {
            if (item == null) return;
            item.UseInCycle = !item.UseInCycle;
            SaveStatusList();
        }

        /// <summary>
        /// Updates the status box character count and color state.
        /// Called from code-behind TextChanged handler.
        /// </summary>
        public void UpdateStatusBoxCount(int count)
        {
            _chatStatus.StatusBoxCount = $"{count}/140";
            if (count > 140)
            {
                int overmax = count - 140;
                _chatStatus.StatusBoxColor = "#FFFF9393";
                _chatStatus.StatusTopBarTxt = $"You're soaring past the 140 char limit by {overmax}. Reign in that message!";
            }
            else if (count == 0)
            {
                _chatStatus.StatusBoxColor = "#FF504767";
                _chatStatus.StatusTopBarTxt = string.Empty;
            }
            else
            {
                _chatStatus.StatusBoxColor = "#FF2C2148";
                _chatStatus.StatusTopBarTxt = count > 22
                    ? "Buckle up! Keep it tight to 20-25 or integrations may suffer."
                    : string.Empty;
            }
        }

        [RelayCommand]
        private void ActivateStatus(StatusItem? item)
        {
            try
            {
                foreach (var i in _chatStatus.StatusList)
                {
                    if (i == item)
                    {
                        i.IsActive = true;
                        i.LastUsed = DateTime.Now;
                    }
                    else
                    {
                        i.IsActive = false;
                    }
                }
                _statusListService.SaveStatusList();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        /// <summary>
        /// Prepares a status item for editing (copies msg to editMsg).
        /// Code-behind handles the focus/caret UI concern.
        /// </summary>
        public void BeginEdit(StatusItem item)
        {
            item.editMsg = item.msg;
        }

        /// <summary>
        /// Confirms the edit, validates, and saves.
        /// </summary>
        public void ConfirmEdit(StatusItem item)
        {
            if (item.editMsg.Length < 145 && !string.IsNullOrEmpty(item.editMsg))
            {
                item.msg = item.editMsg;
                item.IsEditing = false;
                item.editMsg = string.Empty;
                item.LastEdited = DateTime.Now;
                SaveStatusList();
            }
        }
    }
}
