# GameDeck Execution Roadmap

**This file is the single source of truth for WHAT to do next.**
CLAUDE.md defines HOW to work (constraints, conventions, protocol);
`handoffs/` (local, gitignored) holds fine-grained session state;
PLAN.md holds the original deep design (reference, not sequencing).
When this file and PLAN.md disagree about sequencing, this file wins.

Last updated: 2026-07-20 (demo GIFs wired into README).

## Rules of engagement — read before doing anything

1. Work strictly top to bottom within the current milestone. The next
   piece of work is always the FIRST unchecked box below. Do not skip
   ahead, reorder, or start a later milestone early.
2. Every item lists its Definition of Done (DoD). An item is checked
   `[x]` only when its DoD is verified — in the same PR as the work.
3. No unlisted work. If something seems missing or wrong, add a
   proposal under "Proposed changes" at the bottom and ask Will —
   do not just do it. Bug fixes for regressions you introduced are the
   only exception.
4. One feature branch per numbered item (or tight group), named
   `feat/phase4-<topic>` etc. Push, PR, CI green, squash-merge. Never
   commit code straight to main.
5. Each item stays inside the hard constraints in CLAUDE.md (zero AI
   attribution, no game-process hooking, SMTC-only core playback,
   TDD for Core logic). Re-read them at session start.
6. Decisions with alternatives get a row in PLAN.md section 11
   (decision log) in the same PR.

## Milestone: v0.5.0 loose ends (now)

- [x] **L1. v0.5.0 release asset upload.** (Done 2026-07-19: zip
      attached, 7,711,079 bytes, verified via `gh release view`.) GitHub uploads endpoint was
      down (502/503) on release night; the release page exists but may
      have no zip. Verify with
      `gh release view v0.5.0 --json assets`. If empty: the zip is
      rebuilt with `dotnet publish src/GameDeck.App -c Release -r
      win-x64 -p:PublishSingleFile=true --self-contained false` after
      DELETING `src/GameDeck.App/bin/Release` and `obj/Release`, verify
      `(Get-Item <exe>).VersionInfo.ProductVersion` starts `0.5.0`, zip
      ONLY the exe (folder-free, exe at zip root, name
      `GameDeck-0.5.0-win-x64.zip`), then
      `gh release upload v0.5.0 <zip>`.
      DoD: `gh release view v0.5.0 --json assets` shows the zip,
      size ~7.7 MB.
- [x] **L2. Demo GIF — MOVED to V1-2b by Will (2026-07-19).** Not
      blocking Phase 4; P4-6 leaves a hero-GIF placeholder slot in the
      README.

## Milestone: Phase 4 — ship-quality (tag v0.9.0)

- [x] **P4-1. `release.yml`: release build in CI on tag push `v*`.**
      (Done 2026-07-19: test tag v0.5.0-test produced a draft with a
      correctly stamped 74.7 MB self-contained zip; workflow survives
      upload outages via bare create + retried upload; test tag deleted.
      Leftover test DRAFT release may still need deleting — GitHub API
      was 503ing during cleanup.)
      windows-latest; publish single-file; decide self-contained
      (PLAN 8.1 leans YES, ~+60 MB, friendlier to non-devs — decide
      with Will, record in PLAN 11); zip exe only; create release with
      `GITHUB_TOKEN` as pre-release draft for manual notes. Mirror the
      manual release motion in CLAUDE.md exactly (clean output, version
      stamp check). Also bump `actions/checkout` and `setup-dotnet` to
      current majors in `ci.yml`.
      DoD: pushing a test tag on a branch produces a draft release with
      a correctly version-stamped zip; test tag and draft deleted after.
