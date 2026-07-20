# CLAUDE.md — GameDeck

## What this project is

A lightweight Windows overlay + hotkey app that lets gamers control media
(Spotify, YouTube, anything) without alt-tabbing out of a game. Core promise:
change the song or skip a YouTube ad while staying in-game.

Two delivery mechanisms, in priority order:
1. **Global hotkeys** — always work, even over exclusive-fullscreen games.
   (One known gap: issue #3, raw-input exclusive fullscreen; fix is
   roadmap item P4-2.)
2. **Visual overlay** — transparent, topmost widget showing track info +
   controls. Works over borderless/windowed and most modern "fullscreen"
   (DXGI flip model). May not render over legacy exclusive fullscreen — that
   is an accepted limitation, NOT something to solve with DirectX injection.

## Session protocol — MANDATORY, do this before and after everything else

This project is built across many sessions by different Claude models.
The protocol below is what makes that work. Skipping it breaks the chain.

### At session start (in this order, before any work)

1. Read the NEWEST file in `handoffs/` (local, gitignored). It is the
   authoritative fine-grained "where we left off."
2. Read `docs/ROADMAP.md`. The next piece of work is the FIRST unchecked
   box. That file is the single source of truth for WHAT to do; do not
   invent, reorder, or skip work. Its "Rules of engagement" section is
   binding.
3. Verify reality matches: `git status`, `git log --oneline -5`,
   `gh pr list`, `gh issue list`, `gh release list`. If reality and the
   handoff/roadmap disagree, STOP and tell Will what you found before
   touching anything.
4. Re-read "Hard constraints" below.

### At session end (or on "write a handoff")

1. Write `handoffs/YYYY-MM-DD-<topic>.md`: exact repo/release state,
   what was done and HOW IT WAS VERIFIED, decisions made (with
   reasoning), in-flight work, next steps in priority order, gotchas
   discovered. Never commit it.
