# GameDeck architecture

A short tour of how the pieces fit together and why they were chosen this
way. For the user-facing overview, see the [README](../README.md).

## Component diagram

```
+------------------------------ Windows -------------------------------+
|                                                                      |
|  Spotify.exe    chrome.exe (YouTube)    Apple Music    ...           |
|      |                |                     |                        |
|      +------- System Media Transport Controls (SMTC) --------+       |
|                                                              |       |
|  +---------------- GameDeck.App (WPF, tray) --------------+  |       |
|  |  TrayController   OverlayWindow   SettingsWindow       |  |       |
|  |  HotkeyHost       KeyboardHookHost (fallback)          |  |       |
|  |        |               |                |              |  |       |
|  |  +-----+---------------+----------------+-----+        |  |       |
|  |  |               GameDeck.Core                |<-------+  |       |
|  |  |  MediaSessionService  (WinRT / SMTC)       |           |       |
|  |  |  HotkeyBinding + HotkeyFallbackMatcher     |           |       |
|  |  |  SettingsService      (JSON, %APPDATA%)    |           |       |
|  |  |  OverlayStateMachine  (show / hide policy) |           |       |
|  |  |  AdBridgeServer       (WebSocket :52780)   |<------+   |       |
|  |  +--------------------------------------------+       |   |       |
|  +------------------------------------------------------ | --+       |
|                                                          |           |
|  chrome.exe -- GameDeck Companion extension (MV3) -------+           |
|    content script (youtube.com) <-> service worker <-> WebSocket     |
+----------------------------------------------------------------------+
```

`GameDeck.Core` holds all the testable logic and has no UI dependencies.
`GameDeck.App` is a thin WPF adapter: it owns the windows, the tray icon,
the Win32 interop, and the marshaling from background threads onto the UI
thread. The browser extension is a separate Manifest V3 package that talks
to the app over a localhost WebSocket.

## Why these choices

- **SMTC over the Spotify Web API.** Windows System Media Transport
  Controls need no OAuth, no Premium account, and no rate limits, they work
  offline, and they are universal: one integration covers Spotify, YouTube,
  Apple Music, and anything else that reports media state to Windows. A
  Spotify Web API integration would only ever cover Spotify, and only for
  logged-in Premium users.
- **WPF over WinUI 3.** Transparent, non-rectangular, always-on-top windows
  are exactly where WinUI 3 is still rough. WPF's `AllowsTransparency` plus
  layered window styles are well proven for this. The tradeoff is that WPF
  is older, which is fine here: this is a systems-integration project, not a
  UI-framework showcase.
- **A browser extension over ad-blocking tricks for YouTube.** Pressing the
  real Skip button as a user action is robust and honest. It does not fight
  YouTube's anti-adblock detection, because it is automating a click the
  user is entitled to make, on the user's explicit command.
- **A localhost WebSocket over Native Messaging.** Native Messaging needs a
  registry-installed host manifest and spawns a process per browser. A
  loopback-only WebSocket is simpler to install, debuggable with any client,
  and the desktop app is already running anyway. The cost is that the app
  has to handle a taken port and authenticate callers (see
  [bridge-protocol.md](bridge-protocol.md)).
- **No game-process injection, ever.** Drawing inside a game the way Discord
  does means hooking the game's swap chain, which anti-cheat systems (EAC,
  BattlEye, Vanguard) can ban for. Global hotkeys give full functional
  coverage even when the overlay cannot draw over a legacy exclusive
  fullscreen game, so that limitation is an acceptable, honest one.

## Hotkeys, and the fullscreen fallback

Hotkeys take two paths, and a small matcher keeps them from firing twice:

1. **`RegisterHotKey` (primary).** `HotkeyHost` registers each bound combo
   on a message-only `HwndSource` and handles `WM_HOTKEY`. This is the
   normal path and works over the desktop and most games.
2. **`WH_KEYBOARD_LL` low-level keyboard hook (fallback).** Some raw-input
   engines in exclusive fullscreen (DOOM Eternal was the reported case)
   swallow registered hotkeys entirely. `KeyboardHookHost` installs an OS
   keyboard hook that matches only the bound combos and enqueues the action.
   This is an operating-system input hook. It never touches the game
   process, so it carries none of the anti-cheat risk that injection would.

When both paths see the same press, `HotkeyFallbackMatcher` (pure logic in
Core, unit-tested) de-duplicates so the action runs exactly once. The hook
callback does nothing but enqueue, and it re-installs itself if Windows
drops the hook.

## Threading model

- **SMTC events** arrive on arbitrary threadpool threads. Core raises plain
  .NET events without touching any UI. `GameDeck.App` marshals them onto the
  UI thread with `Dispatcher.BeginInvoke`. Album art is decoded into a
  frozen `BitmapImage` before it reaches the UI thread.
- **`RegisterHotKey`** needs a window handle and message loop on the thread
  that registered it, so the app uses a hidden message-only window owned by
  the UI thread. `WM_HOTKEY` handling returns immediately and dispatches the
  media command without blocking.
- **The overlay** never decides its own visibility. `OverlayStateMachine`
  (Core) owns the show, fade, and auto-hide policy from timings derived from
  settings, and the window simply applies whatever state it is told. The app
  never calls `Activate()` or `Focus()` on the overlay, which is what keeps
  it click-through and out of the game's way.
- **The bridge** runs on background tasks. Inbound messages are handled off
  the UI thread and any resulting overlay changes are marshaled back like
  everything else.

## Project layout

```
GameDeck.sln
  src/
    GameDeck.Core/     Media engine, hotkey matching, settings, overlay
                       state machine, bridge server. No UI dependencies.
                       Unit-tested (GameDeck.Core.Tests).
    GameDeck.App/      WPF: tray, overlay window, settings window, hotkey
                       host + keyboard-hook fallback, monitor/window interop,
                       crash dialog.
  extension/           Manifest V3 browser extension (content script +
                       service worker) for YouTube ad-skip.
  tests/
    GameDeck.Core.Tests/
  docs/                This file, bridge-protocol.md, testing/, ROADMAP.md.
```

## Related docs

- [Bridge protocol](bridge-protocol.md): the app/extension WebSocket
  message format and the token handshake.
- [Testing matrix](testing/testing-matrix.md): the manual
  game-compatibility results.
