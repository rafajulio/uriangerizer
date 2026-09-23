using System;
using System.Collections.Generic;
using System.Linq;

namespace Uriangerizer.Chat;

/// <summary>
/// A chat command such as "/p" or "/t First Last@World" put in front of every outgoing line.
/// The game itself routes the line to the right channel, just as if it had been typed.
/// An empty prefix means "the currently active channel".
/// </summary>
public sealed record ChannelPrefix(string Command)
{
    public static readonly ChannelPrefix Active = new(string.Empty);

    public bool IsActiveChannel => Command.Length == 0;

    public string LinePrefix => IsActiveChannel ? string.Empty : Command + " ";

    public override string ToString() => IsActiveChannel ? "active channel" : Command;
}

public static class ChannelPrefixParser
{
    private static readonly HashSet<string> SimpleCommands = new(
        new[]
        {
            "/s", "/say", "/y", "/yell", "/sh", "/shout", "/p", "/party", "/a", "/alliance",
            "/fc", "/freecompany", "/n", "/novice", "/beginner", "/r", "/reply", "/e", "/echo",
        }
        .Concat(Enumerable.Range(1, 8).SelectMany(i => new[]
        {
            $"/l{i}", $"/linkshell{i}", $"/cwl{i}", $"/cwlinkshell{i}",
        })),
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> TellCommands = new(["/t", "/tell"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Splits "/p some text" into (/p, "some text"). Text without a leading '/' goes to the active channel.
    /// Returns false with a user-facing <paramref name="error"/> for unknown or incomplete prefixes.
    /// </summary>
    public static bool TryParse(string input, out ChannelPrefix prefix, out string body, out string error)
    {
        prefix = ChannelPrefix.Active;
        body = input.Trim();
        error = string.Empty;

        if (!body.StartsWith('/'))
            return true;

        var command = TakeToken(ref body);

        if (SimpleCommands.Contains(command))
        {
            prefix = new ChannelPrefix(command.ToLowerInvariant());
        }
        else if (TellCommands.Contains(command))
        {
            // "<t>"-style placeholders (current target etc.) are a single token;
            // character names are always "First Last" or "First Last@World".
            var first = TakeToken(ref body);
            var target = first.StartsWith('<') && first.EndsWith('>')
                ? first
                : $"{first} {TakeToken(ref body)}".Trim();

            if (!target.StartsWith('<') && target.Split(' ').Length != 2)
            {
                error = "Usage: /uri /t First Last@World <text>";
                return false;
            }

            prefix = new ChannelPrefix($"/t {target}");
        }
        else
        {
            error = $"Unknown channel prefix '{command}'. Try /s, /p, /fc, /a, /l1-8, /cwl1-8, /t, /r or /e.";
            return false;
        }

        if (body.Length == 0)
        {
            error = $"Nothing to translate after '{prefix.Command}'.";
            return false;
        }

        return true;
    }

    private static string TakeToken(ref string text)
    {
        text = text.TrimStart();
        var end = text.IndexOfAny([' ', '\t']);
        var token = end < 0 ? text : text[..end];
        text = end < 0 ? string.Empty : text[end..].TrimStart();
        return token;
    }
}
