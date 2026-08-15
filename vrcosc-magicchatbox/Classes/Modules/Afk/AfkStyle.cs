using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace vrcosc_magicchatbox.Classes.Modules.Afk;

public partial class AfkStyle : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString();
    [ObservableProperty] private string _name = "New style";

    [ObservableProperty] private bool _showPrefix = true;
    [ObservableProperty] private string _prefix = "💤";

    [ObservableProperty] private bool _showTime = true;
    [ObservableProperty] private string _messageWithTime = "ᶜᵘʳʳᵉⁿᵗˡʸ AFK ᶠᵒʳ ";
    [ObservableProperty] private string _messageWithoutTime = "ᶜᵘʳʳᵉⁿᵗˡʸ AFK";

    [ObservableProperty] private bool _isBuiltIn;

    public AfkStyle Clone(string newName) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Name = newName,
        ShowPrefix = ShowPrefix,
        Prefix = Prefix,
        ShowTime = ShowTime,
        MessageWithTime = MessageWithTime,
        MessageWithoutTime = MessageWithoutTime,
        IsBuiltIn = false,
    };

    public string Render(string? elapsed)
    {
        string body = ShowTime && !string.IsNullOrWhiteSpace(elapsed)
            ? MessageWithTime + elapsed
            : MessageWithoutTime;

        string line = ShowPrefix && !string.IsNullOrWhiteSpace(Prefix)
            ? $"{Prefix} {body}"
            : body;

        return line.Replace("\\n", "\n").Replace("/n", "\n");
    }
}

public static class AfkStylePresets
{
    public const string ClassicId = "builtin-classic";

    public const string ClassicPrefix = "💤";
    public const string ClassicWithTime = "ᶜᵘʳʳᵉⁿᵗˡʸ AFK ᶠᵒʳ ";
    public const string ClassicWithoutTime = "ᶜᵘʳʳᵉⁿᵗˡʸ AFK";

