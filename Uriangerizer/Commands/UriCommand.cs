using System;
using System.Threading;
using Uriangerizer.Chat;
using Uriangerizer.Pipeline;

namespace Uriangerizer.Commands;

/// <summary>Parses "/uri ..." and hands the work to the pipeline. Contains no translation logic.</summary>
public sealed class UriCommand(
    TranslateAndSendPipeline pipeline,
    Configuration config,
    Action toggleConfigUi,
    CancellationToken unloadToken)
{
    public const string Name = "/uri";

    public const string HelpMessage =
        "Translate Portuguese into Urianger-style English. " +
        "/uri <text> | /uri /p <text> | /uri /t First Last@World <text> | " +
        "/uri send | /uri cancel | /uri on | /uri off | /uri toggle | /uri chan | /uri config";

    // Runs on the game's framework thread; `args` is everything after "/uri ".
    // Async work is fired and forgotten: the pipeline reports its own errors, and the game never waits.
    public void Handle(string command, string args)
    {
        var input = args.Trim();
        switch (input.ToLowerInvariant())
        {
            case "":
                LocalChat.Error($"Usage: {HelpMessage}");
                return;
            case "config":
                toggleConfigUi();
                return;
            case "send":
                _ = pipeline.SendPendingAsync(unloadToken);
                return;
            case "cancel":
                pipeline.CancelPending();
                return;
            case "on":
            case "off":
            case "toggle":
                SetAutoTranslate(input.ToLowerInvariant() switch
                {
                    "on" => true,
                    "off" => false,
                    _ => !config.AutoTranslate,
                });
                return;
        }

        if (input.StartsWith("chan", StringComparison.OrdinalIgnoreCase))
        {
            HandleChannelCommand(input[4..].Trim());
            return;
        }

        if (!ChannelPrefixParser.TryParse(input, out var channel, out var text, out var error))
        {
            LocalChat.Error(error);
            return;
        }

        _ = pipeline.RunAsync(channel, text, unloadToken);
    }

    // Auto mode needs to know which active channel is which, and those numbers are internal to the
    // game. Rather than guessing, this reports the current one so it can be allowed explicitly.
    private void HandleChannelCommand(string argument)
    {
        var current = ChatChannelReader.CurrentChatType();
        switch (argument.ToLowerInvariant())
        {
            case "add":
                if (!config.AutoChatTypes.Contains(current))
                {
                    config.AutoChatTypes.Add(current);
                    config.Save();
                }
                LocalChat.Info($"Auto-translate now covers channel {current}. Allowed: {AutoTranslateRules.DescribeAllowedChatTypes(config)}");
                return;

            case "remove":
                if (config.AutoChatTypes.Remove(current))
                    config.Save();
                LocalChat.Info($"Auto-translate no longer covers channel {current}. Allowed: {AutoTranslateRules.DescribeAllowedChatTypes(config)}");
                return;

            default:
                LocalChat.Info($"Active channel is {current}. Auto-translate covers: {AutoTranslateRules.DescribeAllowedChatTypes(config)} (/uri chan add | /uri chan remove)");
                return;
        }
    }

    private void SetAutoTranslate(bool enabled)
    {
        config.AutoTranslate = enabled;
        config.Save();
        LocalChat.Info(enabled
            ? $"Auto-translate is ON for channels {AutoTranslateRules.DescribeAllowedChatTypes(config)} and /s, /p, /e. Prefix a line with '{config.AutoEscapePrefix}' to skip translation."
            : "Auto-translate is OFF.");
    }
}
