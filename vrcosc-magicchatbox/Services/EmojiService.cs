using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Manages emoji cycling, shuffling, and list operations.
/// Reads the emoji collection from AppSettings.
/// </summary>
public partial class EmojiService : ObservableObject
{
    private readonly AppSettings _appSettings;
    private Queue<string> _shuffledEmojis;
    private readonly Random _random = new();

    [ObservableProperty]
    private string _currentEmoji;

    public string EmojiListString
    {
        get => string.Join(",", _appSettings.EmojiCollection);
        set
        {
            ParseEmojiListString(value);
            OnPropertyChanged(nameof(EmojiListString));
        }
    }

    public EmojiService(AppSettings appSettings)
    {
        _appSettings = appSettings;
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

        if (_appSettings.EmojiCollection == null || !_appSettings.EmojiCollection.Any())
        {
            CurrentEmoji = defaultIcon;
            return defaultIcon;
        }

        if (_appSettings.EnableEmojiShuffle && (isChat ? _appSettings.EnableEmojiShuffleInChats : true))
        {
            if (_shuffledEmojis == null || _shuffledEmojis.Count == 0)
                ShuffleEmojis();

            if (_shuffledEmojis.Count > 0)
            {
                CurrentEmoji = _shuffledEmojis.Dequeue();
                return CurrentEmoji;
            }
        }

        CurrentEmoji = defaultIcon;
        return defaultIcon;
    }

    public void ShuffleEmojis()
    {
        var shuffledList = _appSettings.EmojiCollection.OrderBy(_ => _random.Next()).ToList();
        _shuffledEmojis = new Queue<string>(shuffledList);
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
