using System.Threading;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Uriangerizer.Chat;
using Uriangerizer.Commands;
using Uriangerizer.Pipeline;
using Uriangerizer.Translation;
using Uriangerizer.Windows;

namespace Uriangerizer;

public sealed class Plugin : IDalamudPlugin
{
    // Dalamud's dependency injection: before the constructor runs, Dalamud fills every static
    // property marked [PluginService] with the matching game service. They are static so any
    // class in the plugin can reach them without passing references around.
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInterop { get; private set; } = null!;

    // Cancelled on unload so an in-flight request never acts after the plugin is gone.
    private readonly CancellationTokenSource unloadCts = new();

    private readonly WindowSystem windowSystem = new("Uriangerizer");
    private readonly ConfigWindow configWindow;
    private readonly ClaudeTranslator translator;
    private readonly ChatInterceptor chatInterceptor;
    private readonly UriCommand uriCommand;

    public Configuration Configuration { get; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        translator = new ClaudeTranslator(Configuration);
        var pipeline = new TranslateAndSendPipeline(translator, Configuration);

        configWindow = new ConfigWindow(Configuration);
        windowSystem.AddWindow(configWindow);

        // UiBuilder.Draw fires every frame; WindowSystem draws whichever windows are open.
        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        // The gear/"open" buttons in /xlplugins. There is no separate main window, so both open settings.
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;

        chatInterceptor = new ChatInterceptor(Configuration, pipeline);

        uriCommand = new UriCommand(pipeline, Configuration, ToggleConfigUi, unloadCts.Token);
        CommandManager.AddHandler(UriCommand.Name, new CommandInfo(uriCommand.Handle)
        {
            HelpMessage = UriCommand.HelpMessage,
        });
    }

    public void Dispose()
    {
        unloadCts.Cancel();

        CommandManager.RemoveHandler(UriCommand.Name);
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUi;
        windowSystem.RemoveAllWindows();

        chatInterceptor.Dispose();
        translator.Dispose();
        unloadCts.Dispose();
    }

    private void ToggleConfigUi() => configWindow.Toggle();
}
