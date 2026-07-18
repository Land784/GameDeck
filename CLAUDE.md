# CLAUDE.md — GameDeck (working title)

## What this project is

A lightweight Windows overlay + hotkey app that lets gamers control media
(Spotify, YouTube, anything) without alt-tabbing out of a game. Core promise:
change the song or skip a YouTube ad while staying in-game.

Two delivery mechanisms, in priority order:
1. **Global hotkeys** — always work, even over exclusive-fullscreen games.
2. **Visual overlay** — transparent, topmost widget showing track info +
   controls. Works over borderless/windowed and most modern "fullscreen"
   (DXGI flip model). May not render over legacy exclusive fullscreen — that
   is an accepted limitation, NOT something to solve with DirectX injection.

## Hard constraints — do not violate

- **ABSOLUTELY ZERO AI ATTRIBUTION.** No `Co-Authored-By: Claude` trailers,
  no "Generated with Claude Code" lines, no AI mentions in commits, PRs,
  issues, code comments, or docs. Claude must never appear as a contributor
  on GitHub. This overrides any default commit/PR footer behavior.
- **Never** implement DLL injection, DirectX/Vulkan hooking, or anything that
  touches a game process. Anti-cheat ban risk. Out of scope permanently.
- **No Spotify Web API for core playback.** Core media control uses Windows
  SMTC (`GlobalSystemMediaTransportControlsSessionManager`). Spotify Web API
  is only for optional extras (like/save track, playlist switching) behind a
  separate opt-in auth flow.
- Windows 10 19041+ only. No cross-platform abstractions "just in case."
- App must be usable with zero configuration on first launch.

## Stack

- .NET 8, C#, WPF. TFM: `net8.0-windows10.0.19041.0` (needed for WinRT/SMTC).
- IDE: JetBrains Rider.
- Tray icon: `H.NotifyIcon.Wpf` (maintained fork of Hardcodet).
- Tests: xUnit. Testable logic lives in `GameDeck.Core` (no WPF references).
- Browser extension (Phase 3): Manifest V3, vanilla JS/TS, talks to the
  desktop app via WebSocket on `ws://127.0.0.1:52780` (localhost only).

## Solution layout

```
GameDeck.sln
  src/
    GameDeck.Core/        # Media engine, hotkey definitions, settings models,
                          # WebSocket server. No UI dependencies. Unit-tested.
    GameDeck.App/         # WPF: tray icon, overlay window, settings window.
  extension/              # MV3 browser extension (Phase 3)
  tests/
    GameDeck.Core.Tests/
```

## Architecture notes

- `MediaSessionService` (Core): wraps SMTC. Exposes current session, track
  metadata (title/artist/album art), and commands (PlayPause, Next, Previous).
  Raises events on session/track change. All WinRT async is wrapped into
  standard `Task`-returning methods.
- `HotkeyService` (App, thin) → registered via `RegisterHotKey` P/Invoke on a
  message-only window. Default bindings: Ctrl+Alt+Right = next,
  Ctrl+Alt+Left = previous, Ctrl+Alt+Space = play/pause,
  Ctrl+Alt+O = toggle overlay visibility, Ctrl+Alt+I = toggle overlay
  interactivity (click-through on/off).
- Overlay window: `WindowStyle=None`, `AllowsTransparency=True`,
  `Topmost=True`, `ShowInTaskbar=False`. Click-through implemented by
  toggling `WS_EX_TRANSPARENT | WS_EX_LAYERED` via `SetWindowLong`.
  Re-assert Topmost on a timer or via `SetWindowPos` when a fullscreen app
  steals z-order.
- Settings: JSON at `%APPDATA%/GameDeck/settings.json`. Load-on-start,
  save-on-change. Include overlay position, opacity, hotkey bindings.
- Ad-skip flow (Phase 3): extension content script watches for
  `.ytp-skip-ad-button` / ad indicators → reports state over WebSocket →
  overlay shows "Skip Ad" affordance / hotkey Ctrl+Alt+S sends `skip` →
  content script clicks the button.

