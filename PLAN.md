# GameDeck — Full Development Plan (Deep Version)

Working title: **GameDeck** (rename freely — check name availability on
GitHub, winget, and the Chrome Web Store before v1.0).

---

## 1. Vision & success criteria

**One-sentence pitch:** Control your music and skip YouTube ads without ever
leaving your game.

**The problem, precisely:** When a game holds the foreground (especially
fullscreen without a borderless option), changing a song or skipping a
YouTube ad requires alt-tabbing, which can minimize the game, drop frames,
break capture, or in the worst cases crash fragile titles. Media keys help
but many keyboards lack them, they can't skip YouTube ads, and they give no
visual feedback about what's playing.

**Definition of done for v1.0:**
1. A user can play/pause, skip, and go back on any media source
   (Spotify desktop, YouTube in a browser, Apple Music, etc.) via global
   hotkeys that work over any game, including exclusive fullscreen.
2. A transparent overlay shows track metadata + album art over borderless
   and DXGI-flip "fullscreen" games, with click-through by default.
3. With the companion extension installed, a YouTube ad can be skipped
   from in-game the moment it becomes skippable.
4. Installable by a stranger in under 2 minutes from GitHub Releases or
   winget, with zero configuration required.

**Resume success criteria (be explicit about these — they shape scope):**
- Public repo with a hero demo GIF, architecture diagram, CI badge.
- Measurable adoption: GitHub stars + release download counts + (optional)
  Chrome Web Store install count.
- A story you can tell in an interview: a small distributed system
  (desktop app ⇄ localhost WebSocket ⇄ browser extension), Win32 interop,
  WinRT async, and honest engineering tradeoffs (why NOT DirectX injection).

**Non-goals (write these in the README too):**
- No DirectX/Vulkan/OpenGL hooking or DLL injection, ever (anti-cheat risk).
- No macOS/Linux.
- No full music-client features (search, playlists, queue management).
- No telemetry. "No data leaves your machine" is a feature.

---

## 2. Product specification

### 2.1 Feature list (v1.0)

| # | Feature | Mechanism | Phase |
|---|---------|-----------|-------|
| F1 | Play/pause, next, previous for any media source | SMTC | 1 |
| F2 | Global hotkeys (rebindable) | RegisterHotKey | 1 |
| F3 | Tray icon w/ now-playing tooltip + menu | H.NotifyIcon | 1 |
| F4 | Launch on startup (opt-in) | HKCU Run key | 1 |
| F5 | Transparent topmost overlay: art, title, artist, controls, progress | WPF layered window | 2 |
| F6 | Click-through by default; hotkey toggles interactivity | WS_EX_TRANSPARENT | 2 |
| F7 | Auto-hide: fade in on track change/hotkey, fade out after N s | WPF animations | 2 |
| F8 | Drag to reposition (when interactive); position persisted | settings.json | 2 |
| F9 | Settings UI: hotkeys, opacity, corner snap, auto-hide timing, session picker | WPF window | 2 |
| F10 | YouTube ad detection + one-press skip | MV3 extension + WebSocket | 3 |
| F11 | Multi-session picker (Spotify vs browser vs game audio) | SMTC sessions list | 2 |
| F12 | Crash-safe logging to %APPDATA%\GameDeck\logs | Serilog | 4 |

### 2.2 Default hotkeys

Chosen to avoid common in-game binds (WASD-adjacent, F-keys) and known OS
combos. All rebindable; all conflicts surfaced in Settings with a red badge.

- `Ctrl+Alt+Space` — play/pause
- `Ctrl+Alt+Right` — next track
- `Ctrl+Alt+Left` — previous track
- `Ctrl+Alt+Up` / `Ctrl+Alt+Down` — system volume up/down (optional toggle)
- `Ctrl+Alt+O` — show/hide overlay
- `Ctrl+Alt+I` — toggle overlay interactivity (click-through on/off)
- `Ctrl+Alt+S` — skip YouTube ad (only active when extension reports an ad)

### 2.3 UX flows

**First run:** App starts to tray → toast/balloon: "GameDeck is running.
Ctrl+Alt+O shows the overlay. Right-click the tray icon for settings." →
overlay appears for 5 s in the top-right corner showing whatever is playing
(or a friendly "Play something and I'll show up here").

**In-game happy path:** Song ends, user hits Ctrl+Alt+Right. Overlay fades
in for 4 s showing the new track, fades out. User never touched the mouse.

