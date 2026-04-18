namespace vrcosc_magicchatbox.Core.Messaging;

/// <summary>
/// Sent by WhisperModule (or any source) to update the IntelliChat UI status label.
/// IntelliChatModule subscribes and applies it to its own Settings.
/// </summary>
/// <param name="Text">Status text to display.</param>
/// <param name="ShowPermanently">
/// True  → label stays visible until explicitly cleared.
/// False → label flashes for 2.5 s then auto-hides.
/// </param>
public sealed record IntelliChatUiStatusMessage(string Text, bool ShowPermanently);
