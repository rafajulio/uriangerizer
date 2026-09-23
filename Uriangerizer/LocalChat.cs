namespace Uriangerizer;

/// <summary>Messages only the local player sees. Nothing here is ever sent to other players.</summary>
public static class LocalChat
{
    private const string Tag = "Uriangerizer";

    public static void Info(string message) => Plugin.ChatGui.Print(message, Tag);

    public static void Error(string message) => Plugin.ChatGui.PrintError(message, Tag);
}
