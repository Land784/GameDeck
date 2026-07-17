# SMTC smoke test — 2026-07-17

Phase 0 de-risking check: confirm the Windows SMTC API
(`GlobalSystemMediaTransportControlsSessionManager`) exposes the current
media session and its metadata without any auth or per-app integration.

Scratch console app (TFM `net8.0-windows10.0.19041.0`):

```csharp
using Windows.Media.Control;

var mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
var session = mgr.GetCurrentSession();
var props = await session?.TryGetMediaPropertiesAsync();
Console.WriteLine($"Now playing: {props?.Title} — {props?.Artist}");
```

Output with a YouTube video playing in Edge:

```
SMTC sessions: 1
  - MSEdge
Now playing: Stuck at Golf Course Until We Hole in One Every Hole — Good Good [MSEdge]
Status: Playing
```

Result: **de-risked.** SMTC reports sessions, metadata, and playback status
for browser media with zero configuration. The Windows TFM alone provides
the WinRT projections — no extra NuGet packages needed.
