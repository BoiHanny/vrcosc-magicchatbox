using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace vrcosc_magicchatbox.Classes.Modules.Afk;

/// <summary>
/// One saved way of saying you are away. Everything the chatbox line is built from lives here, so a
/// style can be swapped whole from the side panel without opening Options and editing four fields.
/// </summary>
public partial class AfkStyle : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString();
    [ObservableProperty] private string _name = "New style";

    [ObservableProperty] private bool _showPrefix = true;
    [ObservableProperty] private string _prefix = "💤";

    [ObservableProperty] private bool _showTime = true;
    [ObservableProperty] private string _messageWithTime = "ᶜᵘʳʳᵉⁿᵗˡʸ AFK ᶠᵒʳ ";
    [ObservableProperty] private string _messageWithoutTime = "ᶜᵘʳʳᵉⁿᵗˡʸ AFK";

    /// <summary>Shipped styles can be edited freely but not deleted, so the list can never end up empty.</summary>
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

    /// <summary>
    /// Builds the line exactly as the chatbox will receive it. Taking a duration rather than reading
    /// the clock keeps this a pure function, which is what makes the Options preview and the real
    /// output impossible to drift apart - they call the same code.
    /// </summary>
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

    /// <summary>The long-standing defaults, kept exactly so upgrading changes nothing on screen.</summary>
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
            MessageWithTime = "ᵇᵃᶜᵏ ˢᵒᵒⁿ, ᵍᵒⁿᵉ ",
            MessageWithoutTime = "ᵇᵃᶜᵏ ˢᵒᵒⁿ",
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
    };
}