**Ad-skip path:** YouTube ad starts → extension reports `adState` →
overlay fades in with an amber "Ad playing — Ctrl+Alt+S to skip when ready"
strip → becomes green "Skippable now" → user presses Ctrl+Alt+S → extension
clicks skip → overlay fades back to now-playing.

**Interactivity toggle:** Ctrl+Alt+I → overlay gets a subtle border glow +
becomes clickable/draggable; Esc or Ctrl+Alt+I again returns to
click-through. Never leave it interactive silently — a stuck invisible
click-eating window is the worst bug this app can have.

### 2.4 Visual design brief (Phase 2)

- One compact card, default 320×96 px @ 96 DPI, top-right with 16 px margin.
- Dark glassy card (~85% opacity black, 12 px corner radius), white text,
  album art 64×64 left, title (semibold, truncate w/ ellipsis), artist
  (secondary), thin 2 px progress bar along the bottom edge.
- No window chrome, no shadows that look like a dialog. It should read as a
  game HUD element, not a desktop app.
- Motion: 150 ms fade/slide-in from the nearest edge; 300 ms fade-out.
- Respect `SystemParameters.ReducedMotion`-equivalent setting (expose a
  "disable animations" toggle).

---

## 3. Architecture deep dive

### 3.1 Component diagram

```
┌───────────────────────────── Windows ─────────────────────────────┐
│                                                                   │
│  Spotify.exe   chrome.exe (YouTube)   AppleMusic …                │
│      │               │                    │                       │
│      └──────── System Media Transport Controls (SMTC) ────────┐   │
│                                                               │   │
│  ┌──────────────── GameDeck.App (WPF, tray) ────────────────┐ │   │
│  │  TrayIcon      OverlayWindow      SettingsWindow         │ │   │
│  │      │              │                  │                 │ │   │
│  │  ┌───┴──────────────┴──────────────────┴───┐             │ │   │
│  │  │            GameDeck.Core                │◄────────────┘ │   │
│  │  │  MediaSessionService  (WinRT/SMTC)      │               │   │
│  │  │  HotkeyService        (RegisterHotKey)  │               │   │
│  │  │  SettingsService      (JSON, %APPDATA%) │               │   │
│  │  │  AdBridgeServer       (WebSocket :52780)│◄──────┐       │   │
│  │  └─────────────────────────────────────────┘       │       │   │
│  └──────────────────────────────────────────────────  │  ─────┘   │
│                                                       │           │
│  chrome.exe ── GameDeck extension (MV3) ──────────────┘           │
│    content script (youtube.com) ⇄ service worker ⇄ WebSocket      │
└───────────────────────────────────────────────────────────────────┘
```

### 3.2 Why these choices (keep for interviews)

- **SMTC over Spotify Web API:** no OAuth, no Premium requirement, no rate
  limits, works offline, and it's *universal* — one integration covers every
  media app. Spotify Web API remains a stretch add-on for like/save.
- **WPF over WinUI 3:** WinUI 3 still has rough edges for transparent
  non-rectangular topmost windows; WPF's `AllowsTransparency` + layered
  window styles are battle-tested. Tradeoff acknowledged: WPF is "older" —
  that's fine, this is a systems-integration project, not a UI-framework
  showcase.
- **Extension over ad-blocking / API tricks for YouTube:** clicking the real
  skip button as a user action is robust, honest, and doesn't fight
  YouTube's anti-adblock detection. We are automating a click the user is
  entitled to make, at the user's explicit command.
- **Localhost WebSocket over Native Messaging:** Native Messaging requires a
  registry-installed host manifest and spawns a process per browser; a
  localhost-only WebSocket is simpler, debuggable with any client, and the
  desktop app is already always-running. Tradeoff: must handle "port taken"
  and auth (see 7.4).
- **No injection:** Discord-style in-game rendering requires hooking the
  game's swap chain → anti-cheat (EAC, BattlEye, Vanguard) bans. Hotkeys
  give 100% functional coverage even when the overlay can't draw; that's an
  acceptable, honest limitation.

### 3.3 Threading model

- WinRT SMTC events arrive on arbitrary threadpool threads. Core raises
  plain .NET events without touching UI; **App** marshals via
  `Dispatcher.BeginInvoke`.
