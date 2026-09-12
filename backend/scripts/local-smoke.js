import assert from "node:assert/strict";
import WebSocket from "ws";

const health = await fetch("http://localhost:8787/health");
assert.equal(health.status, 200);
const payload = await health.json();
assert.equal(payload.ok, true);
assert.equal(payload.service, "quest-ai-backend");
assert.ok(payload.providers?.local, "health deve reportar providers.local");
console.log(`[smoke] health OK — perfil=${payload.profile} local.available=${payload.providers.local.available}`);

const status = await new Promise((resolve, reject) => {
  const socket = new WebSocket("ws://localhost:8787");
  const timeout = setTimeout(() => reject(new Error("WebSocket timeout")), 3000);
  socket.on("message", (data, isBinary) => {
    if (isBinary) return;
    clearTimeout(timeout);
    const message = JSON.parse(data.toString("utf8"));
    socket.close();
    resolve(message);
  });
  socket.on("error", reject);
});

assert.equal(status.type, "status");
assert.equal(status.state, "connected");
console.log("LOCAL_SMOKE_OK health=200 websocket=connected");
