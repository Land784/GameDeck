# Manual test matrix (Phase 2 exit, v0.5.0)

Fill the Result column per run: PASS / FAIL (issue #) / N/A. Anything
failing becomes a GitHub issue before v0.5.0 is tagged.

Overlay checks per scenario: card appears on track change, hotkeys work,
Ctrl+Alt+I drag works and the position survives an app restart, the ad
strip shows over the game when a background YouTube ad plays.

| # | Scenario | What to check | Result |
|---|---|---|---|
| 1 | Windowed game | Overlay above the game window | |
| 2 | Borderless fullscreen game | Overlay visible, no flicker | |
| 3 | Modern "fullscreen" (DXGI flip, most current games) | Overlay visible | |
| 4 | Legacy exclusive fullscreen | Overlay may be hidden (ACCEPTED); hotkeys still work | |
| 5 | Mixed-DPI monitors (if available) | Overlay position correct on each monitor, no drift | |
| 6 | Game that reasserts topmost | Overlay recovers within ~2 s | |
| 7 | Alt-Tab | No GameDeck entry in the Alt-Tab list | |
| 8 | Raw-input shooter | No mouse/aim hitching while overlay fades | |
| 9 | Windows lock -> unlock | Hotkeys and media still work; overlay reappears on track change | |
| 10 | Spotify closed mid-session | Tray/settings show "(not running)" pin; playback control moves on | |
| 11 | Drag overlay near a corner | Snaps to the 16 px margin within 32 px; position persists | |
| 12 | Settings: opacity slider | Card previews live at the new opacity | |
| 13 | Settings: rebind a hotkey to a taken combo | Red "in use by another app" badge appears | |
| 14 | Second launch of the exe | Balloon appears; only one instance remains | |

Date tested: ____ Build: ____ GPU/driver: ____ Monitors: ____
