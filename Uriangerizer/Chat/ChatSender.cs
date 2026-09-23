using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Uriangerizer.Chat;

/// <summary>
/// Sends text through the game's chat box, exactly as if the player had typed it and pressed Enter.
/// This is the only place in the plugin that touches native game memory.
/// </summary>
public static class ChatSender
{
    // The chat box rejects anything longer than 500 bytes of UTF-8 (channel prefix included).
    public const int MaxMessageBytes = 500;

    // Pause between parts of a split message, to stay well clear of the game's spam filter.
    private static readonly TimeSpan DelayBetweenParts = TimeSpan.FromSeconds(1.2);

    // Same filter the community ECommons library uses before sending chat: letters, numbers,
    // punctuation, other printable characters. Anything else (control bytes, raw payloads) is stripped.
    private const AllowedEntities SanitizeFlags =
        AllowedEntities.UppercaseLetters | AllowedEntities.LowercaseLetters | AllowedEntities.Numbers |
        AllowedEntities.SpecialCharacters | AllowedEntities.CharacterList | AllowedEntities.OtherCharacters |
        AllowedEntities.Payloads | AllowedEntities.Unknown9;

    /// <summary>
    /// True while this class is pushing a line into the chat box. The auto-mode hook checks it so the
    /// plugin never intercepts (and re-translates) its own output. Only ever touched on the framework
    /// thread, where the send and the hook both run.
    /// </summary>
    public static bool IsSending { get; private set; }

    public static int BodyByteBudget(ChannelPrefix channel) =>
        MaxMessageBytes - Encoding.UTF8.GetByteCount(channel.LinePrefix);

    /// <summary>
    /// Sends every part in order on the game's framework thread. All parts are validated first,
    /// so an invalid part never leaves the conversation half sent.
    /// Throws <see cref="ArgumentException"/> before sending anything when a part is not sendable.
    /// </summary>
    public static async Task SendAllAsync(ChannelPrefix channel, IReadOnlyList<string> parts, CancellationToken cancellationToken)
    {
        var lines = new List<string>(parts.Count);
        foreach (var part in parts)
            lines.Add(BuildLine(channel, part));

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            // Calling game functions from a thread-pool thread can crash the client, so each send runs
            // on the framework (main game loop) thread. RunOnTick also lets us wait between parts
            // without blocking the game.
            await Plugin.Framework.RunOnTick(
                () => SendOnFrameworkThread(line),
                delay: i == 0 ? TimeSpan.Zero : DelayBetweenParts,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private static string BuildLine(ChannelPrefix channel, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Message is empty.");
        if (body.StartsWith('/'))
            throw new ArgumentException("Message text may not start with '/'.");
        if (body.Contains('\n') || body.Contains('\r'))
            throw new ArgumentException("Message contains a line break.");

        var line = channel.LinePrefix + body;
        var bytes = Encoding.UTF8.GetByteCount(line);
        if (bytes > MaxMessageBytes)
            throw new ArgumentException($"Message is {bytes} bytes; the chat limit is {MaxMessageBytes}.");

        return line;
    }

    // `unsafe` allows raw pointers. FFXIVClientStructs exposes game structs as pointers into the
    // game's own memory, so the <AllowUnsafeBlocks> flag in the csproj is required for this.
    private static unsafe void SendOnFrameworkThread(string line)
    {
        if (!Plugin.ClientState.IsLoggedIn)
            throw new InvalidOperationException("Not logged in.");

        var uiModule = UIModule.Instance();
        if (uiModule == null)
            throw new InvalidOperationException("UIModule is not available.");

        // Allocated in the game's heap, so it must be freed with Dtor(true), not by the .NET GC.
        var message = Utf8String.FromString(line);
        try
        {
            message->SanitizeString(SanitizeFlags);
            if (message->Length == 0)
                throw new InvalidOperationException("Message was empty after the game's sanitization.");

            IsSending = true;
            try
            {
                uiModule->ProcessChatBoxEntry(message);
            }
            finally
            {
                IsSending = false;
            }
        }
        finally
        {
            message->Dtor(true);
        }
    }
}
