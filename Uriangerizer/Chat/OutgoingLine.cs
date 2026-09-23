namespace Uriangerizer.Chat;

/// <summary>What auto mode decided to do with one line typed into the chat box.</summary>
public enum OutgoingAction
{
    /// <summary>Let the game handle the line unchanged (commands, other channels, escaped text).</summary>
    PassThrough,

    /// <summary>Swallow the line, translate it, and send the translation in its place.</summary>
    Translate,

    /// <summary>Swallow the line and re-send it unchanged (used to drop the escape prefix).</summary>
    SendAsIs,
}

public readonly record struct OutgoingLine(OutgoingAction Action, ChannelPrefix Channel, string Text)
{
    public static OutgoingLine PassThrough() => new(OutgoingAction.PassThrough, ChannelPrefix.Active, string.Empty);

    public static OutgoingLine Translate(ChannelPrefix channel, string text) =>
        new(OutgoingAction.Translate, channel, text);

    public static OutgoingLine SendAsIs(ChannelPrefix channel, string text) =>
        new(OutgoingAction.SendAsIs, channel, text);
}
