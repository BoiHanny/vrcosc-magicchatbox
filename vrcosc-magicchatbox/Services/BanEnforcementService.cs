using System;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.State;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Enforces user bans by notifying the user and terminating the application.
/// Local data is deliberately left intact: a ban is an unauthenticated server
/// signal and must never destroy the user's settings, tokens, or history.
/// </summary>
public class BanEnforcementService : IBanEnforcementService
{
    private readonly IAppState _appState;

    public BanEnforcementService(IAppState appState)
    {
        _appState = appState;
    }

    public void ProcessBan(string bannedUserID, string reason)
    {
        try
        {
            Logging.WriteInfo($"Ban signal received for {bannedUserID}; closing application, local data left intact.");

            _appState.MainWindowBlurEffect = 10;

            Logging.WriteException(
                new Exception("You have been banned from using MagicChatbox.\n\n" +
                   $"Reason: {reason}\n\n" +
                              "There is no need to appeal this ban; we have a zero-tolerance policy."),
                MSGBox: true,
                exitapp: false,
                autoclose: true);
        }
        finally
        {
            Environment.Exit(1);
        }
    }
}
