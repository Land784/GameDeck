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
    if (button) button.click();
    // Rescan shortly after so the app's strip reflects reality either way.
    setTimeout(() => report(true), 300);
  } else if (message.type === "rescan") {
    report(true);
  }
});

report(true);
