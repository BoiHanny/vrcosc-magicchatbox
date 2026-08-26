using System;
using System.Diagnostics;
using Valve.VR;

namespace vrcosc_magicchatbox.Services.Vr;

/// <summary>
/// Talks to SteamVR's application registry, which is what decides whether MagicChatbox appears
/// in SteamVR's startup list and starts along with it.
/// </summary>
/// <remarks>
/// OpenVR is initialised once per process, so this borrows the session service's runtime handle
/// whenever one is already open and never shuts it down underneath it. On its own it opens a
/// short-lived utility session instead, which is the one application type documented to work
/// without a headset attached.
/// </remarks>
public sealed class SteamVrApplications : ISteamVrApplications
{
    private readonly IOpenVrSessionService _session;
    private readonly object _lock = new();

    public SteamVrApplications(IOpenVrSessionService session)
    {
        _session = session;
    }

    public SteamVrResult Register(string manifestPath, string appKey)
        => Run(applications =>
        {
            EVRApplicationError error = applications.AddApplicationManifest(manifestPath, false);
            if (error != EVRApplicationError.None)
                return SteamVrResult.Failed($"AddApplicationManifest returned {error}");

            // Naming the running process against the key gives SteamVR the link between the
            // manifest and this instance, and settles the entry before auto-launch is set.
            applications.IdentifyApplication((uint)Environment.ProcessId, appKey);

            error = applications.SetApplicationAutoLaunch(appKey, true);
            if (error != EVRApplicationError.None)
                return SteamVrResult.Failed($"SetApplicationAutoLaunch returned {error}");

            return SteamVrResult.Done();
        });

    public SteamVrResult Unregister(string manifestPath, string appKey)
        => Run(applications =>
        {
            // The auto-launch flag lives under the key rather than the manifest and survives the
            // manifest being removed, so it is cleared first or a later re-register comes back
            // already armed.
            if (applications.IsApplicationInstalled(appKey))
                applications.SetApplicationAutoLaunch(appKey, false);

            if (!string.IsNullOrWhiteSpace(manifestPath))
                applications.RemoveApplicationManifest(manifestPath);

            return SteamVrResult.Done();
        });

    public bool IsAutoLaunchEnabled(string appKey)
    {
        bool enabled = false;

        SteamVrResult result = Run(applications =>
        {
            enabled = applications.IsApplicationInstalled(appKey)
                      && applications.GetApplicationAutoLaunch(appKey);
            return SteamVrResult.Done();
        });

        return result.Succeeded && enabled;
    }

    private SteamVrResult Run(Func<CVRApplications, SteamVrResult> work)
    {
        lock (_lock)
        {
            bool borrowed = _session.IsAttached;
            bool initialised = false;

            try
            {
                if (!borrowed)
                {
                    EVRInitError error = EVRInitError.None;
                    Valve.VR.OpenVR.Init(ref error, EVRApplicationType.VRApplication_Utility);

                    if (error != EVRInitError.None)
                        return SteamVrResult.Unavailable($"SteamVR is not available ({error})");

                    initialised = true;
                }

                CVRApplications applications = Valve.VR.OpenVR.Applications;
                if (applications == null)
                    return SteamVrResult.Unavailable("SteamVR did not offer its application registry");

                return work(applications);
            }
            catch (Exception ex)
            {
                return SteamVrResult.Failed(ex.Message);
            }
            finally
            {
                // Never tears down a handle the session service has taken over in the meantime.
                if (initialised && !_session.IsAttached)
                    Valve.VR.OpenVR.Shutdown();
            }
        }
    }
}
