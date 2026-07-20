# GameDeck

[![CI](https://github.com/Land784/GameDeck/actions/workflows/ci.yml/badge.svg)](https://github.com/Land784/GameDeck/actions/workflows/ci.yml)

**Control your music and skip YouTube ads without ever leaving your game.**

A lightweight Windows overlay and global-hotkey app that lets you control any
media source (Spotify, YouTube, Apple Music) while staying in-game. No
alt-tabbing, no dropped frames.

> 🚧 Early development. Released builds ship the media engine, global
> hotkeys, the in-game overlay, YouTube ad-skip, and a settings window.
> See [PLAN.md](PLAN.md) for the full roadmap.

## What works now

- **Global hotkeys** over any game, including exclusive fullscreen (one
  known exception so far:
  [#3](https://github.com/Land784/GameDeck/issues/3)):

  | Hotkey | Action |
  |---|---|
  | `Ctrl+Alt+Space` | Play / pause |
  | `Ctrl+Alt+Right` | Next track |
  | `Ctrl+Alt+Left` | Previous track |
  | `Ctrl+Alt+O` | Show / hide the overlay |
  | `Ctrl+Alt+I` | Make the overlay clickable (drag it, click controls) |
  | `Ctrl+Alt+S` | Skip the current YouTube ad |

- **In-game overlay**: a small translucent card (album art, title, artist,
  progress) that fades in on track changes and hides itself after a few
  seconds. Click-through by default, so your game never loses input. Works
  over borderless and most modern fullscreen games. Drag it where you want
  it (Ctrl+Alt+I) and it stays there, per monitor.
- **Settings window** (right-click the tray icon): overlay opacity,
  auto-hide delay, corner presets, hotkey rebinding with conflict
  warnings, media source pinning, and the extension token.
- **YouTube ad-skip**: with the companion browser extension installed, the
  overlay shows a strip when an ad is playing in a background YouTube tab
  (amber while unskippable, green when the skip button is up), and
  `Ctrl+Alt+S` clicks skip for you. You hear music again without touching
  the browser.
- **Tray icon** with a now-playing tooltip, playback menu, media source
  picker (Spotify vs. browser vs. anything else), and an opt-in
  "Start with Windows" setting.
- Works with **any media source** that integrates with Windows SMTC:
  Spotify desktop, YouTube in any browser, Apple Music, and more. No
  accounts or OAuth required.

### Setting up ad-skip

1. Install the GameDeck Companion extension (Chrome Web Store listing
   pending review; until then load the `extension/` folder unpacked via
   `chrome://extensions` with Developer mode on).
2. Right-click the GameDeck tray icon and pick "Copy extension token".
3. Open the extension's options page, paste the token, hit Save. The
   status dot turns green when it finds the app.

**Security model, honestly:** the extension and app talk over a WebSocket
bound to 127.0.0.1 only; nothing ever leaves your machine. The pasted token
stops other local programs from feeding the app fake ad state. On skip, the
extension presses the same skip button you would press (and if YouTube
ignores the synthetic press, it jumps to the end of the ad instead, but
only while the skip button is showing). It never touches your mouse,
keyboard, focus, or the game. No telemetry on either side.

### Try it

Grab the zip from [Releases](https://github.com/Land784/GameDeck/releases)
(needs the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)),
or build from source:

```bash
dotnet run --project src/GameDeck.App
```

Play something, then hit the hotkeys. No window opens; look for ♪ in the
system tray.

## How it works

- **Global hotkeys** (`RegisterHotKey`) work over any game, including
  exclusive fullscreen. Known exception: some raw-input engines swallow
  registered hotkeys in exclusive fullscreen (seen with DOOM Eternal;
  windowed/borderless is fine) — tracked in
  [#3](https://github.com/Land784/GameDeck/issues/3).
- **Transparent overlay** (WPF layered window) shows track info and album
  art over borderless and modern fullscreen games, click-through by default.
  Legacy exclusive-fullscreen games can hide it; the hotkeys still work.
- **System media control** via Windows SMTC: one integration covers every
  media app, works offline, and needs no accounts or OAuth.
- **YouTube ad-skip** via a companion browser extension talking to the app
  over a localhost WebSocket. Protocol details in
  [docs/bridge-protocol.md](docs/bridge-protocol.md).

## Non-goals

- No DirectX/Vulkan hooking or DLL injection, ever (anti-cheat risk).
- No macOS/Linux.
- No full music-client features (search, playlists, queue management).
- No telemetry. No data leaves your machine.

## License

[MIT](LICENSE)
