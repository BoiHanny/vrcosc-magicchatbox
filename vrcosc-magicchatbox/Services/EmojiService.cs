using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using vrcosc_magicchatbox.Classes.Modules;

namespace vrcosc_magicchatbox.Services;

public partial class EmojiService : ObservableObject
{
    private readonly AppSettings _appSettings;
    private readonly Lock _shuffleGate = new();
    private readonly Random _random = new();

    private Queue<string> _shuffledEmojis = new();
    private ObservableCollection<string>? _observedEmojis;
    private IReadOnlyList<string> _emojiSnapshot = Array.Empty<string>();

    [ObservableProperty]
    private string _currentEmoji = string.Empty;

    public string EmojiListString
    {
        get => string.Join(",", EmojiSnapshot);
        set
        {
            ParseEmojiListString(value);
            OnPropertyChanged(nameof(EmojiListString));
        }
    }

    public IReadOnlyList<string> EmojiSnapshot => Volatile.Read(ref _emojiSnapshot);

    public EmojiService(AppSettings appSettings)
    {
        _appSettings = appSettings;
        _appSettings.PropertyChanged += OnAppSettingsPropertyChanged;
        AttachEmojiCollection(_appSettings.EmojiCollection);
    }

    public bool AddEmoji(string emoji)
    {
        if (string.IsNullOrWhiteSpace(emoji))
            return false;
        if (!_appSettings.EmojiCollection.Contains(emoji))
        {
            _appSettings.EmojiCollection.Add(emoji);
            OnPropertyChanged(nameof(EmojiListString));
        }
        return true;
    }

    public string GetNextEmoji(bool isChat = false)
    {
        const string defaultIcon = "💬";

        IReadOnlyList<string> emojis = EmojiSnapshot;

        if (emojis.Count == 0)
        {
            CurrentEmoji = defaultIcon;
            return defaultIcon;
        }

        if (_appSettings.EnableEmojiShuffle && (isChat ? _appSettings.EnableEmojiShuffleInChats : true))
        {
            lock (_shuffleGate)
            {
                if (_shuffledEmojis.Count == 0)
                    RefillShuffle(emojis);

                if (_shuffledEmojis.Count > 0)
                {
                    string next = _shuffledEmojis.Dequeue();
                    CurrentEmoji = next;
                    return next;
                }
            }
        }

        CurrentEmoji = defaultIcon;
        return defaultIcon;
    }

    public void ShuffleEmojis()
    {
        lock (_shuffleGate)
        {
            RefillShuffle(EmojiSnapshot);
        }
    }

    private void RefillShuffle(IReadOnlyList<string> emojis)
    {
        var shuffled = new List<string>(emojis);

        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        _shuffledEmojis = new Queue<string>(shuffled);
    }

    private void OnAppSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.EmojiCollection))
            AttachEmojiCollection(_appSettings.EmojiCollection);
    }

    private void AttachEmojiCollection(ObservableCollection<string>? collection)
    {
        if (ReferenceEquals(_observedEmojis, collection))
        {
            RefreshSnapshot();
            return;
        }

        if (_observedEmojis != null)
            _observedEmojis.CollectionChanged -= OnEmojiCollectionChanged;

        _observedEmojis = collection;

        if (_observedEmojis != null)
            _observedEmojis.CollectionChanged += OnEmojiCollectionChanged;

        RefreshSnapshot();
    }

    private void OnEmojiCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RefreshSnapshot();

    private void RefreshSnapshot()
    {
        string[] snapshot = _observedEmojis == null
            ? Array.Empty<string>()
            : _observedEmojis.ToArray();

        Volatile.Write(ref _emojiSnapshot, snapshot);

        lock (_shuffleGate)
        {
            _shuffledEmojis.Clear();
        }
    }

    private void ParseEmojiListString(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            _appSettings.EmojiCollection.Clear();
        }
        else
        {
            var emojis = input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(e => e.Trim())
                              .Where(e => !string.IsNullOrWhiteSpace(e));

            _appSettings.EmojiCollection.Clear();
            foreach (var emoji in emojis)
            {
                _appSettings.EmojiCollection.Add(emoji);
            }
        }
    }
}