- `RegisterHotKey` requires a window handle + message loop on the thread
  that registered it. Use a hidden `HwndSource` message-only window owned by
  the App's UI thread; WM_HOTKEY handling must return fast (fire-and-forget
  the async media command).
- AdBridgeServer runs on background tasks; all inbound messages are queued
  through a single `Channel<T>` consumer to avoid interleaving state.

### 3.4 Project structure (final form)

```
GameDeck.sln
├── src/
│   ├── GameDeck.Core/
│   │   ├── Media/
│   │   │   ├── IMediaSessionService.cs
│   │   │   ├── ISmtcFacade.cs                # thin seam over WinRT (unit-testability)
│   │   │   ├── MediaSessionService.cs        # SMTC wrapper: selection policy + debounce
│   │   │   ├── MediaSnapshot.cs              # immutable: title, artist, art, timeline
│   │   │   └── SessionInfo.cs                # AppUserModelId + friendly name
│   │   ├── Hotkeys/
│   │   │   ├── HotkeyBinding.cs              # modifiers + key + action enum
│   │   │   └── HotkeyAction.cs
│   │   ├── Bridge/
│   │   │   ├── AdBridgeServer.cs             # WebSocket listener
│   │   │   ├── BridgeProtocol.cs             # message records + (de)serialization
│   │   │   └── AdState.cs
│   │   ├── Settings/
│   │   │   ├── AppSettings.cs                # POCO, versioned
│   │   │   └── SettingsService.cs            # load/save/migrate
│   │   └── Logging/…                          # Serilog config helpers
│   ├── GameDeck.App/
│   │   ├── App.xaml(.cs)                      # single-instance guard, DI wiring
│   │   ├── Tray/TrayViewModel.cs
│   │   ├── Overlay/OverlayWindow.xaml(.cs)
│   │   ├── Overlay/OverlayViewModel.cs
│   │   ├── Overlay/WindowInterop.cs           # SetWindowLong, SetWindowPos, DPI
│   │   ├── Hotkeys/HotkeyHost.cs              # HwndSource + WM_HOTKEY pump
│   │   └── Settings/SettingsWindow.xaml(.cs)
│   └── GameDeck.Extension/  (plain folder, not a csproj)
│       ├── manifest.json
│       ├── content.js
│       ├── worker.js
│       └── icons/
├── tests/GameDeck.Core.Tests/
├── build/                                     # CI, winget manifest, publish scripts
├── docs/                                      # architecture.md, screenshots, GIFs
├── CLAUDE.md
├── PLAN.md
├── README.md
├── CHANGELOG.md
└── LICENSE (MIT)
```

---

## 4. Phase 0 — Foundation (target: one evening, ~3 h)

### 4.1 Tasks

- [ ] Verify name availability (GitHub, winget search, Chrome Web Store).
- [ ] `git init`, create public GitHub repo, MIT LICENSE, .gitignore
      (VisualStudio template), README stub with the one-sentence pitch.
- [ ] Install .NET 8 SDK; confirm `dotnet --version` ≥ 8.0.4xx.
- [ ] Scaffold:
      ```bash
      dotnet new sln -n GameDeck
      dotnet new classlib -o src/GameDeck.Core -n GameDeck.Core
      dotnet new wpf      -o src/GameDeck.App  -n GameDeck.App
      dotnet new xunit    -o tests/GameDeck.Core.Tests -n GameDeck.Core.Tests
      dotnet sln add src/GameDeck.Core src/GameDeck.App tests/GameDeck.Core.Tests
      dotnet add src/GameDeck.App reference src/GameDeck.Core
      dotnet add tests/GameDeck.Core.Tests reference src/GameDeck.Core
      ```
