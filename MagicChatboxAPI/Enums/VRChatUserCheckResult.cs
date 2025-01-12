using System;

namespace MagicChatboxAPI.Enums
{
    /// <summary>
    /// Detailed information from a check operation.
    /// </summary>
    public class VRChatUserCheckResult
    {
        /// <summary>
        /// Overall status of the check.
        /// </summary>
        public VRChatUserCheckStatus Status { get; set; }

        /// <summary>
        /// Indicates if the check completed with any user
        /// being allowed or no new bans found.
        /// </summary>
        public bool AnyUserAllowed { get; set; }

        /// <summary>
        /// Optional error message if something went wrong.
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}
