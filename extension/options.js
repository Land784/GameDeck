const tokenInput = document.getElementById("token");
const dot = document.getElementById("dot");
const statusText = document.getElementById("statusText");
const savedNote = document.getElementById("saved");

function showStatus(value) {
  dot.className = value;
  statusText.textContent = { connected: "Connected to GameDeck", connecting: "Connecting…" }[value]
    ?? "Disconnected";
}

chrome.storage.local.get("token").then(({ token }) => {
  if (token) tokenInput.value = token;
});

document.getElementById("save").addEventListener("click", async () => {
  await chrome.storage.local.set({ token: tokenInput.value.trim() });
  chrome.runtime.sendMessage({ type: "tokenChanged" }).catch(() => {});
  savedNote.style.visibility = "visible";
  setTimeout(() => (savedNote.style.visibility = "hidden"), 1500);
});

chrome.runtime.onMessage.addListener((message) => {
  if (message.type === "status") showStatus(message.value);
});

chrome.runtime.sendMessage({ type: "getStatus" })
  .then((reply) => showStatus(reply?.value ?? "disconnected"))
  .catch(() => showStatus("disconnected"));
