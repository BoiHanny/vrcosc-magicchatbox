using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using vrcosc_magicchatbox.ViewModels.Models;
using static vrcosc_magicchatbox.Classes.Modules.MediaLinkModule;

namespace vrcosc_magicchatbox.ViewModels.State;

/// <summary>
/// Owns MediaLink session list, saved session settings, and seekbar style selection.
/// Extracted from ViewModel to isolate MediaLink runtime display concerns.
/// </summary>
public sealed partial class MediaLinkDisplayState : ObservableObject
{
    private ObservableCollection<MediaSessionInfo> _mediaSessions = new();
    public ObservableCollection<MediaSessionInfo> MediaSessions
    {
        get => _mediaSessions;
        set { _mediaSessions = value; OnPropertyChanged(); }
    }

    private List<MediaSessionSettings> _savedSessionSettings = new();
    public List<MediaSessionSettings> SavedSessionSettings
    {
        get => _savedSessionSettings;
        set { _savedSessionSettings = value; OnPropertyChanged(); }
    }

    private ObservableCollection<MediaLinkStyle> _mediaLinkSeekbarStyles;
    public ObservableCollection<MediaLinkStyle> MediaLinkSeekbarStyles
    {
        get => _mediaLinkSeekbarStyles;
        set
        {
            if (_mediaLinkSeekbarStyles != value)
            {
                _mediaLinkSeekbarStyles = value;
                OnPropertyChanged();
            }
        }
    }

    private MediaLinkStyle _selectedMediaLinkSeekbarStyle;
    public MediaLinkStyle SelectedMediaLinkSeekbarStyle
    {
        get => _selectedMediaLinkSeekbarStyle;
        set
        {
            if (_selectedMediaLinkSeekbarStyle != value)
            {
                _selectedMediaLinkSeekbarStyle = value;
                OnPropertyChanged();
            }
        }
    }
}
