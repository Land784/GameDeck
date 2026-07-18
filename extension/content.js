// GameDeck Companion content script. Watches the YouTube player for ad
// state and clicks the skip button when the desktop app asks. Selectors
// rot; keep them in one strategy list and verify against the live DOM
// before every store release.

const SKIP_BUTTON_SELECTORS = [
  ".ytp-skip-ad-button",
  ".ytp-ad-skip-button",
  ".ytp-ad-skip-button-modern",
];

const COUNTDOWN_SELECTOR = ".ytp-ad-preview-text";

// Pure: derives ad state from a player element. Kept side-effect free so it
// can be exercised against saved ad DOM outside the browser.
function scanAdState(playerEl) {
  if (!playerEl || !playerEl.classList.contains("ad-showing")) {
    return { adActive: false };
  }

  const skipButton = findSkipButton(playerEl);
  const state = {
    adActive: true,
    skippable: skipButton !== null && isShown(skipButton),
  };

  const countdown = playerEl.querySelector(COUNTDOWN_SELECTOR);
  if (countdown) {
    const match = countdown.textContent.match(/\d+/);
    if (match) state.secondsUntilSkippable = parseInt(match[0], 10);
  }
  return state;
}

function findSkipButton(root) {
  for (const selector of SKIP_BUTTON_SELECTORS) {
    const el = root.querySelector(selector);
    if (el) return el;
  }
  return null;
}

function isShown(el) {
  return el.offsetParent !== null || el.getClientRects().length > 0;
}

// YouTube's player ignores a bare synthetic click() on the skip button in
// some builds (handlers hang off pointer/mouse events, or check the event
// source). Send the full sequence a real mouse would produce.
function dispatchRealisticClick(el) {
  const rect = el.getBoundingClientRect();
  const opts = {
    bubbles: true,
    cancelable: true,
    composed: true,
    view: window,
    button: 0,
    clientX: rect.left + rect.width / 2,
    clientY: rect.top + rect.height / 2,
  };
  const pointerOpts = { ...opts, pointerId: 1, isPrimary: true, pointerType: "mouse" };
  el.dispatchEvent(new PointerEvent("pointerover", pointerOpts));
  el.dispatchEvent(new PointerEvent("pointerdown", pointerOpts));
  el.dispatchEvent(new MouseEvent("mousedown", opts));
  el.dispatchEvent(new PointerEvent("pointerup", pointerOpts));
  el.dispatchEvent(new MouseEvent("mouseup", opts));
  el.dispatchEvent(new MouseEvent("click", opts));
}

// Last resort when the button swallows synthetic events entirely: jump the
// ad video to its end. Only ever called while the skip button is showing,
// so it does nothing the user could not do themselves.
function seekAdToEnd() {
  const video = document.querySelector(".html5-video-player.ad-showing video");
  if (video && isFinite(video.duration) && video.duration > 0) {
    video.currentTime = video.duration;
    return true;
  }
  return false;
}

// --- wiring below; nothing above touches chrome.* ---

let lastReported = "";
let debounceTimer = null;

function player() {
  return document.querySelector(".html5-video-player");
}

function report(force = false) {
  const state = scanAdState(player());
  const key = JSON.stringify(state);
  if (!force && key === lastReported) return;
  lastReported = key;
  try {
    chrome.runtime.sendMessage({ type: "adState", ...state });
  } catch {
    // Extension got reloaded out from under us; the new content script takes over.
  }
}

function scheduleReport() {
  clearTimeout(debounceTimer);
  debounceTimer = setTimeout(() => report(), 250);
}

function observe(target) {
  new MutationObserver(scheduleReport).observe(target, {
    subtree: true,
    childList: true,
    attributes: true,
    attributeFilter: ["class", "style"],
  });
}

const playerEl = player();
if (playerEl) {
  observe(playerEl);
} else {
  // Player not built yet (e.g. youtube.com home). Watch the body until it
  // appears, then re-anchor the observer onto it.
  const bodyObserver = new MutationObserver(() => {
    const el = player();
    if (!el) return;
    bodyObserver.disconnect();
    observe(el);
    scheduleReport();
  });
  bodyObserver.observe(document.body, { subtree: true, childList: true });
}

chrome.runtime.onMessage.addListener((message) => {
  if (message.type === "skip") {
    const button = findSkipButton(document);
    if (button) {
      button.click();
      dispatchRealisticClick(button);
      console.info("[GameDeck] skip requested; clicked the skip button");
    }
    // Give the click a beat to land; if the ad survived it while still
    // showing a skip button, fall back to seeking, then report reality.
    setTimeout(() => {
      const state = scanAdState(player());
      if (state.adActive && state.skippable && seekAdToEnd()) {
        console.info("[GameDeck] click was ignored; seeked ad to its end");
      }
      setTimeout(() => report(true), 300);
    }, 500);
  } else if (message.type === "rescan") {
    report(true);
  }
});

report(true);
