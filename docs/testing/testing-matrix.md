# Manual test matrix (Phase 2 exit, v0.5.0)

Fill the Result column per run: PASS / FAIL (issue #) / N/A. Anything
failing becomes a GitHub issue before v0.5.0 is tagged.

Overlay checks per scenario: card appears on track change, hotkeys work,
Ctrl+Alt+I drag works and the position survives an app restart, the ad
strip shows over the game when a background YouTube ad plays.

| # | Scenario | What to check | Result                                                                                         |
|---|---|---|------------------------------------------------------------------------------------------------|
| 1 | Windowed game | Overlay above the game window | Pass                                                                                           |
| 2 | Borderless fullscreen game | Overlay visible, no flicker | Pass                                                                                           |
| 3 | Modern "fullscreen" (DXGI flip, most current games) | Overlay visible | FAIL (issue #3: DOOM Eternal fullscreen — overlay hidden AND hotkeys dead) |
| 4 | Legacy exclusive fullscreen | Overlay may be hidden (ACCEPTED); hotkeys still work | Pass (Geometry Dash fullscreen: overlay hidden, hotkeys worked)                                |
| 5 | Mixed-DPI monitors (if available) | Overlay position correct on each monitor, no drift | N/A (both monitors at 100% scale; no mixed-DPI hardware available) |
| 6 | Game that reasserts topmost | Overlay recovers within ~2 s | Pass (scripted stealer reasserting every 500 ms; worst buried 1.7 s)                           |
| 7 | Alt-Tab | No GameDeck entry in the Alt-Tab list | Pass                                                                                           |
| 8 | Raw-input shooter | No mouse/aim hitching while overlay fades | Pass (DOOM Eternal, windowed: no perf or aim impact during fades) |
| 9 | Windows lock -> unlock | Hotkeys and media still work; overlay reappears on track change | Pass                                                                                           |
| 10 | Spotify closed mid-session | Tray/settings show "(not running)" pin; playback control moves on | Pass                                                                                           |
| 11 | Drag overlay near a corner | Snaps to the 16 px margin within 32 px; position persists | Pass                                                                                           |
| 12 | Settings: opacity slider | Card previews live at the new opacity | Pass                                                                                           |
| 13 | Settings: rebind a hotkey to a taken combo | Red "in use by another app" badge appears | Pass                                                                                           |
| 14 | Second launch of the exe | Balloon appears; only one instance remains | Pass (scripted: second instance exited, original survived; balloon verified live in Session B) |

Date tested: 7/19/2026 Build: 0.3.0+f12b315
GPU/driver: NVIDIA GeForce RTX 3060 Ti, driver 32.0.15.9636 (596.36)
Monitors: AOC 24G1WG3 1920x1080 @ 100% (primary) + Dell E228WFP 1680x1050 @ 100%
