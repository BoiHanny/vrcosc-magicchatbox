using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using vrcosc_magicchatbox.ViewModels.Models;
using static vrcosc_magicchatbox.Classes.Modules.MediaLinkModule;

namespace vrcosc_magicchatbox.ViewModels.State;

public sealed partial class MediaLinkDisplayState : ObservableObject
{
    private IReadOnlyList<MediaSessionInfo> _mediaSessionsSnapshot = Array.Empty<MediaSessionInfo>();

    private ObservableCollection<MediaSessionInfo> _mediaSessions = new();

    public MediaLinkDisplayState()
    {
        _mediaSessions.CollectionChanged += OnMediaSessionsChanged;
        RefreshMediaSessionsSnapshot();
    }

    public ObservableCollection<MediaSessionInfo> MediaSessions
    {
        get => _mediaSessions;
        set
        {
            var replacement = value ?? new ObservableCollection<MediaSessionInfo>();

            if (!ReferenceEquals(_mediaSessions, replacement))
            {
                _mediaSessions.CollectionChanged -= OnMediaSessionsChanged;
                _mediaSessions = replacement;
                _mediaSessions.CollectionChanged += OnMediaSessionsChanged;
            }

            RefreshMediaSessionsSnapshot();
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<MediaSessionInfo> MediaSessionsSnapshot => Volatile.Read(ref _mediaSessionsSnapshot);

    private void OnMediaSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RefreshMediaSessionsSnapshot();

    private void RefreshMediaSessionsSnapshot()
    {
        var source = _mediaSessions;

        IReadOnlyList<MediaSessionInfo> snapshot = source.Count == 0
            ? Array.Empty<MediaSessionInfo>()
            : source.ToArray();

        Volatile.Write(ref _mediaSessionsSnapshot, snapshot);
    }

    private List<MediaSessionSettings> _savedSessionSettings = new();
    public List<MediaSessionSettings> SavedSessionSettings
    {
        get => _savedSessionSettings;
        set { _savedSessionSettings = value; OnPropertyChanged(); }
    }

    private ObservableCollection<MediaLinkStyle> _mediaLinkSeekbarStyles = new();
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

    private MediaLinkStyle? _selectedMediaLinkSeekbarStyle;
    public MediaLinkStyle? SelectedMediaLinkSeekbarStyle
    {
        get => _selectedMediaLinkSeekbarStyle;
        set
        {
            if (value == null)
                return;

            if (_selectedMediaLinkSeekbarStyle != value)
            {
                _selectedMediaLinkSeekbarStyle = value;
                OnPropertyChanged();
            }
        }
    }
}
