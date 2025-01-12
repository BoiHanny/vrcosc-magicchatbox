using System;

namespace MagicChatboxAPI.Enums
{
    /// <summary>
    /// Possible outcomes for user checks.
    /// </summary>
    public enum VRChatUserCheckStatus
    {
        Success = 0,
        NoFolderFound,
        NoUserIdsFound,
        ApiError,
        ApiTimeout,
        UnknownError
    }
}
