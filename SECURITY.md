# Security

## Reporting a vulnerability

If you find a security issue in GameDeck, please report it privately rather
than opening a public issue. Use GitHub's
[private vulnerability reporting](https://github.com/Land784/GameDeck/security/advisories/new)
(Security tab, "Report a vulnerability"), or email the maintainer at the
address on the GitHub profile.

Please include what you found, how to reproduce it, and the version you were
running. You can expect an acknowledgement within a few days. There is no
paid bounty; this is a personal open-source project.

## Scope and design notes

GameDeck runs entirely on the local machine and makes no outbound network
connections.

- **Local bridge.** The desktop app runs a WebSocket server bound to
  `127.0.0.1` (ports 52780-52784) for the companion browser extension. It is
  not reachable from the network. Connections must present a per-user token
  (generated with a cryptographic RNG and shown in the tray menu) before any
  action is accepted; unauthenticated connections are closed. Browser-page
  origins are rejected at the handshake, so only the extension (or a local
  non-browser client that holds the token) can connect.
- **Input.** Global hotkeys use the OS APIs `RegisterHotKey` and a
  `WH_KEYBOARD_LL` low-level keyboard hook. GameDeck never injects into or
  reads memory from any game process.
- **Privacy.** No telemetry, no analytics, no update checks. Nothing leaves
  your machine.

## Supported versions

The latest release receives security fixes. Older pre-1.0 releases do not.
