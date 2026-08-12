using System;

namespace MagicChatboxAPI.Enums
{
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
