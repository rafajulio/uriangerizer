using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Uriangerizer.Chat;

/// <summary>
/// Splits a long line into chat-sized parts, preferring natural break points:
/// sentence ends first, then clause punctuation, then spaces, and only as a last resort mid-word.
/// </summary>
public static partial class MessageSplitter
{
    public static IReadOnlyList<string> Split(string text, int maxChars, int maxBytes)
    {
        if (maxChars < 1 || maxBytes < 4)
            throw new ArgumentOutOfRangeException(nameof(maxBytes), "Limits are too small to fit any text.");

        bool Fits(string s) => s.Length <= maxChars && Encoding.UTF8.GetByteCount(s) <= maxBytes;

        var parts = Explode(text.Trim(), level: 0, Fits, maxChars, maxBytes);

        // A part starting with '/' would be run as a game command.
        return parts.ConvertAll(p => p.TrimStart('/', ' ')).FindAll(p => p.Length > 0);
    }

    // Breaks text into pieces with this level's separator, splits oversized pieces further with finer
    // separators, then re-joins neighbours greedily. Joining happens per level, so word fragments of one
    // sentence are never glued onto the start of the next sentence.
    private static List<string> Explode(string text, int level, Func<string, bool> fits, int maxChars, int maxBytes)
    {
        if (text.Length == 0)
            return [];

        if (fits(text))
            return [text];

        if (level >= 3)
            return [.. HardSplit(text, maxChars, maxBytes)];

        string[] pieces = level switch
        {
            0 => SentenceBoundary().Split(text),
            1 => ClauseBoundary().Split(text),
            _ => text.Split(' ', StringSplitOptions.RemoveEmptyEntries),
        };

        var parts = new List<string>();
        var current = string.Empty;
        foreach (var piece in pieces)
        {
            foreach (var sub in Explode(piece.Trim(), level + 1, fits, maxChars, maxBytes))
            {
                var candidate = current.Length == 0 ? sub : $"{current} {sub}";
                if (fits(candidate))
                {
                    current = candidate;
                    continue;
                }

                if (current.Length > 0)
                    parts.Add(current);
                current = sub;
            }
        }

        if (current.Length > 0)
            parts.Add(current);
        return parts;
    }

    // Cuts between text elements (never inside a multi-byte character or emoji).
    private static string[] HardSplit(string text, int maxChars, int maxBytes)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            var next = current + element;
            if (next.Length > maxChars || Encoding.UTF8.GetByteCount(next) > maxBytes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            current.Append(element);
        }

        if (current.Length > 0)
            result.Add(current.ToString());
        return result.ToArray();
    }

    // Split after . ! ? … (plus closing quotes/brackets) followed by whitespace; the punctuation stays.
    [GeneratedRegex(@"(?<=[.!?…]+[""'”’)\]]*)\s+")]
    private static partial Regex SentenceBoundary();

    [GeneratedRegex(@"(?<=[,;:—–])\s+")]
    private static partial Regex ClauseBoundary();
}