    public static IReadOnlyList<AfkStyle> Build() => new List<AfkStyle>
    {
        new()
        {
            Id = ClassicId,
            Name = "Classic",
            IsBuiltIn = true,
            Prefix = ClassicPrefix,
            MessageWithTime = ClassicWithTime,
            MessageWithoutTime = ClassicWithoutTime,
        },
        new()
        {
            Id = "builtin-plain",
            Name = "Plain",
            IsBuiltIn = true,
            Prefix = "💤",
            MessageWithTime = "AFK for ",
            MessageWithoutTime = "AFK",
        },
        new()
        {
            Id = "builtin-smallcaps",
            Name = "Small caps",
            IsBuiltIn = true,
            Prefix = "😴",
            MessageWithTime = "ᴀᴡᴀʏ ꜰᴏʀ ",
            MessageWithoutTime = "ᴀᴡᴀʏ ꜰʀᴏᴍ ᴋᴇʏʙᴏᴀʀᴅ",
        },
        new()
        {
            Id = "builtin-backsoon",
            Name = "Back soon",
            IsBuiltIn = true,
            Prefix = "🚪",
            MessageWithTime = "ᵇᵃᶜᵏ soon, ᵍᵒⁿᵉ ",
            MessageWithoutTime = "ᵇᵃᶜᵏ soon",
        },
        new()
        {
            Id = "builtin-dozing",
            Name = "Dozing off",
            IsBuiltIn = true,
            Prefix = "🌙",
            MessageWithTime = "ᶻᶻᶻ ᶠᵒʳ ",
            MessageWithoutTime = "ᶻᶻᶻ",
        },
        new()
        {
            Id = "builtin-grass",
            Name = "Touching grass",
            IsBuiltIn = true,
            Prefix = "🌱",
            MessageWithTime = "ᵗᵒᵘᶜʰⁱⁿᵍ grass ᶠᵒʳ ",
            MessageWithoutTime = "ᵗᵒᵘᶜʰⁱⁿᵍ grass ʳⁱᵍʰᵗ ⁿᵒʷ",
        },
        new()
        {
            Id = "builtin-gym",
            Name = "At the gym",
            IsBuiltIn = true,
            Prefix = "🏋️",
            MessageWithTime = "ᵃᵗ ᵗʰᵉ GYM ᶠᵒʳ ",
            MessageWithoutTime = "ᵃᵗ ᵗʰᵉ GYM ʳⁱᵍʰᵗ ⁿᵒʷ",
        },
        new()
        {
            Id = "builtin-food",
            Name = "Raiding the fridge",
            IsBuiltIn = true,
            Prefix = "🍜",
            MessageWithTime = "ʳᵃⁱᵈⁱⁿᵍ ᵗʰᵉ fridge ᶠᵒʳ ",
            MessageWithoutTime = "ʳᵃⁱᵈⁱⁿᵍ ᵗʰᵉ fridge",
        },
        new()
        {
            Id = "builtin-coffee",
            Name = "Coffee run",
            IsBuiltIn = true,
            Prefix = "☕",
            MessageWithTime = "coffee ʳᵘⁿ, ᵇᵃᶜᵏ ⁱⁿ ",
            MessageWithoutTime = "coffee ʳᵘⁿ",
        },
        new()
        {
            Id = "builtin-shower",
            Name = "In the shower",
            IsBuiltIn = true,
            Prefix = "🚿",
            MessageWithTime = "ⁱⁿ ᵗʰᵉ shower ᶠᵒʳ ",
            MessageWithoutTime = "ⁱⁿ ᵗʰᵉ shower",
        },
        new()
        {
            Id = "builtin-cat",
            Name = "The cat won",
            IsBuiltIn = true,
            Prefix = "🐈",
            MessageWithTime = "ᵗʰᵉ cat ʷᵒⁿ, ᵍᵒⁿᵉ ",
            MessageWithoutTime = "ᵗʰᵉ cat ʷᵒⁿ",
        },
        new()
        {
            Id = "builtin-oneminute",
            Name = "One minute",
            IsBuiltIn = true,
            Prefix = "⏳",
            MessageWithTime = "ᵒⁿᵉ ᵐⁱⁿᵘᵗᵉ ᵗᵘʳⁿᵉᵈ ⁱⁿᵗᵒ ",
            MessageWithoutTime = "ᵍⁱᵛᵉ ᵐᵉ ᵒⁿᵉ minute",
        },
        new()
        {
            Id = "builtin-onemore",
            Name = "One more game",
            IsBuiltIn = true,
            Prefix = "🎮",
            MessageWithTime = "\"ᵒⁿᵉ ᵐᵒʳᵉ game\" ᶠᵒʳ ",
            MessageWithoutTime = "ᵒⁿᵉ ᵐᵒʳᵉ game, ᵖʳᵒᵐⁱˢᵉ",
        },
        new()
        {
            Id = "builtin-deceased",
            Name = "Deceased",
            IsBuiltIn = true,
            Prefix = "💀",
            MessageWithTime = "ᵈᵉᶜᵉᵃˢᵉᵈ ᶠᵒʳ ",
            MessageWithoutTime = "ᵗᵉᵐᵖᵒʳᵃʳⁱˡʸ deceased",
        },
        new()
        {
            Id = "builtin-phone",
            Name = "On the phone",
            IsBuiltIn = true,
            Prefix = "📞",
            MessageWithTime = "ᵒⁿ ᵗʰᵉ phone ᶠᵒʳ ",
            MessageWithoutTime = "ᵒⁿ ᵗʰᵉ phone",
        },
        new()
        {
            Id = "builtin-staring",
            Name = "Staring at a wall",
            IsBuiltIn = true,
            Prefix = "🧍",
            MessageWithTime = "ˢᵗᵃʳⁱⁿᵍ ᵃᵗ ᵃ wall ᶠᵒʳ ",
            MessageWithoutTime = "ˢᵗᵃʳⁱⁿᵍ ᵃᵗ ᵃ wall",
        },
    };
}
