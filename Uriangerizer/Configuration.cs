using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Uriangerizer.Translation;

namespace Uriangerizer;

// Dalamud serializes this class to %AppData%\XIVLauncher\pluginConfigs\Uriangerizer.json.
// New properties just need a default value; old config files get it when loaded.
[Serializable]
public class Configuration : IPluginConfiguration
{
    public const string DefaultModel = "claude-haiku-4-5-20251001";

    public int Version { get; set; } = 0;

    // Stored in plain text in the JSON file above. Never log it.
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = DefaultModel;
    public int MaxChars { get; set; } = 400;
    public int TimeoutSeconds { get; set; } = 10;
    public bool PreviewMode { get; set; } = false;

    // Auto mode (phase 2): translate everything typed into say/party, without /uri.
    public bool AutoTranslate { get; set; } = false;
    // A line starting with this is sent untranslated, as an escape hatch.
    public string AutoEscapePrefix { get; set; } = "!";
    // Active-channel numbers (RaptureShellModule.ChatType) auto mode may translate. 1 is Say;
    // use /uri chan add while in another channel to learn and add its number.
    public List<int> AutoChatTypes { get; set; } = [1];
    // Print a local note when a typed line is swallowed for translation.
    public bool AutoNotify { get; set; } = true;

    // Null means "use the built-in default", so improvements to DefaultPrompt reach the user on the next
    // build instead of being frozen in the JSON. Only an actually edited prompt is stored here.
    public string? CustomSystemPrompt { get; set; }

    public string GetSystemPrompt() => CustomSystemPrompt ?? DefaultPrompt.Text;

    public void SetSystemPrompt(string prompt) =>
        CustomSystemPrompt = string.IsNullOrWhiteSpace(prompt) || prompt == DefaultPrompt.Text ? null : prompt;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