- [x] **P4-2. Issue #3: hotkey fallback for raw-input exclusive
      fullscreen.** (Done 2026-07-19: Will verified in DOOM Eternal
      fullscreen — hotkeys work and the game no longer sees consumed
      combo keys; desktop dedupe verified; windowed shooter feel OK per
      matrix row 8. 11 new Core tests.) DOOM Eternal fullscreen swallows RegisterHotKey
      combos (windowed/borderless fine — see issue #3 diagnostics).
      Approach: `WH_KEYBOARD_LL` low-level keyboard hook as a FALLBACK
      path in App (never touches the game process — this is an OS input
      hook, allowed; re-read CLAUDE.md constraint before implementing):
      match only the bound combos, dedupe against WM_HOTKEY (if both
      fire, act once), callback must do nothing but enqueue (no media
      calls inside the hook), re-install if Windows drops the hook.
      Core gets the pure combo-matching logic (TDD); App gets the hook
      plumbing. Verify with DOOM Eternal fullscreen, all six actions.
      DoD: matrix row 3 hotkey check passes in DOOM Eternal fullscreen;
      no added input latency complaints in a windowed shooter session;
      Core tests for matcher/dedupe.
- [x] **P4-3. Crash dialog.** (Done 2026-07-20: code-only `CrashDialog`
      (no XAML/pack-resource dependency, since resource loading may be
      what failed) shown from both fatal handlers via `ShowCrashDialogOnce`
      (Interlocked re-entry guard, dispatcher-marshaled for the AppDomain
      path); UI-thread path sets `Handled=true` + `Shutdown()` for a clean
      exit. Verified end-to-end with an env-gated test-throw: dialog showed
      once, `[FTL]` line logged, "Open logs folder" opened Explorer to
      `%APPDATA%\GameDeck\logs`, process exited cleanly. Scaffold removed.)
      Global handlers currently log only.
      Add a modest WPF dialog on `DispatcherUnhandledException` and
      `AppDomain.UnhandledException`: "GameDeck hit a problem and
      closed." + [Open logs folder] button; keep log-only for
      `UnobservedTaskException`. Never dialog-loop (guard re-entry).
      DoD: a deliberate test-throw shows the dialog once, opens the
      logs folder, exits cleanly.
- [x] **P4-4. First-run onboarding.** (Done 2026-07-20: additive
      `FirstRunShown` bool on AppSettings (version stays 1). `App.Maybe
      ShowFirstRunTour` runs after media init: one tray balloon + a brief
      overlay pop via `OverlayController.ShowWelcome` (shows the current
      track, or a friendly "Play something and I'll show up here" idle hint
      when nothing plays), then sets the flag. No wizard. Verified: deleted
      settings.json -> first launch showed balloon + overlay welcome and
      flipped FirstRunShown=true; second launch showed neither (screenshots).
      107 Core tests green. Will's real settings.json backed up/restored
      around the test.) New `FirstRunShown` settings
      flag. On first launch: tray balloon ("GameDeck is running —
      Ctrl+Alt+O shows the overlay, right-click the note icon for
      settings") + overlay appears ~5 s with whatever is playing or a
      friendly idle line. Max 3 tooltips total (PLAN 8.3). No wizard.
      DoD: delete settings.json → first launch shows the tour once;
      second launch does not.
- [x] **P4-5. Real app icon.** (Done 2026-07-20: app.ico (8 sizes
      16-256, 32bpp) wired into exe via `<ApplicationIcon>`, tray +
      balloon via `TrayIconFactory` loading app.ico @16, and the settings
      window title bar / Alt-Tab via `Icon=`. Build clean; exe-embedded
      icon and 16 px render verified to show the new GameDeck logo. Overlay
      recolored to match + extension PNGs refreshed, per Proposed changes.
      Pulled forward ahead of P4-3/P4-4 at Will's request; his in-situ
      look sign-off is the last gate before merge.) Replace the generated
      tray icon with a designed .ico (exe icon + tray + balloon). Keep it
      legible at 16 px. Will approves the look before merge.
      ASSETS READY (2026-07-19): Will supplied the design; generated
      files live in `C:\Users\wesch\Downloads\GameDeckIcons\` — app.ico
      (8 sizes, verified), icon16/48/128.png for the extension (V1-2),
      preview montage approved down to 16 px. Remaining work is wiring
      only: csproj `<ApplicationIcon>`, tray IconSource, balloon icon;
      commit the assets into the repo in that PR. Master art:
      GameDeckLogo(.svg/PNG.png) in Downloads.
      DoD: exe, tray, Alt-Tab (settings window), and balloon all show
      the new icon; Will signed off.
- [ ] **P4-6. Docs for strangers.** DRAFTED 2026-07-20 (this PR): README
      restructured to the PLAN 8.3 order (3-bullet summary, Install with
      SmartScreen Run-anyway steps + FAQ, hotkey table, How it works,
      security model, Build from source, Contributing, non-goals, license);
      new `docs/architecture.md` from PLAN section 3 (diagram, why-choices,
      hotkey fallback, threading, layout). FAQ covers the
      exclusive-fullscreen overlay limitation and issue #3 (now fixed by the
      P4-2 keyboard-hook fallback). No em dashes; internal anchors resolve.
      REMAINING before checking the box: (a) Will reads it once; (b) two
      image placeholders need real assets: the hero GIF (`docs/media/hero.gif`,
      deferred to V1-2b) and the SmartScreen screenshot
      (`docs/media/smartscreen.png`) — both are HTML-comment slots with
      working text fallbacks so the README stands alone without them.
      README final structure per
      PLAN 8.3 (hero GIF top, 3 bullets, install incl. SmartScreen
      "More info → Run anyway" screenshot + FAQ, hotkey table, how it
      works, security model, contributing, license).
      `docs/architecture.md` from PLAN section 3 diagram. FAQ includes
      the exclusive-fullscreen overlay limitation and issue #3 status.
      DoD: a stranger can install and use it from README alone; Will
      read it once.
- [x] **P4-7. Release v0.9.0.** (Done 2026-07-20: LIVE, published
      14:18Z as a pre-release.) PR #10 (CHANGELOG [0.9.0] with P4-1..P4-6
      and #3 moved to Fixed + compare link; version 0.5.0 -> 0.9.0) merged
      to main (`17928ea`); tag `v0.9.0` pushed; release.yml built
      `GameDeck-0.9.0-win-x64.zip` (74,793,929 bytes, self-contained);
      verified exe stamps `0.9.0+17928ea`, at zip root, downloaded build
      logs "GameDeck 0.9.0 starting". Notes drafted from the CHANGELOG and
      Will published. GIFs/recordings deferred to the v1.0 run, so v0.9.0
      ships with the README placeholders.
      CHANGELOG (move Known issues forward
      if #3 is fixed — it should be by P4-2), bump version, branch
      merge, tag; release now happens via release.yml (P4-1) — verify
      the asset, smoke-test the downloaded zip on a clean PATH.
      DoD: v0.9.0 release live with working zip; CHANGELOG accurate.

## Milestone: v1.0.0

- [ ] **V1-1. Friend test.** 2–3 friends install from the v0.9.0 zip
      cold; collect every friction point as GitHub issues; fix the
      quick ones on branches.
      DoD: each friction point is an issue (open or fixed); no
      installer-blocking issue remains.
- [ ] **V1-2. Chrome Web Store submission.** (Deferred from v0.3.0 by
      Will — do NOT do earlier.) Will needs the $5 dev account. Zip the
      CONTENTS of `extension/` (manifest.json at zip root). Store
      listing: honest description, screenshots, privacy = no data
      collected. Expect days of review; submit, then continue other
      work.
      DoD: submitted for review (live listing not required for v1.0
      tag; update README install steps when it goes live).
- [ ] **V1-2b. Demo GIF (Will drives; moved from L2).** IN PROGRESS
      2026-07-20: Will recorded two clips over Ghost Recon (Will-approved);
      trimmed/compressed with ffmpeg to `docs/media/hero.gif` (ad-skip:
      amber ad -> green skippable -> Ctrl+Alt+S -> content, 640x360, 12 fps,
      9.5 MB) and `docs/media/overlay-demo.gif` (song-skip, 600x338, 8.8 MB),
      both under 10 MB. This PR wires both into the README (hero slot + What
      you get). Raw ~570 MB originals kept on Will's Desktop. REMAINING to
      check the box: (a) hero GIF confirmed rendering in the README on
      GitHub after merge; (b) link/embed it on the latest release page
      (v0.9.0, or the next release). Over a real
      game: track change via hotkey, overlay card fades in/out; ideally
      the ad strip + Ctrl+Alt+S. Under 10 MB (README-embeddable).
      DoD: GIF plays in the README hero slot on GitHub; latest release
      page links or embeds it.
- [ ] **V1-3. Tag v1.0.0.** CHANGELOG, version bump, tag; release via
      release.yml. Remove "Early development" callout from README.
      DoD: v1.0.0 release live, README reflects stable status.
- [ ] **V1-4. winget manifest.** PR to `microsoft/winget-pkgs`, Id like
      `WillSchmidt.GameDeck`, pointing at the v1.0.0 release asset URL.
      DoD: PR merged upstream; `winget install` works.

## Milestone: Phase 5 — launch (after v1.0.0)

- [ ] **P5-1.** Add GitHub topics; pin a "Roadmap & feature requests"
      issue.
- [ ] **P5-2.** Posts, one at a time, days apart, weekend mornings US:
      r/pcgaming, r/Spotify, r/software, r/SideProject, Show HN. Each
      with the hero GIF and an honest limitations paragraph. Reply to
      every comment; feature requests become GitHub issues, say so
      publicly. (Will writes/posts; sessions can draft.)
- [ ] **P5-3.** Track stars/downloads/install counts periodically
      (screenshots — these are the resume numbers, PLAN 9.2/9.3).

## Post-v1.0 backlog

Lives in PLAN.md section 13. Do not start any of it before Phase 5 is
underway, and only with Will's explicit go.

## Proposed changes

- **2026-07-20 — Overlay accent + extension icons bundled into P4-5
  (APPROVED by Will).** The `feat/phase4-icon-theme` branch, beyond
  P4-5's "wiring only" scope, also recolors the overlay accent (interactive
  border + progress bar) from cyan `#4FC3F7` to purple `#9333FD` to match
  the new icon, and refreshes the extension PNGs (icon16/48/128, normally
  V1-2 scope). Will approved keeping both in the P4-5 PR. Logged in PLAN.md
  section 11. Note: P4-5 was pulled forward ahead of P4-3/P4-4 because Will
  had started the icon branch; P4-3 and P4-4 resume in order after it.
