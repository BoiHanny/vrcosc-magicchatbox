using System;
using System.Threading.Tasks;
using vrcosc_magicchatbox.ViewModels.Models;
using Windows.Media.Control;
using static WindowsMediaController.MediaManager;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Controls Windows media session integration (play/pause, seek, timeline events).
/// </summary>
public interface IMediaLinkService
{
    DateTime LastMediaChangeTime { get; }
    void Start();
    void Dispose();
    void MediaManager_NextAsync(MediaSessionInfo sessionInfo);
    void MediaManager_PlayPauseAsync(MediaSessionInfo sessionInfo);
    Task MediaManager_PreviousAsync(MediaSessionInfo sessionInfo);
    Task MediaManager_SeekTo(MediaSessionInfo sessionInfo, double position);
    void MediaManager_OnAnyTimelinePropertyChanged(MediaSession sender, GlobalSystemMediaTransportControlsSessionTimelineProperties args);
    void SessionRestore(MediaSessionInfo session);
}
