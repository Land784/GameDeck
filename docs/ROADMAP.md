# GameDeck Execution Roadmap

**This file is the single source of truth for WHAT to do next.**
CLAUDE.md defines HOW to work (constraints, conventions, protocol);
`handoffs/` (local, gitignored) holds fine-grained session state;
PLAN.md holds the original deep design (reference, not sequencing).
When this file and PLAN.md disagree about sequencing, this file wins.

Last updated: 2026-07-19 (v0.5.0 released).

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

- [ ] **P4-1. `release.yml`: release build in CI on tag push `v*`.**
      windows-latest; publish single-file; decide self-contained
      (PLAN 8.1 leans YES, ~+60 MB, friendlier to non-devs — decide
      with Will, record in PLAN 11); zip exe only; create release with
      `GITHUB_TOKEN` as pre-release draft for manual notes. Mirror the
      manual release motion in CLAUDE.md exactly (clean output, version
      stamp check). Also bump `actions/checkout` and `setup-dotnet` to
      current majors in `ci.yml`.
      DoD: pushing a test tag on a branch produces a draft release with
      a correctly version-stamped zip; test tag and draft deleted after.
- [ ] **P4-2. Issue #3: hotkey fallback for raw-input exclusive
      fullscreen.** DOOM Eternal fullscreen swallows RegisterHotKey
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
- [ ] **P4-3. Crash dialog.** Global handlers currently log only.
      Add a modest WPF dialog on `DispatcherUnhandledException` and
      `AppDomain.UnhandledException`: "GameDeck hit a problem and
      closed." + [Open logs folder] button; keep log-only for
      `UnobservedTaskException`. Never dialog-loop (guard re-entry).
      DoD: a deliberate test-throw shows the dialog once, opens the
      logs folder, exits cleanly.
- [ ] **P4-4. First-run onboarding.** New `FirstRunShown` settings
      flag. On first launch: tray balloon ("GameDeck is running —
      Ctrl+Alt+O shows the overlay, right-click the note icon for
      settings") + overlay appears ~5 s with whatever is playing or a
      friendly idle line. Max 3 tooltips total (PLAN 8.3). No wizard.
      DoD: delete settings.json → first launch shows the tour once;
      second launch does not.
- [ ] **P4-5. Real app icon.** Replace the generated tray icon with a
      designed .ico (exe icon + tray + balloon). Keep it legible at
      16 px. Will approves the look before merge.
      DoD: exe, tray, Alt-Tab (settings window), and balloon all show
      the new icon; Will signed off.
- [ ] **P4-6. Docs for strangers.** README final structure per
      PLAN 8.3 (hero GIF top, 3 bullets, install incl. SmartScreen
      "More info → Run anyway" screenshot + FAQ, hotkey table, how it
      works, security model, contributing, license).
      `docs/architecture.md` from PLAN section 3 diagram. FAQ includes
      the exclusive-fullscreen overlay limitation and issue #3 status.
      DoD: a stranger can install and use it from README alone; Will
      read it once.
- [ ] **P4-7. Release v0.9.0.** CHANGELOG (move Known issues forward
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
- [ ] **V1-2b. Demo GIF (Will drives; moved from L2).** Over a real
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

(Empty. Sessions append proposals here instead of doing unlisted work;
Will approves or rejects.)
