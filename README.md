# Uriangerizer

A [Dalamud](https://github.com/goatcorp/Dalamud) plugin for Final Fantasy XIV that rewrites chat
messages written in Brazilian Portuguese as archaic, ornate English in the voice of Urianger
Augurelt, using the Claude API, and sends the result in game.

Personal project, run as a local dev plugin. It is not published to the official plugin repository.

## Features

- `/uri <text>` translates and sends to the active chat channel.
- Channel prefixes: `/uri /p <text>`, `/uri /fc <text>`, `/uri /t First Last@World <text>`,
  plus `/s`, `/y`, `/sh`, `/a`, `/e`, `/l1`-`/l8` and `/cwl1`-`/cwl8`.
- Auto mode (`/uri on`, `/uri off`, `/uri toggle`): everything typed into the allowed channels is
  translated on the way out. Prefix a line with `!` to send it untranslated.
- Messages longer than the chat limit are split at sentence, then clause, then word boundaries,
  never mid-character, and sent in order with a pause between parts.
- Preview mode: the translation is shown to you first; `/uri send` sends it, `/uri cancel` drops it.
- Configuration window (`/uri config`): API key, model, character limit, request timeout, preview
  mode, auto mode, and an editable system prompt.
- Errors (timeout, invalid key, network, empty response) are printed locally only. Nothing that
  failed is ever sent to other players.

## Requirements

- FFXIV on PC, launched through [XIVLauncher](https://goatcorp.github.io/) with Dalamud enabled.
- .NET 10 SDK.
- An Anthropic API key.

## Build

```
dotnet build
```

The plugin is written against `Dalamud.NET.Sdk` 15 (API level 15), which resolves the game
references from the local Dalamud install at `%AppData%\XIVLauncher\addon\Hooks\dev\`. Output lands
in `Uriangerizer\bin\x64\Debug\Uriangerizer.dll`.

## Install as a dev plugin

1. In game, type `/xlsettings` and open the **Experimental** tab.
2. Under **Dev Plugin Locations**, add the full path to `Uriangerizer.dll` and save.
3. Type `/xlplugins`, then enable the plugin under **Dev Tools > Installed Dev Plugins**.
4. Open `/uri config` and paste your API key.

## Commands

| Command | What it does |
| --- | --- |
| `/uri <text>` | Translate and send to the active channel |
| `/uri /p <text>` | Translate and send to a specific channel |
| `/uri on` / `off` / `toggle` | Auto mode for everything you type |
| `/uri chan` | Show the active channel number and which ones auto mode covers |
| `/uri chan add` / `remove` | Include or exclude the current channel in auto mode |
| `/uri send` / `cancel` | Confirm or discard a pending preview |
| `/uri config` | Open the settings window |

## Notes

- The API key is stored in plain text in `%AppData%\XIVLauncher\pluginConfigs\Uriangerizer.json`.
  It is never written to logs or to chat.
- Auto mode hooks `ShellCommandModule.ExecuteCommandInner`, the function the chat box calls for
  everything typed into it. The plugin's own sends are marked and skipped, so a translation is never
  translated again. A rate limit caps what auto mode can send.
- Anything the rules do not explicitly claim (other channels, `/dance`, plugin commands, macros) is
  passed through to the game untouched.
