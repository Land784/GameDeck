# GameDeck

[![CI](https://github.com/Land784/GameDeck/actions/workflows/ci.yml/badge.svg)](https://github.com/Land784/GameDeck/actions/workflows/ci.yml)

**Control your music and skip YouTube ads without ever leaving your game.**

A lightweight Windows overlay and global-hotkey app that lets you control any
media source (Spotify, YouTube, Apple Music) while staying in-game. No
alt-tabbing, no dropped frames.

> 🚧 Early development. **v0.1.0** ships global hotkeys, the media engine,
> and a tray icon. The visual overlay is next (Phase 2). See
> [PLAN.md](PLAN.md) for the full roadmap.

## What works now

- **Global hotkeys** over any game, including exclusive fullscreen:

  | Hotkey | Action |
  |---|---|
  | `Ctrl+Alt+Space` | Play / pause |
  | `Ctrl+Alt+Right` | Next track |
  | `Ctrl+Alt+Left` | Previous track |

- **Tray icon** with a now-playing tooltip, playback menu, media source
  picker (Spotify vs. browser vs. anything else), and an opt-in
  "Start with Windows" setting.
- Works with **any media source** that integrates with Windows SMTC:
  Spotify desktop, YouTube in any browser, Apple Music, and more. No
  accounts or OAuth required.

### Try it

Grab the zip from [Releases](https://github.com/Land784/GameDeck/releases)
(needs the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)),
or build from source:

```bash
dotnet run --project src/GameDeck.App
```

Play something, then hit the hotkeys. No window opens; look for ♪ in the
system tray.

## How it works (planned)

- **Global hotkeys** (`RegisterHotKey`) work over any game, including
  exclusive fullscreen.
- **Transparent overlay** (WPF layered window) shows track info and album
  art over borderless and modern fullscreen games, click-through by default.
- **System media control** via Windows SMTC: one integration covers every
  media app, works offline, and needs no accounts or OAuth.
- **YouTube ad-skip** via a companion browser extension talking to the app
  over a localhost WebSocket.

## Non-goals

- No DirectX/Vulkan hooking or DLL injection, ever (anti-cheat risk).
- No macOS/Linux.
- No full music-client features (search, playlists, queue management).
- No telemetry. No data leaves your machine.

## License

[MIT](LICENSE)