- [ ] Edit both `src/*/​*.csproj`: `<TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>`,
      `<Nullable>enable</Nullable>`; Core also gets
      `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
      Note: the Windows TFM automatically references the Windows SDK
      projections — no extra NuGet package needed for SMTC.
- [ ] **Dual-machine setup** (Windows = primary/testing, Mac = occasional
      coding): add `Directory.Build.props` at repo root with
      `<EnableWindowsTargeting>true</EnableWindowsTargeting>` so the whole
      solution *compiles* on macOS (running the app still requires Windows);
      add `.gitattributes` with `* text=auto` to keep CRLF/LF diffs quiet.
      Rule: interop/hotkey/SMTC changes written on the Mac stay on a branch
      until exercised on the Windows machine.
- [ ] Open in **Rider**; add shared `.editorconfig` (Rider default C# rules
      are fine to start).
- [ ] **SMTC smoke test** — temporary code in App startup:
      ```csharp
      var mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
      var session = mgr.GetCurrentSession();
      var props = await session?.TryGetMediaPropertiesAsync();
      Debug.WriteLine($"Now playing: {props?.Title} — {props?.Artist}");
      ```
      Play something in Spotify first. **If this prints, the project is
      de-risked.** If `GetCurrentSession()` is null, check that the source
      app is actually integrated with SMTC (Spotify desktop and all
      Chromium browsers are).
- [ ] GitHub Actions CI: `windows-latest`, steps = checkout → setup-dotnet 8
      → `dotnet build -warnaserror` → `dotnet test`. Add badge to README.
- [ ] Point Claude Code at the repo; CLAUDE.md already encodes constraints.

### 4.2 Exit criteria

Green CI on main; smoke test screenshot saved to `docs/` (it becomes part
of the "how it works" section later).

---

## 5. Phase 1 — Media engine + hotkeys (target: ~1 week of evenings)

The invisible MVP. No overlay yet — by the end of this phase the app must
already be something *you* run daily.

### 5.1 MediaSessionService (Core)

API surface (keep it this small):

```csharp
public interface IMediaSessionService : IAsyncDisposable
{
    Task InitializeAsync();
    MediaSnapshot? Current { get; }                 // null = nothing playing
    IReadOnlyList<SessionInfo> Sessions { get; }
    string? PreferredAppId { get; set; }            // null = follow system current
    event EventHandler<MediaSnapshot?>? SnapshotChanged;
    Task PlayPauseAsync();
    Task NextAsync();
    Task PreviousAsync();
}
```

Implementation notes:
- Acquire manager once via `RequestAsync()`; subscribe to
  `CurrentSessionChanged` and `SessionsChanged` on the manager, and
  `MediaPropertiesChanged` + `PlaybackInfoChanged` + `TimelinePropertiesChanged`
  on the active session. **Unsubscribe from the old session when the active
  session changes** — the classic leak here is stacking handlers.
- Build `MediaSnapshot` as an immutable record: Title, Artist, AlbumTitle,
  `byte[]? AlbumArtPng`, PlaybackStatus, Position, Duration, SourceAppId.
  Convert the WinRT `IRandomAccessStreamReference` thumbnail to bytes inside
  Core; the App converts bytes → frozen `BitmapImage` (freezing makes it
  cross-thread safe).
- Debounce: SMTC fires bursts of property-changed events on track change
  (often 3–6 within 200 ms). Coalesce with a 150 ms debounce before raising
  `SnapshotChanged` or the overlay will flicker.
- Multi-session policy: default to `GetCurrentSession()`. If
  `PreferredAppId` is set (from the tray session picker), find that session
  in `GetSessions()` and pin to it; fall back to current if it disappears.
- Commands: `TrySkipNextAsync()` etc. return bool — log-and-ignore failures
  (some sources reject commands in some states; never throw to the hotkey
  path).
- Position/timeline caveat: SMTC timeline updates are coarse for some apps
  (Spotify updates ~every 5 s). Interpolate locally between updates using a
  stopwatch when PlaybackStatus == Playing; snap on each real update.

Unit tests (this is the testable heart — put real effort here):
- Session-selection policy (preferred id present / missing / null).
- Debounce behavior (use a fake clock).
- Snapshot equality → no event when nothing meaningful changed.
- Wrap the WinRT layer behind a thin `ISmtcFacade` interface so tests never
  touch WinRT.

### 5.2 HotkeyHost (App)

- Hidden `HwndSource` (message-only window: `HWND_MESSAGE` parent).
- P/Invoke: `RegisterHotKey(hwnd, id, MOD_CONTROL|MOD_ALT|MOD_NOREPEAT, vk)`,
  `UnregisterHotKey`. Assign stable ids per action (enum value).
- On WM_HOTKEY: map id → `HotkeyAction`, dispatch to a mediator that calls
  the media service / overlay. Fire-and-forget with exception logging.
- Registration result handling: collect failures into a
  `IReadOnlyList<HotkeyAction> Conflicts` surfaced later in Settings; also
  tray-balloon on startup if any default failed ("Ctrl+Alt+Space is taken by
  another app — rebind in Settings").
- Rebinding flow (built now, UI later): unregister all → register new set →
  report conflicts; persist to settings.

### 5.3 Tray icon (App)

- Package: `H.NotifyIcon.Wpf`.
- Tooltip: "GameDeck — {Title} · {Artist}" (truncate to fit tooltip limit).
- Context menu: Play/Pause, Next, Previous, separator, "Media source ▸"
  (radio list of `Sessions` + "Automatic"), separator, "Start with Windows"
  (checkbox), Settings…, Exit.
- Single-instance guard: named `Mutex("Global\\GameDeck")`; second instance
  activates the first (send a WM_ message or just exit silently with a
  balloon from instance one).

### 5.4 Startup + settings plumbing

- `SettingsService`: JSON at `%APPDATA%\GameDeck\settings.json`;
  `System.Text.Json`, `WriteIndented=true`; include `"version": 1` for
  future migrations; atomic save (write temp file → `File.Replace`).
- Launch on startup: write/remove
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\GameDeck` =
  `"<exe path>" --minimized`. Never touch HKLM (no admin).

