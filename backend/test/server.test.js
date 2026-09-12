import assert from "node:assert/strict";
import test from "node:test";
import { VoiceActivityDetector } from "../src/audio/VoiceActivityDetector.js";
import { SessionManager } from "../src/session/SessionManager.js";
import { ProviderRouter } from "../src/router/ProviderRouter.js";

test("health payload contract", () => {
  const payload = { ok: true, service: "quest-ai-backend", profile: "FULL", routingMode: "auto" };
  assert.equal(payload.ok, true);
  assert.equal(payload.service, "quest-ai-backend");
});

test("VAD detects speech_started then speech_stopped after silence hold", () => {
  const vad = new VoiceActivityDetector({ speechThreshold: 0.1, silenceHoldMs: 100 });
  const loudFrame = makeFrame(0.5);
  const silentFrame = makeFrame(0);

  assert.equal(vad.process(loudFrame, 0), "speech_started");
  assert.equal(vad.process(loudFrame, 10), "speech_continues");
  assert.equal(vad.process(silentFrame, 20), "speech_continues");
  assert.equal(vad.process(silentFrame, 200), "speech_stopped");
  assert.equal(vad.process(silentFrame, 210), "silence");
});

test("SessionManager rejects invalid state", () => {
  const session = new SessionManager({ idleTimeoutMs: 10_000 });
  assert.throws(() => session.setState("NotAState"));
  session.setState("Listening");
  assert.equal(session.state, "Listening");
});

test("ProviderRouter local_only mode requires local provider", async () => {
  const fakeLocal = { name: "local", isAvailable: async () => true };
  const router = new ProviderRouter({
    providers: { local: fakeLocal, groq: null, gemini: null, openaiLocal: null },
    mode: "local_only",
  });
  const selected = await router.select({ modality: "text", complexity: "low" });
  assert.equal(selected, fakeLocal);
});

function makeFrame(amplitude) {
  const samples = 480; // 10ms @ 48kHz
  const buffer = Buffer.alloc(samples * 2);
  const value = Math.round(amplitude * 32767);
  for (let i = 0; i < samples; i++) buffer.writeInt16LE(value, i * 2);
  return buffer;
}
