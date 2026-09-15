# PROJECT KNOWLEDGE BASE

**Generated:** 2026-09-14
**Commit:** dc1c497
**Branch:** main

## OVERVIEW
EndfieldIsland — a Windows "Dynamic Island" style battery/power HUD (Avalonia 11 / .NET 8, x64) whose business features are dynamically-loaded plugins. The root `EndfieldCharge.csproj` is the only executable; everything else is contract assemblies, host-project folders, one in-repo plugin, and tests.

## STRUCTURE
```
zmd-charge/
├─ EndfieldCharge.csproj   # ONLY executable (WinExe, net8.0-windows10.0.17763.0); DefaultItemExcludes src/plugins/tests
├─ Program.cs              # entry: single-instance mutex + AppBuilder
├─ App.axaml(.cs)          # composition root (bootstrap order below)
├─ src/
│  ├─ EndfieldCharge.Contracts/          # BCL-only plugin contracts (NO Avalonia, NO NuGet)
│  └─ EndfieldCharge.Contracts.Avalonia/ # skin contracts, DesignTokens, AnimationPrimitives, shared Styles
├─ Host/                   # host FOLDERS (not assemblies): island state machine, plugin registry/loader, battery meta-plugin, menu composer
├─ plugins/EndfieldCharge.Plugin.Music/  # the one in-repo external plugin (built, not type-referenced)
├─ tests/EndfieldCharge.Tests/           # xUnit (pure logic)
├─ Services/ Settings/ Views/ Styles/ Assets/  # more host-project folders (OS P/Invoke, settings UI, windows, theme, icons)
├─ installer/ .github/ .githooks/
└─ reference/ references/ docs/          # vendored refs + design tool — NOT part of the build
```
Note: `Host/`, `Services/`, `Settings/`, `Views/`, `Styles/`, `Assets/` are folders of the root host project, NOT separate assemblies. There are exactly 5 csproj files and no root `.sln`.

## WHERE TO LOOK
| Task | Location | Notes |
|---|---|---|
| App startup / wiring | `Program.cs`, `App.axaml.cs` | bootstrap: settings → registry → battery meta → plugins → HudWindow → tray → power → IPC → debug flags |
| CLI / wake a running instance | `Program.cs`, `Services/HudIpc.cs` | single-instance mutex decided first; `--show` reaches the resident instance over a same-user named pipe (`EndfieldIsland.Hud.Ipc`) |
| Plugin contracts | `src/EndfieldCharge.Contracts`, `src/EndfieldCharge.Contracts.Avalonia` | `IPlugin`, `IIslandSkin`, `IIslandContentProvider`, `IContextMenuContributor`, `IPluginSettingsPage`, `IIslandHost` |
| Plugin discovery/loading | `Host/Plugins/PluginLoader.cs` | scans `plugins/*.dll`, per-plugin ALC; `SharedAssemblies` must list every contract assembly |
| Island 4-state machine | `Host/Island/IslandStateMachine.cs` | Hidden/Response/Waiting/Contracted; pure + tested |
| Built-in battery meta-plugin | `Host/Plugins/Battery/` | `CanUnload=false`, is both skin and content provider |
| HUD window chrome | `Views/HudWindow.axaml(.cs)` | transparent, click-through, NOACTIVATE/TOOLWINDOW, skin rotation, positioning |
| Example plugin | `plugins/EndfieldCharge.Plugin.Music/` | reference for a real plugin: SMTC, lyrics, spectrum, settings page |
| Register a plugin in the build | `EndfieldCharge.csproj` `ProjectReference ReferenceOutputAssembly=false` + `PluginDll` item + copy targets | a new plugin must be added to BOTH lists |
| Settings UI | `Settings/SettingsWindow.axaml(.cs)` | island-anchored drawer; instant save; global styles in `Contracts.Avalonia/Styles` |
| OS integration | `Services/` | PowerWatcher/PowerNative (powrprof), BatteryService (WMI fallback), AutoStart (HKCU), UpdateChecker |
| CI / release | `.github/workflows/build.yml`, `installer/EndfieldCharge.iss` | release is manual only |

## CODE MAP
| Symbol | Type | Location | Role |
|---|---|---|---|
| `Program.Main` | entry | Program.cs:12 | single-instance mutex `Local\EndfieldCharge_SingleInstance_7C1D` |
| `App.OnFrameworkInitializationCompleted` | bootstrap | App.axaml.cs:38 | the composition root |
| `PluginLoader.Load` / `PluginLoadContext` | loader / ALC | Host/Plugins/PluginLoader.cs:31 / :86 | `AssemblyDependencyResolver`; contracts fall back to default ALC |
| `PluginRegistry` | registry | Host/Plugins/PluginRegistry.cs:11 | dedupes by Id; refuses to remove `CanUnload=false` |
| `PluginContext` | context | Host/Plugins/PluginContext.cs | `DataDirectory` = `%APPDATA%\EndfieldCharge\plugins\<id>` |
| `IIslandSkin` | contract | Contracts.Avalonia/IIslandSkin.cs:13 | skin = view + play methods + optional traits |
| `IIslandHost` | contract | Contracts.Avalonia/IIslandHost.cs:10 | current/switch/show skins (host service) |
| `IslandStateMachine` | pure logic | Host/Island/IslandStateMachine.cs:19 | 4-state transitions |
| `IslandWindowMath` | pure logic | Host/Island/IslandWindowMath.cs | window origin + top-edge hover strip math (no Avalonia); shared by positioning and hit test |
| `HudIpc` | service | Services/HudIpc.cs | named-pipe channel; `show` command; `PipeOptions.CurrentUserOnly` |
| `LyricsCache` | plugin helper | plugins/EndfieldCharge.Plugin.Music/LyricsCache.cs | LRU lyrics cache cap; only `^[0-9A-F]{16}\.lrc$` is ever measured or deleted |
| `HudWindow` | Window, `IIslandHost` | Views/HudWindow.axaml.cs:35 | window chrome + skin rotation + host service |
| `BatteryPlugin` | meta-plugin | Host/Plugins/Battery/BatteryPlugin.cs:18 | non-unloadable skin + content |
| `Localization` | strings | src/EndfieldCharge.Contracts/Localization.cs | single zh+en table shared by host AND plugins |
| `DesignTokens` | tokens | Contracts.Avalonia/DesignTokens.cs | colors/metrics/timing (namespace `EndfieldCharge.Host.Island.Animation`) |

