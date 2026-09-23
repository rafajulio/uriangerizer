using System;
using System.Collections.Generic;
using System.Linq;

namespace Uriangerizer.Chat;

/// <summary>
/// Decides what auto mode does with a typed line. Pure logic, so it can be reasoned about (and tested)
/// without the game: the interceptor only feeds it the text and the active channel number.
/// </summary>
public static class AutoTranslateRules
{
    /// <summary>
    /// Channel commands auto mode may translate when typed explicitly. Everything else - /sh, /y, /fc,
    /// /t, linkshells, /dance, plugin commands - is passed through untouched.
    /// </summary>
    private static readonly HashSet<string> AllowedPrefixes =
        new(["/s", "/say", "/p", "/party", "/e", "/echo"], StringComparer.OrdinalIgnoreCase);

    public static OutgoingLine Decide(string line, int activeChatType, Configuration config)
    {
        var text = line.Trim();
        if (text.Length == 0)
            return OutgoingLine.PassThrough();

        // Escape hatch: "!omw" is sent as "omw", untranslated. The line is swallowed and re-sent so
        // the prefix itself never reaches the channel. "!/p omw" keeps working as a party message.
        var escape = config.AutoEscapePrefix;
        if (!string.IsNullOrEmpty(escape) && text.StartsWith(escape, StringComparison.Ordinal))
        {
            var stripped = text[escape.Length..].Trim();
            if (stripped.Length == 0)
                return OutgoingLine.PassThrough();

            return stripped.StartsWith('/')
                ? ChannelPrefixParser.TryParse(stripped, out var escChannel, out var escBody, out _) && escBody.Length > 0
                    ? OutgoingLine.SendAsIs(escChannel, escBody)
                    : OutgoingLine.PassThrough()
                : OutgoingLine.SendAsIs(ChannelPrefix.Active, stripped);
        }

        if (text.StartsWith('/'))
        {
            var command = text.Split([' ', '\t'], 2)[0];
            if (!AllowedPrefixes.Contains(command))
                return OutgoingLine.PassThrough();

            return ChannelPrefixParser.TryParse(text, out var channel, out var body, out _) && body.Length > 0
                ? OutgoingLine.Translate(channel, body)
                : OutgoingLine.PassThrough();
        }

        // No prefix: the line goes to whatever channel is currently active.
        return config.AutoChatTypes.Contains(activeChatType)
            ? OutgoingLine.Translate(ChannelPrefix.Active, text)
            : OutgoingLine.PassThrough();
    }

    public static string DescribeAllowedChatTypes(Configuration config) =>
        config.AutoChatTypes.Count == 0 ? "(none)" : string.Join(", ", config.AutoChatTypes.OrderBy(t => t));
}
