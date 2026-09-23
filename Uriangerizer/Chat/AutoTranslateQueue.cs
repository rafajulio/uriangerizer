using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Uriangerizer.Pipeline;

namespace Uriangerizer.Chat;

/// <summary>
/// Processes intercepted lines one at a time, in the order they were typed. Without this, two quick
/// messages would race through two API calls and could arrive swapped.
/// </summary>
public sealed class AutoTranslateQueue(TranslateAndSendPipeline pipeline) : IDisposable
{
    // A runaway loop would spam the channel and risk a chat ban, so cap what auto mode may send.
    private const int MaxLinesPerMinute = 20;

    private readonly Channel<OutgoingLine> channel = Channel.CreateUnbounded<OutgoingLine>(
        new UnboundedChannelOptions { SingleReader = true });

    private readonly Queue<DateTime> recentSends = new();
    private readonly CancellationTokenSource cts = new();
    private Task? worker;

    public void Enqueue(OutgoingLine line)
    {
        if (!RateLimitAllows())
        {
            LocalChat.Error("Auto-translate rate limit reached; message dropped. Use /uri off if this repeats.");
            return;
        }

        worker ??= Task.Run(() => RunAsync(cts.Token), cts.Token);
        channel.Writer.TryWrite(line);
    }

    private bool RateLimitAllows()
    {
        var now = DateTime.UtcNow;
        while (recentSends.Count > 0 && now - recentSends.Peek() > TimeSpan.FromMinutes(1))
            recentSends.Dequeue();

        if (recentSends.Count >= MaxLinesPerMinute)
            return false;

        recentSends.Enqueue(now);
        return true;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var line in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (line.Action == OutgoingAction.SendAsIs)
                    await pipeline.SendAsIsAsync(line.Channel, line.Text, cancellationToken).ConfigureAwait(false);
                else
                    await pipeline.RunAsync(line.Channel, line.Text, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // plugin unloading
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Auto-translate queue stopped");
        }
    }

    public void Dispose()
    {
        cts.Cancel();
        channel.Writer.TryComplete();
        cts.Dispose();
    }
}