## CONVENTIONS (project-specific)
- Commits: Angular exactly, **English-only** text, scopes fixed to `{hud,music,battery,contracts,plugins,menu,power,settings,build,ci,docs}`; body required for every type except `docs`. The hook treats the Angular style as **errors** for AI commits, so run strict mode — `ENDFIELDCHARGE_STRICT_COMMIT=1 git commit ...` (or set `git config endfieldcharge.strictCommit true` once per clone) — and every Angular warning fails the commit instead of passing. Hook `.githooks/commit-msg` (enable once: `git config core.hooksPath .githooks`) — **not** enforced in CI.
- All user-visible text goes through `Localization` (zh + en) — never hardcode display strings in XAML or code-behind.
- Log via `Logger.Info/Warn/Error` (files under `%TEMP%\EndfieldCharge\`), never `Console.WriteLine`.
- UI thread: marshal with `Dispatcher.UIThread`; never `.Result`/`.Wait()`; animations take a `CancellationToken`.
- Avalonia 11: with multiple keyframes `Animation.Easing` is ignored — each segment needs an explicit `KeySpline` (use `AnimationPrimitives`).
- `EndfieldCharge.Contracts` is **BCL-only** (no Avalonia, no NuGet); only `Contracts.Avalonia` may reference Avalonia.
- A plugin may reference ONLY the two contract assemblies (+ Avalonia) — **never** the host `EndfieldCharge.csproj`.
- Icons are Material 24×24 `StreamGeometry` in `Contracts.Avalonia/Styles/Geometries.axaml`; island geometry comes from `IslandMetrics`.
- Formatting: LF + UTF-8; 4-space C#, 2-space XAML/csproj/JSON/YAML; file-scoped namespaces; `_camelCase` for readonly/static private fields.
- `docs/` is the layout design canvas (drag-and-drop island mockups). **Design blueprints are human-authored**: agents must NOT generate `docs/designs/*.json` (or the archived `*-designs.js` templates) — the final designs must come from a human hand. Agents MAY extend the canvas *tool* itself (HTML/CSS/JS under `docs/`) when it lacks a feature or component, or **refine but not redesign** the human-made design. This is a project initiative, not a hard rule — exercise judgement.

## ANTI-PATTERNS (THIS PROJECT)
- A plugin referencing the host project (`EndfieldCharge.csproj`).
- Adding a contract assembly without adding it to `PluginLoader.SharedAssemblies` (→ `plugin is IPlugin` silently false).
- Hardcoding UI strings instead of `Localization`.
- `Console.WriteLine` instead of `Logger`.
- `#pragma warning disable` / `as any`-style casts / empty `catch` (fix the cause).
- A multi-keyframe `Animation` without per-segment `KeySpline`.
- `.Result` / `.Wait()` on the UI thread.
- Committing `bin/`, `obj/`, `publish/`, logs or IDE files; non-English commit messages; bundling unrelated changes; reformatting untouched files.
- Authoring or editing design blueprints (`docs/designs/*.json`, `docs/assets/js/*-designs.js`) — final designs must be human-made. (Extending the canvas *tool* itself or **refine** the human-made design without obvious refactor is allowed.)

## UNIQUE STYLES
- Island pill: background `#312F30`, radius 30, shadow `0 1 6 #40000000`; global scale applies to the whole visual tree.
- The settings panel is a real, **activatable**, borderless window anchored under the island (not an overlay). The island itself is `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`: it never steals focus and is not in Alt+Tab.
- `plugins/` is loaded at runtime; a missing/invalid plugin DLL is skipped, never fatal.
- `docs/` is the island design canvas: the **tool** may be extended by agents, but the **blueprints** (`docs/designs/*.json`) must be human-authored. Not build input.

## COMMANDS
```bash
dotnet build -c Debug                                   # host + contracts + in-repo plugin
dotnet test tests/EndfieldCharge.Tests                  # xUnit (pure logic)
dotnet format EndfieldCharge.csproj --verify-no-changes # CI gate: run on ALL 5 csproj
dotnet publish -c Release -o publish                    # single-file; native dlls land beside the exe
iscc installer/EndfieldCharge.iss                       # requires Inno Setup on PATH
```
CI-equivalent build: `dotnet build -c Release --no-restore /p:TreatWarningsAsErrors=true`.

## NOTES
- A running `EndfieldIsland` locks `bin/` output — stop it before rebuilding.
- `PublishSingleFile` bundles managed DLLs only: SkiaSharp native dlls (`libSkiaSharp`/`libHarfBuzzSharp`/`av_libglesv2`) and `plugins/` MUST ship beside the exe, or it crashes on start.
- Release is manual: `workflow_dispatch` + `publish_release` + running on a `v*` tag (a tag push alone publishes nothing).
- `AssemblyDependencyResolver` needs a `*.deps.json` next to a plugin to resolve private dependencies; the current plugin works because all its deps are in `SharedAssemblies`. Add `EnableDynamicLoading`/`GenerateDependencyFile` if a plugin gains private NuGet deps.
