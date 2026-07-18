# GameDeck bridge protocol v1

The desktop app and the GameDeck Companion browser extension talk over a
WebSocket on localhost. The app listens on `http://127.0.0.1:{port}/bridge/`
where `{port}` is the first free port of `52780..52784`; the extension tries
the same ports in the same order. The socket never leaves the machine: the
listener binds 127.0.0.1 only.

All frames are JSON text and carry `"v": 1`. Unknown or malformed frames are
ignored by the app (never a crash, never a reply).

## Authentication

The app generates a token (GUID) on first launch and stores it in
`%APPDATA%\GameDeck\settings.json`. The user copies it from the tray menu
("Copy extension token") into the extension's options page. The first frame
on a new socket must be a `hello` carrying that token; anything else closes
the socket.

## Frames: extension to app

```jsonc
{ "v":1, "type":"hello", "client":"extension", "ext":"1.0.0", "token":"<token>" }
{ "v":1, "type":"adState", "tabId":123, "adActive":true,
  "skippable":false, "secondsUntilSkippable":4 }
{ "v":1, "type":"adState", "tabId":123, "adActive":false }
{ "v":1, "type":"pong" }
```

`adState` is per tab. `skippable` and `secondsUntilSkippable` are optional
and default to `false` / absent. `adActive: false` clears the tab's ad.

## Frames: app to extension

```jsonc
{ "v":1, "type":"helloAck" }
{ "v":1, "type":"skip", "tabId":123 }
{ "v":1, "type":"ping" }
```

`skip` names the tab whose ad the user asked to skip; the extension clicks
the player's skip button in that tab, then reports fresh `adState`.

## Liveness

The app pings every 15 seconds and drops a connection that has sent nothing
(pong or otherwise) for 45 seconds. The extension reconnects with exponential
backoff (1 s doubling to a 30 s cap) and re-reports ad state for every
YouTube tab after each successful hello, so an app restart cannot leave the
overlay showing stale state.

## Multiple ads

The app tracks ad state per connection and tab and surfaces the most
recently reported active ad. When that ad ends (or its browser disconnects),
the next still-active ad takes over; when none remain, the overlay strip
disappears.