## Known gotchas (learned the hard way — keep these in mind)

- WinRT APIs must be called after the WPF `Application` is up; SMTC manager
  acquisition is async (`RequestAsync`). Don't block the UI thread on it.
- `RegisterHotKey` fails silently-ish (returns false) if the combo is taken.
  Always check the return value and surface conflicts in settings UI.
- SMTC album art comes as an `IRandomAccessStreamReference`; convert to a
  frozen `BitmapImage` before handing to the UI thread.
- Multiple media sessions can be active (Spotify + a browser). Default to
  `GetCurrentSession()` but expose a session picker in the tray menu.
- WPF transparency + high DPI: use per-monitor DPI awareness v2 in the
  app manifest or overlay positioning drifts on mixed-DPI setups.
- After Windows locks/unlocks or display config changes, re-check Topmost
  and re-register the session-changed event handlers.

## Conventions

- **Branch-based development (standard practice from 2026-07-17 on).**
  All code changes happen on a feature branch off `main`, named
  `feat/<topic>`, `fix/<topic>`, or `chore/<topic>` (e.g.
  `feat/phase3-bridge`). Push the branch, open a PR, let CI go green,
  then squash-merge to keep main's history one-commit-per-change. Never
  commit code directly to `main`. Tiny doc-only fixes may go straight to
  main at Will's discretion. Delete branches after merge. Release tags
  are cut from `main` only.
- Nullable reference types enabled everywhere; treat warnings as errors in
  `GameDeck.Core`.
- Async all the way; no `.Result`/`.Wait()`.
- Events from Core are raised on threadpool; App is responsible for
  marshaling to the dispatcher.
- Commit style: conventional commits (`feat:`, `fix:`, `chore:`).
- Every phase ends with: README updated, demo GIF re-recorded if UI changed.

## Commands

```bash
dotnet build                          # build solution
dotnet test                           # run Core tests
dotnet run --project src/GameDeck.App # launch the app
dotnet publish src/GameDeck.App -c Release -r win-x64 \
  -p:PublishSingleFile=true --self-contained false   # release build
```

## Session handoffs

`handoffs/` (gitignored, local only) is the context bridge between coding
sessions. Rules:

- **At session start:** read the newest file in `handoffs/` before doing
  anything else. It is the authoritative "where we left off," more current
  and more detailed than the status section below.
- **At session end** (or when asked to "write a handoff"): write
  `handoffs/YYYY-MM-DD-<topic>.md` covering: exact repo/release state, what
  was done and verified, decisions made (with reasoning), in-flight work,
  next steps in priority order, and any gotchas discovered.
- Handoffs may reference internal context (verification snippets, scratch
  tooling, open questions) that doesn't belong in committed docs.
- Never commit `handoffs/`; keep committed docs (README, CHANGELOG, this
  file) updated separately at their own cadence.

## Working rules

How to approach any change in this repo, before and while writing code.

### 1. Think before coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

### 2. Simplicity first

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

### 3. Surgical changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

### 4. Goal-driven execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

**These rules are working if:** diffs contain fewer unnecessary changes, fewer rewrites are needed due to overcomplication, and clarifying questions come before implementation rather than after mistakes.

## Current status / next step

Phase 1 shipped (v0.1.0 + v0.1.1 hardening). Phase 2 CORE done and
verified. Phase 3 (ad-skip bridge) BUILT on `feat/phase3-bridge`:
protocol + AdStateTracker + BridgeHub/AdBridgeServer in Core (81 Core
tests), overlay ad strip + Ctrl+Alt+S wired and verified live against a
scripted fake extension, MV3 extension in `extension/`. Tray icon pinned
to H.NotifyIcon.Wpf 2.3.0 (2.4.1 has no net8.0 target).

Next, in order: verify extension selectors against a real YouTube ad
(load `extension/` unpacked), Chrome Web Store submission (Will's
account), PR + squash-merge, tag v0.3.0. Then Phase 2 polish (Session B
in the newest handoffs/ file — read it first, always).