### 5.5 Exit criteria

- You use it daily for ≥3 days instead of alt-tabbing.
- CI green; ≥15 meaningful Core tests.
- Tag `v0.1.0` and cut a pre-release zip on GitHub (practice the release
  motion early).

---

## 6. Phase 2 — The overlay (target: ~1–2 weeks)

### 6.1 Window creation

- `OverlayWindow`: `WindowStyle="None"`, `AllowsTransparency="True"`,
  `Background="Transparent"`, `Topmost="True"`, `ShowInTaskbar="False"`,
  `ResizeMode="NoResize"`, `ShowActivated="False"`, `Focusable="False"`.
- On `SourceInitialized`, set extended styles via
  `SetWindowLong(GWL_EXSTYLE, …)`:
  - Always: `WS_EX_TOOLWINDOW` (hide from Alt-Tab) + `WS_EX_NOACTIVATE`
    (never steal focus from the game — **critical**).
  - Click-through mode: add `WS_EX_TRANSPARENT | WS_EX_LAYERED`.
  - Interactive mode: remove `WS_EX_TRANSPARENT`.
- Never call `Activate()` or `Focus()` on the overlay. Ever.

### 6.2 Staying on top (the z-order war)

- Fullscreen apps and some launchers re-assert their own topmost. Strategy:
  1. On `SnapshotChanged`/fade-in, call
     `SetWindowPos(hwnd, HWND_TOPMOST, 0,0,0,0, SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE)`.
  2. Low-frequency guard: a 2 s `DispatcherTimer` re-asserting the same —
     but **only while the overlay is visible** (don't burn CPU hidden).
  3. Re-assert on `SystemEvents.DisplaySettingsChanged` and session
     unlock (`SystemEvents.SessionSwitch`).
- Document the known-unwinnable case: true exclusive-fullscreen (mostly
  older DX9/DX11 titles with flip-model off). Detection heuristic is
  unreliable — instead, put it in the FAQ: "If the overlay doesn't show over
  your game, switch the game to borderless; your hotkeys still work either
  way."

### 6.3 DPI & multi-monitor

- App manifest: `<dpiAwareness>PerMonitorV2</dpiAwareness>`.
- Store position as (monitor device name, anchor corner, offset in DIPs).
  On restore: if the monitor is gone, fall back to primary top-right.
- Corner snapping: when drag ends within 32 px of a corner, snap and store
  the corner as anchor so resolution changes keep it sensible.

### 6.4 Interactivity toggle & drag

- Ctrl+Alt+I flips click-through. Visual affordance in interactive mode:
  1 px accent border + move cursor + small "pin/close/settings" buttons.
- Drag = `MouseLeftButtonDown` → `DragMove()` (works on borderless WPF).
- Failsafes against the "invisible click-eater" bug:
  - Esc key (works because interactive mode may take focus) reverts.
  - Auto-revert to click-through after 30 s of no interaction.
  - Tray menu item "Reset overlay" → click-through + default position.

### 6.5 Rendering the card

- Bind to `OverlayViewModel`: Title, Artist, ArtImage (frozen BitmapImage),
  IsPlaying, Progress (0–1, from interpolated timeline), AdState.
- Album-art fallback: monogram of the source app (♪ glyph) on a colored
  tile derived from a hash of the app id.
- Text: `TextOptions.TextFormattingMode="Display"`, ellipsis truncation,
  max 2 lines total.
- Animations: `DoubleAnimation` on Opacity + a 12 px TranslateTransform
  slide; respect the "disable animations" setting.
- Auto-hide state machine (explicit — don't improvise with nested timers):
  `Hidden → FadingIn → Visible(timer N s) → FadingOut → Hidden`, with
  transitions on: track change, hotkey show, ad state change, hover
  (interactive mode pauses the timer).

### 6.6 Settings window

- Plain WPF window opened from tray. Sections: General (start with Windows,
  animations), Overlay (opacity slider 30–100%, auto-hide seconds 2–15 or
  "always visible", corner presets), Hotkeys (list of actions with current
  bind + "Record" button + conflict badges), Media (session picker mirror),
  About (version, GitHub link, licenses).
- Hotkey recorder: on Record, capture next keydown w/ modifiers, attempt
  registration immediately, show conflict inline if it fails.

### 6.7 Testing matrix (manual, do all of them, record results in docs/)

| Scenario | Expectation |
|---|---|
| Windowed game | Overlay above, click-through works |
| Borderless (e.g. any modern title) | Overlay above |
| "Fullscreen" DXGI flip (Win11 default) | Overlay above |
| Legacy exclusive fullscreen | Overlay hidden; hotkeys still work |
| Mixed-DPI dual monitor | Position stable on both |
| Game that grabs topmost repeatedly | Guard timer wins while visible |
| Alt-Tab | Overlay absent from switcher |
| Game with raw-input mouse | Interactive mode still clickable on overlay |
| Lock → unlock | Overlay recovers, events still firing |
| Spotify closed mid-session | Snapshot goes null, overlay shows idle/hides |

### 6.8 Exit criteria

- Demo GIF over a real game (record with ShareX or OBS; keep <10 MB for the
  README).
- All matrix rows pass or are documented limitations.
- Tag `v0.5.0` pre-release; ask 2–3 friends to install from the release zip
  and note every friction point — that list feeds Phase 4.

---

## 7. Phase 3 — YouTube ad-skip bridge (target: ~1–2 weeks)

The differentiator. Design it like a real protocol, document it in
`docs/bridge-protocol.md`, and mention it prominently in the README.

### 7.1 Bridge protocol (v1)

Transport: WebSocket, `ws://127.0.0.1:52780/bridge`, JSON text frames.
Server = desktop app. Client = extension service worker.

Messages (all include `"v": 1`):

```jsonc
// extension → app
{ "v":1, "type":"hello", "client":"extension", "ext":"1.0.0", "token":"…" }
{ "v":1, "type":"adState", "tabId":123, "adActive":true,
  "skippable":false, "secondsUntilSkippable":4 }
{ "v":1, "type":"adState", "tabId":123, "adActive":false }
{ "v":1, "type":"pong" }

// app → extension
{ "v":1, "type":"helloAck" }
{ "v":1, "type":"skip", "tabId":123 }     // click the skip button
{ "v":1, "type":"ping" }
```

Rules:
- App tracks ad state per tabId; overlay reflects the most recent
  `adActive:true` tab; Ctrl+Alt+S sends `skip` for that tab.
- Heartbeat ping/pong every 15 s; drop dead sockets.
- Multiple browsers/profiles → multiple clients; that's fine, state is
  per-connection+tab.

### 7.2 AdBridgeServer (Core)

- Options: `System.Net.WebSockets` over `HttpListener` on
  `http://127.0.0.1:52780/` (no admin needed for localhost with a URL ACL
  caveat — test this; if `HttpListener` ACL is annoying, use a raw
  `TcpListener` + a minimal WebSocket handshake via a small library, or
  Kestrel-lite via `WebApplication` — pick the least-dependency option that
  works without admin. Decide in a spike, record in the decision log).
- Bind **127.0.0.1 only**. Refuse non-loopback remote endpoints anyway.
- Port conflict: if 52780 is taken, try 52781–52784; write the chosen port
  to `%APPDATA%\GameDeck\bridge.json` — the extension can't read that file,
  so the extension simply tries the same 5 ports in order on connect.
- Auth token (lightweight, honest about its limits): first-run generates a
  token shown in Settings → user pastes it into the extension options page
  once. Prevents a random local webpage from driving the bridge via
  cross-origin WebSocket (pages *can* open localhost sockets). Document
  this threat model in the README — interviewers love it.

### 7.3 Extension (MV3)

Files:
- `manifest.json`: MV3, `"permissions": ["storage"]`,
  `"host_permissions": ["*://www.youtube.com/*"]`, content script on
  youtube.com, background service worker.
- `content.js` (runs in the YouTube tab):
  - Detect ad state from player container class changes
    (historically `.ad-showing` on `.html5-video-player`) and the skip
    button (historically `.ytp-skip-ad-button` / `.ytp-ad-skip-button`).
    **These selectors change over time — verify against the live DOM at
    implementation, and structure detection as a small strategy list that's
    easy to update.** Use a `MutationObserver` on the player element, not
    polling.
  - Report `adState` messages to the worker (`chrome.runtime.sendMessage`).
  - On `skip` command: find the skip button, `.click()` it. If not found,
    report `adActive` refresh so the app UI stays truthful.
- `worker.js`:
  - Owns the WebSocket. Reconnect with backoff (1 s → 30 s cap). MV3
    workers sleep; use `chrome.alarms` (min ~30 s) to nudge reconnection
    and re-open the socket on `adState` traffic from content scripts.
  - Routes: content → socket (adds tabId); socket `skip` → correct tab via
    `chrome.tabs.sendMessage`.
- Options page: token field (stored in `chrome.storage.local`), connection
  status indicator.

### 7.4 Security & ethics posture (put verbatim-ish in README)

- Everything is localhost; no external servers; no analytics.
- The extension only runs on youtube.com and only clicks the same Skip
  button the user could click.
- Skipping is user-initiated (hotkey) by default. An "auto-skip when
  skippable" toggle can exist but ships **off**; be transparent that
  auto-clicking is the user's choice.
- Token handshake prevents arbitrary webpages from puppeting the bridge.

### 7.5 Testing

- Manual: real YouTube ads (open an incognito window without your ad-blocker).
- Protocol unit tests in Core: message (de)serialization, per-tab state
  machine, heartbeat timeout, port-fallback logic.
- Fault drills: kill the app with the extension connected (extension should
  reconnect when app returns); sleep/wake laptop; two browsers at once.

### 7.6 Exit criteria

- The demo: fullscreen game, ad audibly starts in a background YouTube tab,
  overlay strip appears, Ctrl+Alt+S, music/video resumes. **Record this as
  the hero GIF** — it's the whole pitch in 8 seconds.
- Extension published to the Chrome Web Store (developer account is a $5
  one-time fee; first review can take days — submit early, iterate).
- Tag `v0.9.0`.

---

## 8. Phase 4 — Ship it (target: ~1 week)

### 8.1 Packaging

- `dotnet publish src/GameDeck.App -c Release -r win-x64
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
  --self-contained false` → single exe requiring the .NET 8 Desktop Runtime
  (installer link in README + a friendly runtime-missing dialog).
  Evaluate `--self-contained true` (~+60 MB) vs runtime-dependent; pick one
  and record why in the decision log. Self-contained is friendlier for
  non-dev users; that likely wins.
- GitHub Release: zip with exe + README-lite txt. Automate with a
  `release.yml` workflow on tag push (build → publish → upload asset).
- **winget**: author a manifest (Id like `YourName.GameDeck`), PR to
  `microsoft/winget-pkgs`. Requires a stable versioned installer URL — the
  GitHub release asset URL qualifies. This is free distribution + a
  professional signal.
- Code signing: real certs cost money; skip for v1 and document the
  SmartScreen "More info → Run anyway" flow with a screenshot in the README.
  (Optional later: Azure Trusted Signing has a low-cost tier — investigate
  only after the app has users.)

### 8.2 Reliability & logging

- Serilog → rolling file `%APPDATA%\GameDeck\logs\gamedeck-.log`, 7-day
  retention, no PII beyond track titles (mention in README).
- Global handlers: `DispatcherUnhandledException`,
  `TaskScheduler.UnobservedTaskException`, `AppDomain.UnhandledException` →
  log, show one modest crash dialog with a "open logs folder" button, exit
  cleanly. The app must never take down or hitch the game.
- Watchdog behaviors: SMTC manager lost (rare) → retry acquisition with
  backoff; hotkeys silently unregistered by an OS event → re-register on
  session unlock.

### 8.3 First-run & docs

- Onboarding balloon + a `--help`-style first-run overlay tour (3 tooltips
  max).
- README final structure: hero GIF → 3 bullet features → install (winget +
  zip) → hotkey table → "How it works" (component diagram from §3.1) →
  FAQ (fullscreen limitation, SmartScreen, security model) → contributing →
  license.
- `docs/architecture.md`: expand §3 with the diagrams; link from README.
- CHANGELOG.md (Keep a Changelog format), tag **v1.0.0**.

---

## 9. Phase 5 — Users & resume framing (ongoing)

### 9.1 Launch checklist

- [ ] Post to r/pcgaming, r/Spotify, r/software, r/SideProject, and Hacker
      News "Show HN" — one at a time, spaced days apart, each with the hero
      GIF and an honest limitations paragraph (HN respects candor).
      Weekend mornings US time perform best. Reply to every comment fast;
      feature requests → GitHub issues, and say so publicly.
- [ ] Add GitHub topics: `overlay`, `spotify`, `gaming`, `wpf`, `dotnet`,
      `media-controls`, `browser-extension`.
- [ ] Pin an issue: "Roadmap & feature requests."

### 9.2 Metrics (free, no telemetry)

- GitHub stars, release download counts (visible per-asset), Chrome Web
  Store install count, winget install stats (public dashboard).
- Screenshot these periodically; they become the resume numbers.

### 9.3 Resume bullets (draft now, fill numbers later)

- "Built and shipped **GameDeck**, a Windows in-game media overlay
  (.NET 8/WPF, Win32 interop, WinRT media APIs) — N downloads, N GitHub
  stars."
- "Designed a localhost WebSocket protocol bridging a desktop app and a
  Manifest V3 Chrome extension to enable in-game YouTube ad skipping,
  including auth against cross-origin localhost attacks."
- "Chose OS-level media integration (SMTC) over per-service APIs, cutting
  auth complexity to zero while supporting every media app."

---

## 10. Risks & mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Overlay invisible over some fullscreen games | Certain (subset) | Medium | Hotkeys always work; FAQ; borderless guidance |
| YouTube DOM/selectors change | High over time | Medium | Strategy-list detection, easy patch releases, extension auto-updates via Web Store |
| Anti-cheat flags the app | Low (no injection, no game interaction) | High | Never touch game processes; state this in README; topmost windows are what Discord/Steam notifications already do |
| Hotkey conflicts with games/apps | Medium | Low | MOD_NOREPEAT, rebinding UI, conflict detection |
| MV3 service worker sleeps, misses ads | Medium | Medium | Content script re-pokes worker on ad events; alarms-based reconnect |
| `HttpListener` URL ACL needs admin | Medium | Medium | Spike early (7.2); fall back to raw TcpListener or Kestrel |
| SmartScreen scares users | High | Medium | README screenshot + winget path (winget installs bypass the double-warning UX) |
| Scope creep kills momentum | High | High | Non-goals list; each phase ends with a tag + demo |

## 11. Decision log (append as you go)

| Date | Decision | Alternatives | Why |
|---|---|---|---|
| — | SMTC for playback control | Spotify Web API, media-key synthesis | Universal, no auth, offline |
| — | WPF | WinUI 3, Tauri, Electron | Transparent topmost maturity; .NET+Rider fit |
| — | Localhost WebSocket | Native Messaging | Simpler install, app already resident |
| — | No injection | DX hook overlay | Anti-cheat risk unacceptable |
| 2026-07-18 | `HttpListener` for the bridge server | TcpListener + manual WS handshake | Spike confirmed non-admin bind works on `http://127.0.0.1:52780/bridge/` (loopback needs no URL ACL) |

## 12. Rough timeline (evenings/weekends pace)

- Phase 0: 1 evening
- Phase 1: week 1
- Phase 2: weeks 2–3
- Phase 3: weeks 4–5 (submit extension for review at start of week 5)
- Phase 4: week 6
- Launch: end of week 6 / week 7
- Total: ~6–7 weeks part-time to v1.0 + launch.

## 13. Backlog (post-v1.0 only — do not touch earlier)

- Spotify Web API opt-in: like/save track, Spotify Connect volume.
- Auto-skip toggle (ships off) + skip-count stat on the About page.
- Controller chords (XInput: hold Guide/Back + D-pad) — likely the most
  requested gamer feature.
- Per-game profiles via foreground-process detection (position, auto-hide).
- Firefox extension port (WebSocket code is identical; manifest differs).
- Lyrics line via a lyrics API (scope check hard before committing).
- Localization (resx) if issues arrive in other languages.
