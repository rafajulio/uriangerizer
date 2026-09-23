using System;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using FFXIVClientStructs.FFXIV.Component.Shell;
using Uriangerizer.Pipeline;

namespace Uriangerizer.Chat;

/// <summary>
/// Auto mode: intercepts what the player types in the chat box, and replaces it with the translation.
///
/// The hook sits on ShellCommandModule.ExecuteCommandInner, which the chat box calls for everything
/// typed into it: plain text, chat channel commands (/p, /fc) and other commands (/dance, plugin
/// commands). UIModule.ProcessChatBoxEntry, which the plugin uses to send, is a different entry point
/// and is never called when the player presses Enter.
///
/// Safety rules, in order of importance:
/// - the plugin's own sends pass through untouched (ChatSender.IsSending), so a translation is never
///   translated again - that is what would otherwise become an endless loop;
/// - anything the rules do not explicitly claim is passed through unchanged;
/// - the queue enforces a rate limit, so a bug cannot flood the channel;
/// - the detour never throws: an exception inside the game's call stack would crash the client.
/// </summary>
public sealed unsafe class ChatInterceptor : IDisposable
{
    private readonly Configuration config;
    private readonly AutoTranslateQueue queue;
    private readonly Hook<ShellCommandModule.Delegates.ExecuteCommandInner> hook;

    public ChatInterceptor(Configuration config, TranslateAndSendPipeline pipeline)
    {
        this.config = config;
        queue = new AutoTranslateQueue(pipeline);

        // A hook rewrites the start of a game function so our code runs first. The address comes from
        // FFXIVClientStructs' signature scan, which is why it survives most game patches.
        hook = Plugin.GameInterop.HookFromAddress<ShellCommandModule.Delegates.ExecuteCommandInner>(
            ShellCommandModule.Addresses.ExecuteCommandInner.Value,
            Detour);
        hook.Enable();
    }

    public void Dispose()
    {
        hook.Disable();
        hook.Dispose();
        queue.Dispose();
    }

    private void Detour(ShellCommandModule* shellCommandModule, Utf8String* command, UIModule* uiModule)
    {
        try
        {
            if (ShouldSwallow(command))
                return; // the line is ours now; the original function is deliberately not called
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "ChatInterceptor.Detour failed; passing the line through");
        }

        hook.Original(shellCommandModule, command, uiModule);
    }

    private bool ShouldSwallow(Utf8String* command)
    {
        // ChatSender.IsSending is true while the plugin itself is pushing a line into the chat box.
        if (!config.AutoTranslate || ChatSender.IsSending || !Plugin.ClientState.IsLoggedIn)
            return false;

        var shell = RaptureShellModule.Instance();
        var chatType = shell == null ? -1 : shell->ChatType;
        var decision = AutoTranslateRules.Decide(command->ToString(), chatType, config);

        switch (decision.Action)
        {
            case OutgoingAction.Translate:
                if (config.AutoNotify)
                    LocalChat.Info($"Translating: {decision.Text}");
                queue.Enqueue(decision);
                return true;

            case OutgoingAction.SendAsIs:
                queue.Enqueue(decision);
                return true;

            default:
                return false;
        }
    }
}
