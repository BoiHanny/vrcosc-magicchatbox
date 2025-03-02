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
        event EventHandler<BanDetectedEventArgs> BanDetected;
    }

    public class AllowedForUsingService : IAllowedForUsingService
    {
        #region Constants and Fields

        // External API endpoint for checking a user's ban status.
        private const string CheckApiEndpoint = "https://api.magicchatbox.com/moderation/checkIfClientIsAllowed";
        // External API endpoint for acknowledging a ban.
        private const string AcknowledgeBanEndpoint = "https://api.magicchatbox.com/moderation/acknowledgeBan";

        private readonly HttpClient _httpClient;
        private Timer _timer;
        private bool _isMonitoring;
        private readonly object _monitorLock = new();

        private List<string> _allUserIds;
        // Cache tracking each user's current allowed state.
        private readonly Dictionary<string, bool> _userAllowedCache = new();

        #endregion

        #region Events

        public event EventHandler<BanDetectedEventArgs> BanDetected;

        #endregion

        #region Constructor

        public AllowedForUsingService()
        {
            _httpClient = new HttpClient();
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
                                   TimeSpan.Zero,
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

        /// <summary>
        /// Scans the VRChat OSC folder once, collecting all user IDs.
        /// </summary>
        private List<string> ScanAllVrChatUserIds()
        {
            var userIds = new List<string>();

            try
            {
                // Base path to VRChat's OSC user folders.
                var basePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "AppData", "LocalLow", "VRChat", "VRChat", "OSC");

                if (!Directory.Exists(basePath))
                {
                    Console.WriteLine($"[AllowedForUsingService] VRChat OSC folder not found: {basePath}");
                    return userIds;
                }

                var userDirectories = Directory.GetDirectories(basePath, "usr_*");
                if (userDirectories == null || userDirectories.Length == 0)
                {
                    Console.WriteLine("[AllowedForUsingService] No user directories found.");
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
                Console.WriteLine($"[AllowedForUsingService] Error scanning user IDs: {ex.Message}");
            }

            return userIds.Distinct().ToList();
        }

        /// <summary>
        /// Timer callback: checks the ban status of all known user IDs via API.
        /// When a user transitions from allowed to banned, it calls the acknowledge-ban endpoint,
        /// then fires the BanDetected event with the user ID and ban reason.
        /// </summary>
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

                        // Call the acknowledge-ban endpoint.
                        bool acknowledged = await AcknowledgeBanAsync(userId);
                        if (!acknowledged)
                        {
                            Console.WriteLine($"[AllowedForUsingService] Failed to acknowledge ban for user {userId}.");
                        }

                        // Fire the BanDetected event with the ban reason.
                        BanDetected?.Invoke(this, new BanDetectedEventArgs(userId, reason));

                        return;
                    }
                    else
                    {
                        // Update the cache in any case.
                        lock (_userAllowedCache)
                        {
                            _userAllowedCache[userId] = isCurrentlyAllowed;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AllowedForUsingService] Monitoring error: {ex.Message}");
            }
        }

        /// <summary>
        /// Calls the external API for a single user to determine if they are banned,
        /// and retrieves the ban reason if applicable.
        /// Returns a tuple where the first value indicates whether the user is allowed
        /// (true means allowed, false means banned), and the second value contains the ban reason.
        /// </summary>
        private async Task<(bool isAllowed, string reason)> CheckSingleUserWithReasonAsync(string userId)
        {
            var payload = new { userId };
            try
            {
                var response = await _httpClient.PostAsJsonAsync(CheckApiEndpoint, payload);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[AllowedForUsingService] API returned {response.StatusCode}: {errorContent}");
                    // In case of error, treat user as allowed for safety.
                    return (true, string.Empty);
                }

                var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse>();
                if (apiResponse == null)
                {
                    Console.WriteLine("[AllowedForUsingService] API response was null.");
                    return (true, string.Empty);
                }

                // If "isBanned" is true in the API response, the user is banned (i.e. not allowed).
                // We assume that the API returns a 'reason' when a user is banned.
                bool isAllowed = !apiResponse.isBanned;
                string reason = apiResponse.isBanned ? apiResponse.reason : string.Empty;
                return (isAllowed, reason);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AllowedForUsingService] CheckSingleUserWithReasonAsync error for userId={userId}: {ex.Message}");
                return (true, string.Empty);
            }
        }

        /// <summary>
        /// Calls the external acknowledge-ban API endpoint to mark the ban as acknowledged.
        /// </summary>
        private async Task<bool> AcknowledgeBanAsync(string userId)
        {
            var payload = new { userId };
            try
            {
                var response = await _httpClient.PostAsJsonAsync(AcknowledgeBanEndpoint, payload);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[AllowedForUsingService] Acknowledge API returned {response.StatusCode}: {errorContent}");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AllowedForUsingService] Error acknowledging ban for user {userId}: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Internal Model

        /// <summary>
        /// Internal class that maps the JSON structure from the external API.
        /// Adjust properties to match the actual API response.
        /// </summary>
        private class ApiResponse
        {
            public bool isBanned { get; set; }
            // The reason provided by the API when a user is banned.
            public string reason { get; set; }
        }

        #endregion
    }
}
