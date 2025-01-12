using MagicChatboxAPI.Enums;
using MagicChatboxAPI.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
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

        // External API endpoint for checking a user's ban status
        private const string ApiEndpoint = "https://api.magicchatbox.com/moderation/checkIfClientIsAllowed";

        private readonly HttpClient _httpClient;

        private Timer _timer;
        private bool _isMonitoring;
        private readonly object _monitorLock = new();

        private List<string> _allUserIds;

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
        /// <returns>List of all user IDs found (excluding "usr_" prefix).</returns>
        private List<string> ScanAllVrChatUserIds()
        {
            var userIds = new List<string>();

            try
            {
                // Base path to VRChat's OSC user folders
                var basePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "AppData", "LocalLow", "VRChat", "VRChat", "OSC");

                // If folder doesn't exist, return empty
                if (!Directory.Exists(basePath))
                {
                    Console.WriteLine($"[AllowedForUsingService] VRChat OSC folder not found: {basePath}");
                    return userIds;
                }

                // Get all directories matching "usr_*"
                var userDirectories = Directory.GetDirectories(basePath, "usr_*");
                if (userDirectories == null || userDirectories.Length == 0)
                {
                    Console.WriteLine("[AllowedForUsingService] No user directories found.");
                    return userIds;
                }

                // Extract the user IDs
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

            return userIds.Distinct().ToList(); // Remove duplicates just in case
        }

        /// <summary>
        /// Timer callback: checks the ban status of all known user IDs via API.
        /// Fires the BanDetected event immediately when a user transitions from allowed to banned.
        /// </summary>
        private async Task UserMonitorCallback()
        {
            if (_allUserIds == null || !_allUserIds.Any())
                return; // Skip if no users are loaded

            try
            {
                // Iterate over known user IDs to check their ban status
                foreach (var userId in _allUserIds)
                {
                    bool isCurrentlyAllowed = await CheckSingleUserAsync(userId);

                    lock (_userAllowedCache)
                    {
                        // If we have cached state for this user
                        if (_userAllowedCache.TryGetValue(userId, out bool wasAllowed))
                        {
                            // If the user was previously allowed but now banned
                            if (wasAllowed && !isCurrentlyAllowed)
                            {
                                // Update the cache for consistency
                                _userAllowedCache[userId] = isCurrentlyAllowed;

                                // Fire the BanDetected event immediately with the banned user ID
                                BanDetected?.Invoke(
                                    this,
                                    new BanDetectedEventArgs(userId)
                                );

                                // Break out of the loop once a banned user is found 
                                // to trigger the event without checking further users
                                return;
                            }
                        }
                        else
                        {
                            // In case user is not present in the cache, add them
                            _userAllowedCache[userId] = isCurrentlyAllowed;
                        }

                        // Update the user's allowed status if no ban was detected
                        _userAllowedCache[userId] = isCurrentlyAllowed;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log or handle exception as needed
                Console.WriteLine($"[AllowedForUsingService] Monitoring error: {ex.Message}");
            }
        }


        /// <summary>
        /// Calls the external API for a single user to determine if they are banned.
        /// Returns true if the user is allowed (not banned); false if banned.
        /// </summary>
        /// <param name="userId">Unique portion of the user ID (e.g., after 'usr_').</param>
        private async Task<bool> CheckSingleUserAsync(string userId)
        {
            var payload = new { userId };
            try
            {
                var response = await _httpClient.PostAsJsonAsync(ApiEndpoint, payload);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[AllowedForUsingService] API returned {response.StatusCode}: {errorContent}");
                    // Treat as banned (false) to be safe
                    return true;
                }

                var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse>();
                if (apiResponse == null)
                {
                    Console.WriteLine("[AllowedForUsingService] API response was null.");
                    // Treat as banned (false) to be safe
                    return true;
                }

                // If "isBanned" is true in the API response, the user is banned (not allowed).
                return !apiResponse.isBanned;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AllowedForUsingService] CheckSingleUserAsync error for userId={userId}: {ex.Message}");
                // On exception, treat as banned for safety
                return true;
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
            public string userId { get; set; }
            public bool isBanned { get; set; }
        }

        #endregion
    }
}
