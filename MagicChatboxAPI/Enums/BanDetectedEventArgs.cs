using System;

namespace MagicChatboxAPI.Events
{
    public class BanDetectedEventArgs : EventArgs
    {
        public string UserId { get; }
        public string Reason { get; }

        public BanDetectedEventArgs(string userId, string reason)
        {
            UserId = userId;
            Reason = reason;
        }
    }
}

