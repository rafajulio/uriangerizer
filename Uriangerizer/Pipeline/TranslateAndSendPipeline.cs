using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Uriangerizer.Chat;
using Uriangerizer.Translation;

namespace Uriangerizer.Pipeline;

/// <summary>
/// Translate -> sanitize -> split -> preview or send. Independent of how the text arrived
/// (the /uri command today, a chat-send hook in phase 2), so both entry points can call <see cref="RunAsync"/>.
/// All public methods are safe to fire and forget: every failure ends up as a local-only chat line.
/// </summary>
public sealed class TranslateAndSendPipeline(ITranslator translator, Configuration config)
{
    // Written from thread-pool threads and read from the game thread, hence Interlocked/Volatile.
    private PendingPreview? pending;

    public Task RunAsync(ChannelPrefix channel, string text, CancellationToken cancellationToken) =>
        Guarded(async () =>
        {
            var raw = await translator.TranslateAsync(text, cancellationToken).ConfigureAwait(false);
            var translation = ResponseSanitizer.Clean(raw);
            if (translation.Length == 0)
                throw new TranslationException("The translation was empty after cleanup.");

            var parts = MessageSplitter.Split(translation, config.MaxChars, ChatSender.BodyByteBudget(channel));

            if (config.PreviewMode)
            {
                var previous = Interlocked.Exchange(ref pending, new PendingPreview(channel, parts));
                if (previous is not null)
                    LocalChat.Info("Previous preview discarded.");
                PrintPreview(channel, parts);
                return;
            }

            await ChatSender.SendAllAsync(channel, parts, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    /// <summary>Sends text with no translation (auto mode's escape prefix).</summary>
    public Task SendAsIsAsync(ChannelPrefix channel, string text, CancellationToken cancellationToken) =>
        Guarded(async () =>
        {
            var parts = MessageSplitter.Split(text, config.MaxChars, ChatSender.BodyByteBudget(channel));
            await ChatSender.SendAllAsync(channel, parts, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    public Task SendPendingAsync(CancellationToken cancellationToken) =>
        Guarded(async () =>
        {
            var preview = Interlocked.Exchange(ref pending, null);
            if (preview is null)
            {
                LocalChat.Error("Nothing to send. Translate something with /uri <text> first.");
                return;
            }

            await ChatSender.SendAllAsync(preview.Channel, preview.Parts, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    public void CancelPending()
    {
        if (Interlocked.Exchange(ref pending, null) is null)
            LocalChat.Error("Nothing to cancel.");
        else
            LocalChat.Info("Preview discarded.");
    }

    private static void PrintPreview(ChannelPrefix channel, IReadOnlyList<string> parts)
    {
        for (var i = 0; i < parts.Count; i++)
        {
            var counter = parts.Count > 1 ? $" ({i + 1}/{parts.Count})" : string.Empty;
            LocalChat.Info($"Preview → {channel}{counter}: {parts[i]}");
        }
        LocalChat.Info("/uri send to send it, /uri cancel to discard.");
    }

    private static async Task Guarded(Func<Task> action, CancellationToken cancellationToken)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Plugin unloading; nothing to report.
        }
        catch (TranslationException ex)
        {
            Plugin.Log.Warning(ex.InnerException, "Translation failed: {Reason}", ex.Message);
            LocalChat.Error(ex.Message);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // Rejected by ChatSender before (or while) reaching the game.
            LocalChat.Error($"Not sent: {ex.Message}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Unexpected error in translation pipeline");
            LocalChat.Error("Unexpected error. See /xllog for details.");
        }
    }
}
