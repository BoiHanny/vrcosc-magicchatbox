using System;

namespace MagicChatboxAPI.Enums
{
    public class VRChatUserCheckResult
    {
        public VRChatUserCheckStatus Status { get; set; }

        public bool AnyUserAllowed { get; set; }

        public string ErrorMessage { get; set; } = string.Empty;
    }
}
