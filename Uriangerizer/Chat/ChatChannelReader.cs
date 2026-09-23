using FFXIVClientStructs.FFXIV.Client.UI.Shell;

namespace Uriangerizer.Chat;

/// <summary>Reads the chat channel the player currently has selected.</summary>
public static unsafe class ChatChannelReader
{
    /// <summary>The game's internal channel number, or -1 when it cannot be read. 1 is Say.</summary>
    public static int CurrentChatType()
    {
        var shell = RaptureShellModule.Instance();
        return shell == null ? -1 : shell->ChatType;
    }
}
