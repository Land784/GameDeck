# GameDeck

[![CI](https://github.com/Land784/GameDeck/actions/workflows/ci.yml/badge.svg)](https://github.com/Land784/GameDeck/actions/workflows/ci.yml)

**Control your music and skip YouTube ads without ever leaving your game.**

<!-- HERO GIF SLOT (roadmap V1-2b): replace this comment with
     ![GameDeck demo](docs/media/hero.gif)
     once the demo GIF is recorded. Keep it under 10 MB so it embeds. -->
_Demo GIF coming soon: track change by hotkey and the overlay card fading
in over a real game._

GameDeck is a lightweight Windows tray app that puts music and YouTube
ad-skip on global hotkeys and a small in-game overlay, so you never have to
alt-tab out of a fullscreen game to change a song.

- **Global hotkeys** control any media source (Spotify, YouTube, Apple
  Music, anything) and keep working even over exclusive-fullscreen games.
- **A translucent overlay** shows the current track and fades itself out,
  click-through by default so your game never loses input.
- **YouTube ad-skip from in-game**, with a small companion browser
  extension, on a single keypress.

No accounts, no OAuth, no telemetry. Nothing leaves your machine.

> Early development. The app is usable today (media engine, global hotkeys,
> overlay, YouTube ad-skip, settings window), and the pieces below all work.
> Expect rough edges until the v1.0 tag.

## Install

1. Download the latest `GameDeck-x.y.z-win-x64.zip` from the
   [Releases page](https://github.com/Land784/GameDeck/releases).
2. Unzip it anywhere and run `GameDeck.exe`. The release build is
   self-contained, so you do not need to install a .NET runtime or anything
   else.
3. No window opens. GameDeck lives in the system tray (look for its icon
   near the clock). Play something and try `Ctrl+Alt+O`.

### SmartScreen

GameDeck is a new app without a paid code-signing certificate yet, so
Windows SmartScreen may warn you ("Windows protected your PC") the first
time you run it. This is expected
for small open-source tools. To run it:

1. Click **More info** on the blue SmartScreen dialog.
2. Click **Run anyway**.

<!-- SCREENSHOT SLOT (roadmap P4-6): replace this comment with
     ![SmartScreen: More info then Run anyway](docs/media/smartscreen.png)
     once the screenshot is captured. -->

You only see this once per downloaded build. If you would rather not, you
can [build from source](#build-from-source) instead.

> A `winget` package is planned for the v1.0 release, which will let you
> install with a single `winget install` command and skip the SmartScreen
> prompt.

## Hotkeys

All hotkeys are rebindable in the settings window, and conflicts with other
apps are flagged there.

| Hotkey | Action |
|---|---|
| `Ctrl+Alt+Space` | Play / pause |
| `Ctrl+Alt+Right` | Next track |
| `Ctrl+Alt+Left` | Previous track |
| `Ctrl+Alt+O` | Show / hide the overlay |
| `Ctrl+Alt+I` | Make the overlay clickable (drag it, click controls) |
| `Ctrl+Alt+S` | Skip the current YouTube ad |

## What you get

- **Global hotkeys** over any game, including exclusive fullscreen. Raw-input
  engines that used to swallow registered hotkeys (DOOM Eternal was the
  reported case) are now covered by a low-level keyboard-hook fallback. See
  the [FAQ](#faq) for the details.
- **In-game overlay**: a small translucent card (album art, title, artist,
  progress) that fades in on track changes and hides itself after a few
  seconds. Click-through by default, so your game never loses input. Works
  over borderless and most modern fullscreen games. Drag it where you want
  it (`Ctrl+Alt+I`) and it stays there, per monitor.
- **Settings window** (right-click the tray icon): overlay opacity,
  auto-hide delay, corner presets, hotkey rebinding with conflict warnings,
  media source pinning, and the extension token.
- **YouTube ad-skip**: with the companion extension installed, the overlay
  shows a strip when an ad is playing in a background YouTube tab (amber
  while unskippable, green when the skip button is up), and `Ctrl+Alt+S`
  clicks skip for you. You hear music again without touching the browser.
- **Tray icon** with a now-playing tooltip, playback menu, media source
  picker (Spotify vs. browser vs. anything else), and an opt-in "Start with
  Windows" setting.
- Works with **any media source** that integrates with Windows SMTC:
  Spotify desktop, YouTube in any browser, Apple Music, and more, with no
  accounts or OAuth.

### Setting up ad-skip

1. Install the GameDeck Companion extension. The Chrome Web Store listing is
   pending review; until then, load the `extension/` folder unpacked via
   `chrome://extensions` with Developer mode turned on.
2. Right-click the GameDeck tray icon and pick "Copy extension token".
3. Open the extension's options page, paste the token, and hit Save. The
   status dot turns green when it finds the app.

## How it works

- **Global hotkeys** (`RegisterHotKey`) work over any game. When a raw-input
  engine swallows them in exclusive fullscreen, a `WH_KEYBOARD_LL` keyboard
  hook picks up the same combos as a fallback. Both are OS-level input APIs
  that never touch the game process.
- **The overlay** is a transparent WPF layered window that shows track info
  and album art over borderless and modern (DXGI flip model) fullscreen
  games, click-through by default. Legacy exclusive-fullscreen games can
  hide it; the hotkeys still work.
- **Media control** goes through Windows SMTC
  (`GlobalSystemMediaTransportControlsSessionManager`), so one integration
  covers every media app, works offline, and needs no accounts.
- **YouTube ad-skip** runs through a companion browser extension that talks
  to the app over a WebSocket bound to `127.0.0.1`.

For the full picture (component diagram, threading model, and the reasoning
behind each choice), see [docs/architecture.md](docs/architecture.md). The
app/extension message format is in
[docs/bridge-protocol.md](docs/bridge-protocol.md).

## Security model, honestly

The extension and app talk over a WebSocket bound to `127.0.0.1` only, so
nothing ever leaves your machine. A token you copy from the tray and paste
into the extension stops other local programs from feeding the app fake ad
state. On skip, the extension presses the same skip button you would press
(and if YouTube ignores the synthetic press, it jumps to the end of the ad
instead, but only while the skip button is showing). It never touches your
mouse, keyboard, focus, or the game. No telemetry on either side.

## FAQ

**Will this get me banned by anti-cheat?** GameDeck never injects into or
reads from a game process. It uses ordinary OS input APIs (`RegisterHotKey`
and a keyboard hook) and draws a normal top-most window, the same category
of thing Discord and Steam notifications already do. It does not hook the
game's rendering. See the [non-goals](#non-goals).

**The overlay does not show over my fullscreen game.** Some older games use
true exclusive fullscreen, where no other window can draw on top. The
overlay cannot appear there, and that is a deliberate limitation (the fix
would be swap-chain injection, which risks anti-cheat bans). The hotkeys
still work, so you can change tracks blind. If you can switch the game to
borderless or windowed fullscreen, the overlay will appear.

**My hotkeys did nothing in a fullscreen shooter.** Earlier builds could
miss hotkeys in raw-input exclusive fullscreen (this was tracked as
[issue #3](https://github.com/Land784/GameDeck/issues/3), reported against
DOOM Eternal). A low-level keyboard-hook fallback now covers that case, so
the hotkeys fire even when the game swallows the registered ones. If you
still hit a game that eats them, please open an issue.

**Windows warned me the app is unrecognized.** That is SmartScreen on an
unsigned new app. See [Install](#smartscreen) for how to run it.

**Does it need Spotify Premium or a login?** No. It drives whatever Windows
reports as the current media session, so it works with the free tier and
with any other player.

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet build                            # build the solution
dotnet test                             # run the Core unit tests
dotnet run --project src/GameDeck.App   # launch the app
```

## Contributing

Bug reports and feature requests are welcome as
[GitHub issues](https://github.com/Land784/GameDeck/issues). If you want to
send a change:

- Work on a branch off `main` (`feat/`, `fix/`, or `chore/`), and open a
  pull request. CI (build plus the Core unit tests) must pass.
- Keep logic that can be unit-tested in `GameDeck.Core`, which has no UI
  dependencies, and add tests for it. The WPF layer stays a thin adapter.
- Please do not add DirectX/Vulkan hooking, DLL injection, or anything that
  touches a game process. That is out of scope permanently (see below).

## Non-goals

- No DirectX/Vulkan/OpenGL hooking or DLL injection, ever (anti-cheat risk).
- No macOS or Linux. This is a Windows 10 (19041+) and Windows 11 app.
- No full music-client features (search, playlists, queue management).
- No telemetry. No data leaves your machine.

## License

[MIT](LICENSE)
