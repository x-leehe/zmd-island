# Contributing to EndfieldCharge

Thanks for taking the time to contribute. EndfieldCharge is a plugin-driven, four-state
"Dynamic Island" style HUD for Windows: it reacts to power events and hosts plugins that
extend the island with their own skins, content and settings.

Please read this guide before opening an issue or a pull request. It covers how to build
and verify the project, how to write code and commits, and how to add a plugin.

## Table of contents

- [Before you start](#before-you-start)
- [Building and running](#building-and-running)
- [Commit messages](#commit-messages)
- [Pull requests](#pull-requests)
- [Code guidelines](#code-guidelines)
- [AI-assisted contributions](#ai-assisted-contributions)
- [Plugin guidelines](#plugin-guidelines)
- [Project layout](#project-layout)
- [Reporting bugs](#reporting-bugs)
- [Releases](#releases)
- [License](#license)

## Before you start

- Requirements: Windows 10 1809 (build 17763) or later, or Windows 11; x64; .NET 8 SDK.
- The HUD is a layered, click-through, always-on-top window. Many behaviors depend on the
  Windows build and on "scroll inactive windows when I hover" being enabled — mention your
  Windows build when reporting or fixing behavior.
- Prefer opening an issue before writing a lot of code for anything that changes behavior.
- Never commit build output: `bin/`, `obj/`, `publish/`, logs, or IDE files.

## Building and running

```bash
# debug build (host + contracts + in-repo plugins; the plugin DLL is copied to <output>/plugins)
dotnet build -c Debug

# exactly what CI enforces
dotnet restore
dotnet build -c Release --no-restore /p:TreatWarningsAsErrors=true

# unit tests and the formatting baseline (both are CI gates)
dotnet test tests/EndfieldCharge.Tests
dotnet format EndfieldCharge.csproj --verify-no-changes

# release packaging (produces publish/plugins/*.dll next to the single-file exe)
dotnet publish -c Release -o publish
iscc installer/EndfieldCharge.iss        # requires Inno Setup (iscc on PATH)
```

Notes

- The debug output lives in `bin/Debug/net8.0-windows10.0.17763.0/`; the contract assemblies
  are `src/EndfieldCharge.Contracts[.Avalonia]`, and external plugins land in `plugins/`
  next to the executable — that is where the host loads them from at runtime.
- On Windows a running instance locks the output. Stop it before rebuilding:
  `Get-Process EndfieldCharge -ErrorAction SilentlyContinue | Stop-Process -Force`
- Debug flags: `--demo`, `--preview`, `--preview-unplug`, `--debug-ring`, `--power-log`,
  `--show-fps`, `--demo-music` (see the README for the current list and semantics).
- Logs: `%TEMP%\EndfieldCharge\log-YYYYMMDD.txt`. Plugin state lives in
  `%APPDATA%\EndfieldCharge\plugins\<plugin-id>\`.
- Projects: the host, the two contract assemblies, `plugins/*` and `tests/EndfieldCharge.Tests`
  (xUnit). CI runs the tests and verifies the `.editorconfig` formatting baseline, so run
  `dotnet test` and `dotnet format` before pushing.

## Commit messages

This project follows the [Angular commit message convention][angular-commit], which is the
same shape as [Conventional Commits][conventional-commits].

**Commit messages MUST be written in English**, even though other parts of the repository
(comments, docs) may be Chinese. History is the project's changelog for release notes, so it
is kept uniform and machine-readable. Issue and pull-request conversations may be in Chinese
or English — whatever gets the point across.

```
<header>
<blank line>
<body>
<blank line>
<footer>
```

### Header

```
<type>(<scope>): <summary>
```

- **type** — one of:

  | type       | use for                                                        |
  | ---------- | -------------------------------------------------------------- |
  | `feat`     | a new feature for the user                                     |
  | `fix`      | a bug fix for the user                                         |
  | `docs`     | documentation only                                             |
  | `perf`     | a change that improves performance                             |
  | `refactor` | a change that neither fixes a bug nor adds a feature           |
  | `test`     | adding or correcting tests                                     |
  | `build`    | build system, project files or external dependencies           |
  | `ci`       | CI configuration and scripts (`.github/workflows`)             |
  | `chore`    | maintenance that fits none of the above                        |
  | `revert`   | reverting a previous commit                                    |

- **scope** — optional, but when present it must come from this list: `hud`, `template`,
  `battery`, `contracts`, `plugins`, `menu`, `power`, `settings`, `build`, `ci`, `docs`.
  Omit it for changes that span the whole repo; if your change really needs a new scope, say
  so in the pull request.
- **summary** — imperative present tense ("add", not "added" nor "adds"), not capitalized,
  no period at the end, ≤ 72 characters.

### Body

Mandatory for every type except `docs`, and at least a few lines long. Explain **why** the
change is needed and what the previous vs. new behavior is — the diff already shows *what*
changed. Wrap around 72 columns.

### Footer

Optional. Use it for breaking changes, deprecations and issue references:

```
BREAKING CHANGE: <short summary>
<blank line>
<migration instructions>

Closes #123
```

A breaking change can also be flagged by appending `!` after the type/scope
(`feat(contracts)!: ...`). For a revert, start the header with `revert: <header of the
reverted commit>` and put `This reverts commit <SHA>` in the body.

### Examples

```
feat(template): grow the pill during the response animation

The template skin used to jump straight to the tall state, which made the
response animation hard to follow. Animating the pill height through the
shared AnimationPrimitives keeps the motion consistent with the battery
skin, and the settled final frame is written back as the new base value.

Closes #42
```

```
fix(hud): keep island buttons working when the island is clicked

Left click on the island body now hides it, but the in-island controls already
mark the pointer event as handled; document that and cover the cover/toggle
button so tapping a control never dismisses the island.

Refs #57
```

### Enforcing it locally

The repository ships a `commit-msg` hook in `.githooks/`. Enable it once per clone:

```bash
git config core.hooksPath .githooks
```

It hard-fails on a malformed header, non-ASCII text, an over-long header and a trailing
period, and warns when a `feat`/`fix` carries no body. Commit messages are **not** checked in
CI — the hook is the only automated gate, so please turn it on. In an emergency it can be
bypassed with `git commit --no-verify`.

### What not to do

- No Chinese (or any non-ASCII) text in commit messages.
- No "wip", "fixes", "update code" style subjects, and no bundling of unrelated changes —
  split them into separate commits instead.
- Don't reformat or reorder files you did not otherwise touch; it makes review and
  `git blame` harder.

## Pull requests

- One logical change per pull request; keep the diff focused and reviewable.
- Branch from `main`, rebase on `main` before asking for review — don't merge `main` into
  your branch.
- Pull requests are **squash-merged**: the PR title — which must follow the
  [commit convention](#commit-messages) — becomes the commit that lands on `main`. Keep your
  individual commits tidy for reviewers, but don't rely on them for the final history.
- The pull request title must follow the [commit convention](#commit-messages) (it can be
  reused as the commit title when merging).
- Describe what changed and why, plus **how you verified it**: build command, flags used,
  and the relevant lines from `%TEMP%\EndfieldCharge\log-*.txt` (which must not contain
  `ERROR`). Visual changes need a before/after screenshot.
- Behavior or layout changes must update `README.md` (feature table, debug flags) in the
  same pull request.
- CI (`Build Windows Installer`) must be green. Warnings are errors in CI, so build with
  `/p:TreatWarningsAsErrors=true` before pushing.
- Run the unit tests (`dotnet test tests/EndfieldCharge.Tests`). Pure-logic changes (state
  machine, geometry math, localization) must come with tests; UI changes need the manual
  evidence above.

## Code guidelines

- C# / .NET 8, `net8.0-windows10.0.17763.0`, nullable enabled, implicit usings, file-scoped
  namespaces, 4-space indent, `_camelCase` private fields, `PascalCase` public members.
- Match the style of the file you are editing. Do not introduce a second way of doing
  something that already has a pattern in the codebase.
- Never silence the compiler: no `#pragma warning disable`, no `as any`-style casts, no
  swallowing exceptions with empty `catch` blocks. Fix the cause.
- Prefer the existing libraries and the BCL over new dependencies; every new package or
  project must be justified in the pull request.
- All user-visible text goes through `Localization` (`zh` + `en`); never hardcode display
  strings in XAML or code-behind.
- Icons are Material Icons on the 24×24 positive-coordinate grid, exposed as `StreamGeometry`
  resources in `src/EndfieldCharge.Contracts.Avalonia/Styles/Geometries.axaml`, placed in a
  square `Viewbox`. Reuse them instead of hand-drawing new paths.
- Island geometry is reported by skins through `IslandMetrics` (design size, top offset,
  internal scale). The host derives hit-testing, hover and menu anchors from it — do not
  hardcode window-level sizes or assume a specific skin.
- Keep the UI thread for UI: marshal with `Dispatcher.UIThread`, never block with
  `.Result` / `.Wait()`, and make animations cancellable through the passed
  `CancellationToken`.
- Avalonia 11 detail: with multiple keyframes, `Animation.Easing` is ignored — each segment
  needs an explicit `KeySpline`.
- Log through `Logger.Info/Warn/Error` (never `Console.WriteLine`); warnings and errors
  should be actionable from the log file alone.
- Run `dotnet format` (whitespace, line endings and using order are verified in CI) and
  `dotnet test tests/EndfieldCharge.Tests` before pushing.
- Comments and XML docs are Chinese today, but English is acceptable — write whichever you
  can make precise, keep it meaningful, and explain *why* rather than restating the code.

## Plugin guidelines

Plugins are the point of this architecture: the host knows nothing but the contracts.

- A plugin that ships with the app is an **in-repo sub-project**:
  `plugins/EndfieldCharge.Plugin.<Name>/EndfieldCharge.Plugin.<Name>.csproj`. Do not create a
  separate repository for it.
- Community plugins **may** live in their own repository and be distributed as a plain DLL that
  users drop into `plugins/` next to the executable. Only in-repo plugins are built into
  official packages — an out-of-repo plugin owns its releases and has to keep up with the
  contracts on its own.
- A plugin may reference **only the contract assemblies**:
  - `src/EndfieldCharge.Contracts` (BCL-only): `IPlugin`, `IPluginContext`,
    `IIslandContentProvider`, `IslandContentDescriptor`, `IContextMenuContributor`,
    `MenuContribution`, `Localization`, `Logger`.
  - `src/EndfieldCharge.Contracts.Avalonia`: `IIslandSkin`, `IIslandHost`,
    `IIslandExpandToggle`, `IslandMetrics`, design tokens, animation primitives, geometry.

  **Never** reference the host project (`EndfieldCharge.csproj`) — that would break the
  plugin boundary and cannot be loaded in isolation.
- Contract assemblies are shared through the default `AssemblyLoadContext`
  (`PluginLoader.SharedAssemblies`). Each plugin gets its own collectible-free ALC, resolved
  via `AssemblyDependencyResolver`. If you add a contract assembly, add it to that list too —
  otherwise `plugin is IPlugin` silently fails because the type identity is split.
- Implement `IPlugin`: `Id` (stable, lowercase, e.g. `template`), `DisplayName`, `CanUnload`,
  `Initialize(IPluginContext)` (read `DataDirectory` for anything you persist) and
  `Shutdown()` (stop timers, unsubscribe events, dispose what you created).
- Optional capabilities — declare them, the host picks them up without changes:
  - `IIslandSkin` default members: `ParticipatesInWheelSwitch` (`false` = skip this skin when
    the user rotates with the mouse wheel), `UsesHostContent` (`false` = the skin provides its
    own data, e.g. media), `ExpandedTimeoutSeconds` (how long the expanded state may last
    before the host falls back to waiting) and `WindowHeight`.
  - State lifetimes are the host's concern and **do not depend on how a state was entered**:
    every transition into expanded / waiting / contracted arms that state's own timeout. The
    waiting and contracted durations are settings; the expanded one is reported by the skin and
    is floored at 3 s by the host (no state may be exempt from timing out).
  - `IIslandExpandToggle` for an in-island expand/collapse button.
  - `IContextMenuContributor` to add island/tray menu entries (aggregated by
    `Target` → `Section` → `Priority`).
  - `IPluginSettingsPage` to contribute a self-titled settings panel to the settings window.
    Plugins are selected on the island itself (mouse wheel), so the settings page only hosts
    the panels.
  - `IPluginContext.GetService<IIslandHost>()` for `CurrentSkinId`, `WheelSkinIds`,
    `SwitchSkin(id)` and `ShowExpanded(id)` (used to pop the island when your content starts).
- Register the plugin in `EndfieldCharge.csproj`: add a `ProjectReference` with
  `ReferenceOutputAssembly=false` and extend `CopyPluginsToOutput` / `CopyPluginsToPublish`
  so the DLL ends up in `plugins/` for both the debug output and `publish/`. A plugin that
  isn't copied there will not ship and will not load.
- Prefer **event-driven** integration over polling in the style of the in-repo plugins. If
  polling is unavoidable, make the interval a documented constant, keep the work off the UI
  thread and stop when the island is not visible.

## AI-assisted contributions

AI-assisted code is welcome, but you own the result. Before opening a pull request you must
have built with `/p:TreatWarningsAsErrors=true`, actually run the application, and read the
code you are submitting — and the pull request has to say how you verified it. "The model
wrote it" is not a description of behavior.

## Project layout

```
EndfieldCharge.csproj                  host (the only executable project)
src/EndfieldCharge.Contracts/          plugin contracts, BCL-only
src/EndfieldCharge.Contracts.Avalonia/ skin contracts + design tokens + geometry
plugins/EndfieldCharge.Plugin.Template/ in-repo plugin example (empty three-state island)
tests/EndfieldCharge.Tests/            xUnit tests for pure logic (state machine, geometry)
Host/                                  island chrome, plugin registry/loader, menu composer
Services/                              power, battery, auto-start, update check
Settings/, Views/, Styles/, Assets/    settings UI, windows, theme, tray/installer icons
installer/, .github/                   Inno Setup script, CI, PR and issue templates
.editorconfig, .githooks/commit-msg    formatting baseline and commit-message hook
```

See the README for the full tree, the four-state model and the animation notes.

## Reporting bugs

Please include:

- App version (Settings → About) and how you got it (installer or portable).
- Windows edition and build number (`winver`).
- Exact steps to reproduce, and what you expected instead.
- The relevant lines from `%TEMP%\EndfieldCharge\log-YYYYMMDD.txt` (with `ERROR` / `WARN`),
  and whether you ran the app elevated.
- For HUD/animation issues: your display scaling, HUD position setting, and whether the
  island was visible, contracted or hidden.

## Releases

- Only maintainers create tags and publish releases.
- Tags use `v<semver>`. The release is **manual**: Actions → *Build Windows Installer* →
  *Run workflow* → pick the `v*` tag → tick `publish_release`. Pushing a tag alone does not
  publish anything; CI artifacts carry the streaming `0.0.<run_number>` version and never
  reach a release.
- Versioning follows Conventional Commits: `fix` → patch, `feat` → minor,
  `BREAKING CHANGE` → major.
- Packages embed the external plugins: the single-file exe must be distributed together with
  the `plugins/` folder produced by `dotnet publish`.

## License

By contributing, you agree that your contribution is licensed under the MIT License, the same
license as this project.

[angular-commit]: https://github.com/angular/angular/blob/main/contributing-docs/commit-message-guidelines.md
[conventional-commits]: https://www.conventionalcommits.org/en/v1.0.0/
