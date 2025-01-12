using System;

namespace MagicChatboxAPI.Events
{
    /// <summary>
    /// Event arguments fired when a newly banned user is detected.
    /// </summary>
    public class BanDetectedEventArgs : EventArgs
    {
        /// <summary>
        /// The user ID found to be banned in the latest check.
        /// </summary>
        public string BannedUserId { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="BanDetectedEventArgs"/>.
        /// </summary>
        /// <param name="bannedUserId">ID of the newly banned user.</param>
        public BanDetectedEventArgs(string bannedUserId)
        {
            BannedUserId = bannedUserId ?? string.Empty;
        }
    }
}
