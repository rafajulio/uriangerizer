using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Uriangerizer.Chat;
using Uriangerizer.Translation;

namespace Uriangerizer.Windows;

// Dalamud's Window base class wraps an ImGui window. ImGui is "immediate mode": Draw() runs every
// frame and re-declares every widget; a widget returns true on the frame its value changed.
public class ConfigWindow : Window
{
    private readonly Configuration config;

    // The "###..." suffix is the ImGui ID; the text before it is only the visible title.
    public ConfigWindow(Configuration config) : base("Uriangerizer Settings###UriangerizerConfig")
    {
        this.config = config;
        Size = new Vector2(560, 480);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        // ImGui needs `ref` to a variable, and properties can't be passed by ref,
        // hence the local copy -> edit -> write back pattern.
        var apiKey = config.ApiKey;
        if (ImGui.InputText("API key", ref apiKey, 256, ImGuiInputTextFlags.Password))
        {
            config.ApiKey = apiKey.Trim();
            config.Save();
        }
        ImGui.TextDisabled("Stored in plain text in pluginConfigs\\Uriangerizer.json.");

        var model = config.Model;
        if (ImGui.InputText("Model", ref model, 128))
        {
            config.Model = model.Trim();
            config.Save();
        }
        ImGui.SameLine();
        if (ImGui.SmallButton("Default##model"))
        {
            config.Model = Configuration.DefaultModel;
            config.Save();
        }

        var maxChars = config.MaxChars;
        if (ImGui.InputInt("Max characters", ref maxChars, 10))
        {
            config.MaxChars = Math.Clamp(maxChars, 20, 480);
            config.Save();
        }

        var timeout = config.TimeoutSeconds;
        if (ImGui.InputInt("Timeout (seconds)", ref timeout, 1))
        {
            config.TimeoutSeconds = Math.Clamp(timeout, 1, 120);
            config.Save();
        }

        var preview = config.PreviewMode;
        if (ImGui.Checkbox("Preview mode (confirm with /uri send, discard with /uri cancel)", ref preview))
        {
            config.PreviewMode = preview;
            config.Save();
        }

        ImGui.Separator();
        var status = config.CustomSystemPrompt is null ? "built-in default, updates with the plugin" : "customized";
        ImGui.TextUnformatted($"System prompt ({status}). {DefaultPrompt.MaxCharsPlaceholder} is replaced by the max characters:");
        var prompt = config.GetSystemPrompt();
        var size = new Vector2(-1, ImGui.GetContentRegionAvail().Y - ImGui.GetFrameHeightWithSpacing());
        if (ImGui.InputTextMultiline("##systemPrompt", ref prompt, 16384, size))
        {
            config.SetSystemPrompt(prompt);
            config.Save();
        }
        if (ImGui.Button("Reset prompt to default"))
        {
            config.CustomSystemPrompt = null;
            config.Save();
        }
    }
}
