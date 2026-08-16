using System;
using System.Threading.Tasks;
using vrcosc_magicchatbox.ViewModels.Models;
using Windows.Media.Control;
using static WindowsMediaController.MediaManager;

namespace vrcosc_magicchatbox.Services;

public interface IMediaLinkService
{
    DateTime LastMediaChangeTime { get; }

    /// <summary>Whether the Windows media session listener is currently attached.</summary>
    bool IsRunning { get; }

    void Start();

    /// <summary>Attaches the listener if the integration is switched on and allowed to run.</summary>
    void StartIfEnabled();
    void Dispose();
    void SelectMediaSession(MediaSessionInfo sessionInfo);
    Task MediaManager_NextAsync(MediaSessionInfo sessionInfo);
    Task MediaManager_PlayPauseAsync(MediaSessionInfo sessionInfo);
    Task MediaManager_PreviousAsync(MediaSessionInfo sessionInfo);
    Task MediaManager_SeekTo(MediaSessionInfo sessionInfo, double position);
    void MediaManager_OnAnyTimelinePropertyChanged(MediaSession sender, GlobalSystemMediaTransportControlsSessionTimelineProperties args);
    void SessionRestore(MediaSessionInfo session);
}