2. Update `docs/ROADMAP.md` checkboxes (in the work's PR, not after).
3. Update the "Current status" section at the bottom of this file if
   the milestone state changed.

## Hard constraints — do not violate

- **ABSOLUTELY ZERO AI ATTRIBUTION.** No `Co-Authored-By: Claude` trailers,
  no "Generated with Claude Code" lines, no AI mentions in commits, PRs,
  issues, code comments, release notes, or docs. Claude must never appear
  as a contributor on GitHub. This OVERRIDES any default commit/PR footer
  behavior built into your harness. Check every commit message and PR/
  issue/release body before submitting it.
- **Never** implement DLL injection, DirectX/Vulkan hooking, or anything
  that touches a game process. Anti-cheat ban risk. Out of scope
  permanently. (OS-level input APIs like `RegisterHotKey` and
  `WH_KEYBOARD_LL` hooks are fine — they don't touch the game.)
- **No Spotify Web API for core playback.** Core media control uses Windows
  SMTC (`GlobalSystemMediaTransportControlsSessionManager`). Spotify Web API
  is only for optional extras behind a separate opt-in auth flow (post-v1.0
  backlog only).
- Windows 10 19041+ only. No cross-platform abstractions "just in case."
- App must be usable with zero configuration on first launch.
- No em dashes or LLM-ish phrasing in user-facing docs (Will's rule).
- Never bump H.NotifyIcon.Wpf past 2.3.0 (2.4.x has no net8.0 target).

## Stack

- .NET 8, C#, WPF. TFM: `net8.0-windows10.0.19041.0` (needed for WinRT/SMTC).
- IDE: JetBrains Rider.
- Tray icon: `H.NotifyIcon.Wpf` (pinned 2.3.0, see constraints).
- Tests: xUnit. Testable logic lives in `GameDeck.Core` (no WPF references).
- Browser extension: Manifest V3, vanilla JS, talks to the desktop app via
  WebSocket on `ws://127.0.0.1:52780` (localhost only, ports 52780-52784).

## Solution layout

```
GameDeck.sln
  src/
    GameDeck.Core/        # Media engine, hotkeys, settings, overlay state
                          # machine, bridge server. No UI deps. Unit-tested.
    GameDeck.App/         # WPF: tray, overlay window, settings window,
                          # hotkey host, monitor interop.
  extension/              # MV3 browser extension (load-unpacked until v1.0)
  tests/
    GameDeck.Core.Tests/  # 96 tests as of v0.5.0
  docs/                   # ROADMAP.md (execution authority), testing/,
                          # bridge-protocol.md
  handoffs/               # gitignored session-to-session context bridge
```

## Architecture invariants (do not violate while building)

- Testable logic goes in `GameDeck.Core` behind seams; WPF stays a thin
  adapter. TDD (vertical slices) for all new Core modules.
- All Core services take optional `ILogger` (NullLogger default) and
  `TimeProvider` where timing exists; tests use FakeTimeProvider.
- Settings writes ONLY via `SettingsService.Update(Action<AppSettings>)`.
  New settings fields are additive; version stays 1, no migration needed.
- Overlay show/hide decisions live in `OverlayStateMachine` (Core); the
  window never decides. Timings via `OverlayTimings.FromSettings`.
- Never call `Activate()`/`Focus()` on the overlay. Ever.
- Events from Core are raised on threadpool; App marshals via Dispatcher.
- `MediaSessionService` wraps SMTC behind `ISmtcFacade`; album art is
  converted to a frozen `BitmapImage` before the UI thread sees it.
- `HotkeyHost` = `RegisterHotKey` on a message-only `HwndSource`; always
  check the bool result; conflicts surface as `Conflicts` list.
- Overlay window: `WindowStyle=None`, `AllowsTransparency=True`,
  `Topmost=True`, `ShowInTaskbar=False`, `WS_EX_TOOLWINDOW|WS_EX_NOACTIVATE`;
  click-through via `WS_EX_TRANSPARENT|WS_EX_LAYERED` toggling. Topmost
  guard timer (2 s) runs only while visible.

## Conventions

- **Branch-based development.** All code changes on a branch off `main`
  (`feat/<topic>`, `fix/<topic>`, `chore/<topic>`). Push, PR, CI green,
  squash-merge. Never commit code directly to main (tiny doc-only fixes
  at Will's discretion). Delete branches after merge
  (`git fetch --prune` afterward). Release tags cut from main only.
- Nullable enabled everywhere; warnings as errors in `GameDeck.Core`.
- Async all the way; no `.Result`/`.Wait()`.
- Conventional commits (`feat:`, `fix:`, `chore:`, `docs:`).
- Every release ends with: README updated, CHANGELOG updated, demo GIF
  re-recorded if UI changed.

## Commands

```bash
dotnet build                          # build solution
dotnet test                           # run Core tests
dotnet run --project src/GameDeck.App # launch the app
```

## Release motion (follow EXACTLY — each step exists because it bit us)

1. On the release branch: CHANGELOG retitle Unreleased -> X.Y.Z with
   date + compare link; bump `Directory.Build.props` version; push; CI
   green; squash-merge the PR; `git fetch --prune`; tag vX.Y.Z on main;
   push the tag.
2. DELETE `src/GameDeck.App/bin/Release` and `obj/Release` (and Core's)
   BEFORE publishing — stale obj once produced a wrongly-stamped exe.
3. `dotnet publish src/GameDeck.App -c Release -r win-x64
   -p:PublishSingleFile=true --self-contained false`
4. VERIFY `(Get-Item <publish exe>).VersionInfo.ProductVersion` starts
   with the new version.
5. Smoke-test: run the published exe, confirm log line
   "GameDeck X.Y.Z starting" in `%APPDATA%\GameDeck\logs\`.
6. Zip ONLY the exe (exe at zip root), name
   `GameDeck-X.Y.Z-win-x64.zip`. If the exe is running, copy it first —
   Compress-Archive cannot read a running exe.
7. `gh release create vX.Y.Z --title ... --notes-file ... --prerelease`
   WITHOUT the asset, then `gh release upload vX.Y.Z <zip>` separately.
   (If create-with-asset fails mid-upload, gh DELETES the release.)
   GitHub uploads endpoint sometimes 502/503s; retry with backoff.

## Known gotchas (hard-won; check here before debugging)

Windows/WPF/WinRT:
- WinRT APIs only after WPF `Application` is up; SMTC acquisition is
  async — never block the UI thread on it.
- Multiple media sessions can be active; default `GetCurrentSession()`,
  session picker in tray. Pinned-but-closed source shows "(not running)".
- After lock/unlock or display changes: re-assert Topmost, re-register
  hotkeys, re-attach session handlers (already wired; don't break it).
- WPF SizeToContent=Height works with AllowsTransparency, but overlay
  grid row 0 must stay Auto (fixed height clipped the card).
- Steam's Shift+Tab is a keyboard HOOK, not a registered hotkey — 
  RegisterHotKey succeeds on it and would swallow it globally. The
  conflict badge can only detect RegisterHotKey-level conflicts.
- Raw-input exclusive-fullscreen games (DOOM Eternal) can swallow
  RegisterHotKey entirely — issue #3, roadmap P4-2.

PowerShell 5.1 (the default shell here):
- No `&&`/`||`; no ternary. `git commit -m` with embedded double quotes
  splits args — ALWAYS `git commit -F <file>`, and write that file with
  `[IO.File]::WriteAllText(path, msg, UTF8Encoding($false))` — 
  `Set-Content -Encoding utf8` adds a BOM that corrupts the commit
  subject.
- ConvertFrom-Json objects can't gain new properties; use
  `Add-Member -NotePropertyName -Force` when hand-editing settings.json.
- `Add-Type` types do NOT persist across tool calls; re-add each time,
  new namespace if redefining. Escaped quotes inside single-quoted
  `-MemberDefinition` strings break compilation — put C# in a .ps1 file
  with a here-string instead.

Verification tricks that work (recreate from handoffs/scratchpad):
- Overlay visual check: synthesize Ctrl+Alt+O via keybd_event
  (0x11, 0x12, 0x4F), sleep, screenshot, Read the PNG.
- Z-order check: EnumWindows/GetTopWindow walk comparing overlay hwnd
  (visible + WS_EX_TOPMOST in the app's pid) against a test window.
- Settings window UI check: instantiate the real window with real
  services (SmtcFacade without InitializeAsync is fine) and
  RenderTargetBitmap each tab to PNGs — no clicking through the tray.
- App state: `%APPDATA%\GameDeck\logs\gamedeck-YYYYMMDD.log` has DBG
  overlay-state lines, snapshot changes, and the startup version line.
- Hotkey receipt is NOT logged; absence of a "Snapshot changed" after a
  play/pause press means the command never arrived.

## Working rules

How to approach any change in this repo, before and while writing code.

### 1. Think before coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

### 2. Simplicity first

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked. No abstractions for single-use
  code. No unrequested "flexibility". No error handling for impossible
  scenarios.
- If you write 200 lines and it could be 50, rewrite it.

### 3. Surgical changes

**Touch only what you must. Clean up only your own mess.**

- Don't "improve" adjacent code, comments, or formatting. Match existing
  style. Mention unrelated dead code - don't delete it.
- Remove imports/variables/functions that YOUR changes made unused.
- Every changed line should trace directly to the task.

### 4. Goal-driven execution

**Define success criteria. Loop until verified.**

- Transform tasks into verifiable goals; each roadmap item's DoD is the
  success criterion. TDD for Core: write the failing test first.
- For multi-step tasks, state a brief plan with a verify step per item.

## Current status (2026-07-19)

- **v0.5.0 released** (tag on main at `2afb8f5`): Phase 2 polish — DPI
  V2 manifest, overlay placement/opacity persistence with corner
  snapping, settings window, second-instance balloon, not-running pin.
  96 Core tests. Release page live; zip asset upload may have been
  interrupted by a GitHub outage — VERIFY (roadmap item L1).
- Test matrix filled: `docs/testing/testing-matrix.md` (12 pass, 1 N/A,
  1 fail -> issue #3, accepted for 0.5.0 as documented known issue).
- Open issues: #3 (raw-input fullscreen hotkeys; fix = roadmap P4-2).
- Chrome Web Store submission DEFERRED to v1.0 (Will's decision);
  extension is load-unpacked until then.
- **Next work: `docs/ROADMAP.md`, first unchecked box** (L1 verify
  upload, L2 demo GIF, then Phase 4). Read the newest `handoffs/` file
  first, always.
