// GameDeck Companion service worker. Owns the WebSocket to the desktop app
// (ws://127.0.0.1:5278x/bridge, localhost only) and routes messages between
// it and the YouTube content scripts. The app pings every 15 s, which also
// keeps this worker alive while connected.

const PORTS = [52780, 52781, 52782, 52783, 52784];
const BACKOFF_START_MS = 1000;
const BACKOFF_CAP_MS = 30000;

let socket = null;
let status = "disconnected"; // disconnected | connecting | connected
let backoffMs = BACKOFF_START_MS;
let reconnectTimer = null;

function setStatus(value) {
  status = value;
  chrome.runtime.sendMessage({ type: "status", value }).catch(() => {});
}

async function connect() {
  if (socket || status === "connecting") return;
  setStatus("connecting");

  const { token } = await chrome.storage.local.get("token");
  if (!token) {
    setStatus("disconnected");
    return; // Nothing to authenticate with until the options page has a token.
  }

  for (const port of PORTS) {
    try {
      socket = await open(`ws://127.0.0.1:${port}/bridge`);
      break;
    } catch {
      // Port not listening; try the next candidate.
    }
  }

  if (!socket) {
    setStatus("disconnected");
    scheduleReconnect();
    return;
  }

  socket.onmessage = (event) => onFrame(event.data);
  socket.onclose = () => {
    socket = null;
    setStatus("disconnected");
    scheduleReconnect();
  };

  socket.send(JSON.stringify({
    v: 1,
    type: "hello",
    client: "extension",
    ext: chrome.runtime.getManifest().version,
    token,
  }));
}

function open(url) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(url);
    ws.onopen = () => resolve(ws);
    ws.onerror = () => reject(new Error("connect failed"));
  });
}

function onFrame(data) {
  let message;
  try {
    message = JSON.parse(data);
  } catch {
    return;
  }

  switch (message.type) {
    case "helloAck":
      backoffMs = BACKOFF_START_MS;
      setStatus("connected");
      rescanAllTabs(); // The app just (re)started; refresh its picture.
      break;
    case "ping":
      socket?.send(JSON.stringify({ v: 1, type: "pong" }));
      break;
    case "skip":
      chrome.tabs.sendMessage(message.tabId, { type: "skip" }).catch(() => {});
      break;
  }
}

function scheduleReconnect() {
  clearTimeout(reconnectTimer);
  reconnectTimer = setTimeout(connect, backoffMs);
  backoffMs = Math.min(backoffMs * 2, BACKOFF_CAP_MS);
}

async function rescanAllTabs() {
  const tabs = await chrome.tabs.query({ url: "*://www.youtube.com/*" });
  for (const tab of tabs) {
    chrome.tabs.sendMessage(tab.id, { type: "rescan" }).catch(() => {});
  }
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.type === "adState" && sender.tab) {
    if (socket && status === "connected") {
      socket.send(JSON.stringify({ v: 1, ...message, tabId: sender.tab.id }));
    } else {
      connect(); // A live YouTube tab is the best reconnect trigger there is.
    }
  } else if (message.type === "getStatus") {
    sendResponse({ value: status });
  } else if (message.type === "tokenChanged") {
    socket?.close();
    backoffMs = BACKOFF_START_MS;
    connect();
  }
  return false;
});

// MV3 workers sleep; a low-frequency alarm nudges the connect loop back to
// life if every other trigger has gone quiet.
chrome.alarms.create("reconnect", { periodInMinutes: 0.5 });
chrome.alarms.onAlarm.addListener((alarm) => {
  if (alarm.name === "reconnect" && !socket) connect();
});

connect();
