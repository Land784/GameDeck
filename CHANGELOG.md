# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

Phase 2 — the transparent in-game overlay (track card, click-through,
auto-hide) and the settings window.

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

[Unreleased]: https://github.com/Land784/GameDeck/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/Land784/GameDeck/releases/tag/v0.1.0
