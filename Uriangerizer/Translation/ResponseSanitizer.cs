using System.Text.RegularExpressions;

namespace Uriangerizer.Translation;

/// <summary>Turns raw model output into a single clean chat line.</summary>
public static partial class ResponseSanitizer
{
    // Pairs of wrapping quotes the model might put around the whole answer.
    private static readonly (char Open, char Close)[] QuotePairs =
    [
        ('"', '"'), ('“', '”'), ('\'', '\''), ('‘', '’'), ('«', '»'), ('`', '`'),
    ];

    public static string Clean(string raw)
    {
        var text = raw.Trim();

        // "Translation: ..." / "Here is the translation: ..."
        text = PreambleRegex().Replace(text, string.Empty);

        // Everything on a single line.
        text = WhitespaceRegex().Replace(text, " ").Trim();

        // Markdown: headings, quotes and bullets at the start, then emphasis/code markers anywhere.
        text = LeadingMarkdownRegex().Replace(text, string.Empty);
        text = text.Replace("**", string.Empty).Replace("__", string.Empty).Replace("*", string.Empty).Replace("`", string.Empty);

        // Trailing notes such as "(Note: ...)" or "(Translated from Portuguese)".
        text = TrailingNoteRegex().Replace(text, string.Empty).Trim();

        text = StripWrappingQuotes(text);

        // A leading '/' would make the game run the output as a command.
        text = text.TrimStart('/', ' ');

        return text;
    }

    private static string StripWrappingQuotes(string text)
    {
        bool changed;
        do
        {
            changed = false;
            foreach (var (open, close) in QuotePairs)
            {
                if (text.Length >= 2 && text[0] == open && text[^1] == close)
                {
                    text = text[1..^1].Trim();
                    changed = true;
                }
            }
        } while (changed);

        return text;
    }

    [GeneratedRegex(@"^\s*(here(?:'s| is)[^:\n]{0,40}translation[^:\n]{0,20}|translation|translated|urianger)\s*:\s*", RegexOptions.IgnoreCase)]
    private static partial Regex PreambleRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^(?:#{1,6}\s+|>\s*|[-*+]\s+)+")]
    private static partial Regex LeadingMarkdownRegex();

    [GeneratedRegex(@"\s*[\(\[](?:note|translat)[^\)\]]*[\)\]]\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingNoteRegex();
}
