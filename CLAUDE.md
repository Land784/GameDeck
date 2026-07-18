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

## Current status / next step

Phase 1 complete — tagged v0.1.0 (invisible MVP): SMTC media engine with
session picker + debounce in Core (25 unit tests), global hotkeys on a
message-only window, tray icon (H.NotifyIcon.Wpf pinned to 2.3.0 — 2.4.1
has no net8.0 target), single-instance mutex, JSON settings.

Next: Phase 2 — the overlay. OverlayWindow (transparent/topmost/click-through),
z-order guard, auto-hide state machine, interactivity toggle, settings window.
See PLAN.md §6.
