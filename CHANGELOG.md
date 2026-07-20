# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.9.0] - 2026-07-20

The Phase 4 ship-quality release: hotkeys now reach the app even in
raw-input exclusive fullscreen, plus a real app icon, first-run
onboarding, a crash dialog, and docs written for people who did not build
it.

### Added

- First-run onboarding. On the very first launch, a tray balloon points at
  the overlay hotkey and the settings menu, and the overlay pops briefly
  showing the current track (or a friendly "Play something and I'll show up
  here" when nothing is playing). It shows once and never repeats.
- A designed app icon across the executable, the tray, the taskbar and
  Alt-Tab (settings window), and the notification balloon, legible down to
  16 px.
- A crash dialog. If GameDeck hits an unhandled error it now shows a short
  "GameDeck hit a problem and closed" dialog with an "Open logs folder"
  button, instead of only writing to the log and disappearing.
- `docs/architecture.md`: component diagram, the reasoning behind each
  design choice, the hotkey fallback, the threading model, and the project
  layout.

### Changed

- Release builds are now self-contained single-file executables, produced
  by a tag-triggered CI workflow. You no longer need to install a .NET
  runtime to run a downloaded build.
- The README is rewritten for newcomers: install-first, with SmartScreen
  guidance, a hotkey table, an FAQ (including the exclusive-fullscreen
  overlay limitation), the security model, and contributing notes.

### Fixed

- Global hotkeys now reach the app even when a raw-input title swallows
  registered hotkeys in exclusive fullscreen (seen with DOOM Eternal), via
  a low-level keyboard-hook fallback that matches the same combos and fires
  once. This resolves
  [#3](https://github.com/Land784/GameDeck/issues/3); windowed and
  borderless were never affected.

## [0.5.0] - 2026-07-19

The Phase 2 polish release: the overlay remembers its placement, a
settings window, and DPI/single-instance hardening. Verified against the
manual game-compatibility matrix in docs/testing/testing-matrix.md.

### Added

- Settings window (tray menu, "Settings"): overlay opacity with live
  preview, auto-hide delay (or always visible), corner presets, hotkey
  rebinding with a key recorder and inline conflict warnings, media
  source pinning, bridge token and extension connection status, start
  with Windows, and an animations toggle.
- The overlay remembers where you put it. Drag it in interactive mode
  (Ctrl+Alt+I) and it snaps to a corner when close, then restores there
  on the next launch, per monitor. If the monitor is gone, it falls back
  to the primary top right.
- Per-monitor DPI awareness (V2), so the overlay lands where it should
  on mixed-DPI setups.
- Launching GameDeck while it is already running now shows a balloon
  pointing at the tray icon instead of silently doing nothing.
- A pinned media source that is not running shows grayed in the pickers
  as "name (not running)" instead of disappearing.

### Known issues

- Global hotkeys do not reach the app while some raw-input titles run in
  exclusive fullscreen (seen with DOOM Eternal; windowed and borderless
  are unaffected). Tracked in
  [#3](https://github.com/Land784/GameDeck/issues/3); fix planned for
  the next phase.

## [0.3.0] - 2026-07-18

The Phase 2 overlay core and the Phase 3 ad-skip bridge, verified against
live YouTube ads. The browser extension ships in-repo; the Chrome Web
Store listing is submitted separately and takes days to review.

### Added

- In-game overlay: translucent now-playing card (album art, title, artist,
  progress bar) that fades in on track changes and auto-hides after 4
  seconds. Click-through by default. `Ctrl+Alt+O` toggles visibility;
  `Ctrl+Alt+I` makes it clickable for dragging, with an Esc/30-second
  auto-revert failsafe and a tray "Reset overlay" item.
- YouTube ad-skip: the GameDeck Companion browser extension (in
  `extension/`, Manifest V3) watches YouTube tabs for ads and reports to
  the app over a localhost-only WebSocket. The overlay shows an amber
  strip while an ad plays and a green one once it is skippable;
  `Ctrl+Alt+S` skips it without leaving the game.
- Token handshake between app and extension: copy it from the tray menu
  ("Copy extension token") into the extension options page. Connections
  without the token are dropped.
- Bridge protocol documented in `docs/bridge-protocol.md`.

### Changed

- The overlay card grows a bottom strip only while an ad is active; the
  ad keeps the overlay visible until the ad ends.
- Skipping is resilient to YouTube ignoring synthetic clicks: the
  extension sends a full pointer event sequence and, if the ad survives
  with the skip button still up, jumps to the end of the ad instead
  (only ever while the ad is already skippable).

## [0.1.1] - 2026-07-17

Hardening pass from a design review of Phase 1. No new features; this is
the build meant for daily use while Phase 2 is developed.

### Added

- File logging to `%APPDATA%\GameDeck\logs` (7-day rolling retention).
  Unhandled exceptions, media command failures, hotkey conflicts, and
  session changes are now recorded. No data leaves your machine.
- Hotkeys re-register and the media session re-attaches automatically
  after the workstation is unlocked, so both survive lock/unlock cycles.
- Playback progress is now exposed separately from track metadata and is
  interpolated locally between the coarse updates some apps emit, ready
  for the Phase 2 overlay progress bar.

### Changed

- Track-change notifications no longer fire on playback-position ticks,
  only on meaningful changes (title, artist, art, play state, source).
- "Start with Windows" is stored only in the registry Run key instead of
  being mirrored into settings.json, removing a source of drift.
- The single-instance guard is now per-user-session, so a second user on
  the same machine can run their own instance.

### Fixed

- Media session wrappers for closed sessions are now released instead of
  accumulating for the lifetime of the process.

## [0.1.0] - 2026-07-17

The "invisible MVP": full media control without a visible window.

### Added

- Media engine over Windows SMTC: works with Spotify, browsers, Apple
  Music, and any other SMTC-integrated source; no accounts or API keys.
- Global hotkeys via `RegisterHotKey` (work over fullscreen games):
  `Ctrl+Alt+Space` play/pause, `Ctrl+Alt+Right` next, `Ctrl+Alt+Left`
  previous. Conflicts with other apps are detected and surfaced.
- Tray icon with now-playing tooltip, playback menu, media-source picker,
  and opt-in "Start with Windows" (HKCU Run key, no admin).
- Single-instance guard; settings persisted to
  `%APPDATA%\GameDeck\settings.json` with atomic saves.

[0.9.0]: https://github.com/Land784/GameDeck/compare/v0.5.0...v0.9.0
[0.5.0]: https://github.com/Land784/GameDeck/compare/v0.3.0...v0.5.0
[0.3.0]: https://github.com/Land784/GameDeck/compare/v0.1.1...v0.3.0
[0.1.1]: https://github.com/Land784/GameDeck/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/Land784/GameDeck/releases/tag/v0.1.0
