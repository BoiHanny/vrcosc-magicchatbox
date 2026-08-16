using System;
using System.Threading.Tasks;
using vrcosc_magicchatbox.ViewModels.Models;
using Windows.Media.Control;
using static WindowsMediaController.MediaManager;

namespace vrcosc_magicchatbox.Services;

public interface IMediaLinkService
{
    DateTime LastMediaChangeTime { get; }

    bool IsRunning { get; }

    void Start();

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
