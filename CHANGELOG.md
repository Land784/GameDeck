# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

Ships as v0.3.0 once the ad-skip flow is verified against a live YouTube
ad. Contains the Phase 2 overlay core and the Phase 3 ad-skip bridge.

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

[Unreleased]: https://github.com/Land784/GameDeck/compare/v0.1.1...HEAD
[0.1.1]: https://github.com/Land784/GameDeck/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/Land784/GameDeck/releases/tag/v0.1.0
