using System;

namespace vrcosc_magicchatbox.Services;

public interface ILiveTypingService
{
    event Action? FinalizeRequested;

    bool IsHolding { get; }

    void Show(string text);

    void Release(bool clearChatbox);
}
