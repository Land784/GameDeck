# GameDeck

**Control your music and skip YouTube ads without ever leaving your game.**

A lightweight Windows overlay + global-hotkey app that lets you control any
media source (Spotify, YouTube, Apple Music, …) while staying in-game — no
alt-tabbing, no dropped frames.

> 🚧 Early development — Phase 0 (scaffolding). See [PLAN.md](PLAN.md) for the
> full roadmap.

## How it works (planned)

- **Global hotkeys** (`RegisterHotKey`) — work over any game, including
  exclusive fullscreen.
- **Transparent overlay** (WPF layered window) — track info + album art over
  borderless and modern fullscreen games, click-through by default.
- **System media control** via Windows SMTC — one integration covers every
  media app, no OAuth, works offline.
- **YouTube ad-skip** via a companion browser extension talking to the app
  over a localhost WebSocket.

## Non-goals

- No DirectX/Vulkan hooking or DLL injection, ever (anti-cheat risk).
- No macOS/Linux.
- No full music-client features (search, playlists, queue management).
- No telemetry — no data leaves your machine.

## License

[MIT](LICENSE)
