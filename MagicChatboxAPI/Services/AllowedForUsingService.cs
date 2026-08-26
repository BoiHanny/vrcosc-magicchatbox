using MagicChatboxAPI.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MagicChatboxAPI.Services
{
    public interface IAllowedForUsingService
    {
        void StartUserMonitoring(TimeSpan interval);
        void StopUserMonitoring();
        event EventHandler<BanDetectedEventArgs>? BanDetected;
    }

    public class AllowedForUsingService : IAllowedForUsingService
    {
        #region Constants and Fields

        private const string CheckApiEndpoint = "https://api.magicchatbox.com/moderation/checkIfClientIsAllowed";
        private const string AcknowledgeBanEndpoint = "https://api.magicchatbox.com/moderation/acknowledgeBan";
        private static readonly TimeSpan InitialCheckDelay = TimeSpan.FromSeconds(5);

        private readonly HttpClient _httpClient;
        private Timer? _timer;
        private bool _isMonitoring;
        private readonly object _monitorLock = new();

        private List<string>? _allUserIds;
        private readonly Dictionary<string, bool> _userAllowedCache = new();

        #endregion

        #region Events

        public event EventHandler<BanDetectedEventArgs>? BanDetected;

        #endregion

        #region Constructor

        public AllowedForUsingService()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        }

        #endregion

        #region Public Methods

        public void StartUserMonitoring(TimeSpan interval)
        {
            lock (_monitorLock)
            {
                if (_isMonitoring)
                    return;

                _allUserIds = ScanAllVrChatUserIds();

                foreach (var userId in _allUserIds)
                {
                    _userAllowedCache[userId] = true;
                }

                if (_allUserIds.Count == 0)
                {
                    return;
                }

                _timer = new Timer(async _ => await UserMonitorCallback(),
                                   null,
                                   InitialCheckDelay,
                                   interval);
                _isMonitoring = true;
            }
        }

        public void StopUserMonitoring()
        {
            lock (_monitorLock)
            {
                if (!_isMonitoring)
                    return;

                _timer?.Dispose();
                _timer = null;
                _isMonitoring = false;
            }
        }

        #endregion

        #region Private Methods

        private List<string> ScanAllVrChatUserIds()
        {
            var userIds = new List<string>();

            try
            {
                var basePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "AppData", "LocalLow", "VRChat", "VRChat", "OSC");

                if (!Directory.Exists(basePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[AllowedForUsingService] VRChat OSC folder not found: {basePath}");
                    return userIds;
                }

                var userDirectories = Directory.GetDirectories(basePath, "usr_*");
                if (userDirectories == null || userDirectories.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[AllowedForUsingService] No user directories found.");
                    return userIds;
                }

                foreach (var directory in userDirectories)
                {
                    var directoryName = Path.GetFileName(directory);
                    if (!string.IsNullOrEmpty(directoryName) && directoryName.StartsWith("usr_"))
                    {
                        var extractedUserId = directoryName.Substring("usr_".Length).Trim();
                        if (!string.IsNullOrWhiteSpace(extractedUserId))
                        {
                            userIds.Add(extractedUserId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AllowedForUsingService] Error scanning user IDs: {ex.Message}");
            }

            return userIds.Distinct().ToList();
        }

        private async Task UserMonitorCallback()
        {
            if (_allUserIds == null || !_allUserIds.Any())
                return;

            try
            {
                foreach (var userId in _allUserIds)
                {
                    var (isCurrentlyAllowed, reason) = await CheckSingleUserWithReasonAsync(userId);

                    bool wasAllowed;
                    lock (_userAllowedCache)
                    {
                        _userAllowedCache.TryGetValue(userId, out wasAllowed);
                    }

                    if (wasAllowed && !isCurrentlyAllowed)
                    {
                        lock (_userAllowedCache)
                        {
                            _userAllowedCache[userId] = isCurrentlyAllowed;
                        }

                        bool acknowledged = await AcknowledgeBanAsync(userId);
                        if (!acknowledged)
                        {
                            System.Diagnostics.Debug.WriteLine($"[AllowedForUsingService] Failed to acknowledge ban for user {userId}.");
                        }

                        BanDetected?.Invoke(this, new BanDetectedEventArgs(userId, reason));

                        return;
                    }
                    else
                    {
                        lock (_userAllowedCache)
                        {
                            _userAllowedCache[userId] = isCurrentlyAllowed;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AllowedForUsingService] Monitoring error: {ex.Message}");
            }
        }

        private async Task<(bool isAllowed, string reason)> CheckSingleUserWithReasonAsync(string userId)
        {
            var payload = new { userId };
            try
            {
                var response = await _httpClient.PostAsJsonAsync(CheckApiEndpoint, payload);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[AllowedForUsingService] API returned {response.StatusCode}: {errorContent}");
                    return (true, string.Empty);
                }

                var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse>();
                if (apiResponse == null)
                {
                    System.Diagnostics.Debug.WriteLine("[AllowedForUsingService] API response was null.");
                    return (true, string.Empty);
                }

                bool isAllowed = !apiResponse.isBanned;
                string reason = apiResponse.isBanned ? apiResponse.reason : string.Empty;
                return (isAllowed, reason);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AllowedForUsingService] CheckSingleUserWithReasonAsync error for userId={userId}: {ex.Message}");
                return (true, string.Empty);
            }
        }

        private async Task<bool> AcknowledgeBanAsync(string userId)
        {
            var payload = new { userId };
            try
            {
                var response = await _httpClient.PostAsJsonAsync(AcknowledgeBanEndpoint, payload);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[AllowedForUsingService] Acknowledge API returned {response.StatusCode}: {errorContent}");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AllowedForUsingService] Error acknowledging ban for user {userId}: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Internal Model

        private class ApiResponse
        {
            public bool isBanned { get; set; }
            public string reason { get; set; } = string.Empty;
        }

        #endregion
    }
}
