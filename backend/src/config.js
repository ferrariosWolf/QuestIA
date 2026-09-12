import "dotenv/config";

const PROFILES = ["FULL", "BALANCED", "LOW_VRAM", "PCVR"];
const ROUTING_MODES = ["auto", "local_only", "groq_only", "gemini_only"];
const VOICE_MODES = ["auto", "local", "groq", "gemini_live"];

function parsePort(value) {
  const port = Number.parseInt(value ?? "8787", 10);
  if (!Number.isInteger(port) || port < 1 || port > 65535) {
    throw new Error("PORT deve ser um número entre 1 e 65535.");
  }
  return port;
}

function parseProfile(value) {
  const profile = (value ?? "FULL").trim().toUpperCase();
  return PROFILES.includes(profile) ? profile : "FULL";
}

function parseRoutingMode(value) {
  const mode = (value ?? "auto").trim().toLowerCase();
  return ROUTING_MODES.includes(mode) ? mode : "auto";
}

function parseVoiceMode(value) {
  const mode = (value ?? "auto").trim().toLowerCase();
  return VOICE_MODES.includes(mode) ? mode : "auto";
}

function parseBool(value, fallback) {
  if (value === undefined || value === null || value === "") return fallback;
  return String(value).trim().toLowerCase() === "true";
}

const profile = parseProfile(process.env.QUEST_AI_PROFILE);

// Keep-alive por perfil: quanto tempo o Ollama mantém o modelo residente em VRAM.
// Centralizado aqui — nenhum outro arquivo deve hardcodar essa política.
const KEEP_ALIVE_BY_PROFILE = {
  FULL: "30m",
  BALANCED: "10m",
  LOW_VRAM: "5m",
  PCVR: "0s",
};

export const config = Object.freeze({
  host: "0.0.0.0",
  port: parsePort(process.env.PORT),
  profile,
  routingMode: parseRoutingMode(process.env.AI_ROUTING_MODE),

  local: Object.freeze({
    enabled: parseBool(process.env.LOCAL_AI_ENABLED, true),
    baseUrl: (process.env.OLLAMA_BASE_URL || "http://127.0.0.1:11434").trim(),
    textModel: (process.env.OLLAMA_TEXT_MODEL || "qwen3:8b").trim(),
    lightModel: (process.env.OLLAMA_LIGHT_MODEL || "qwen3:4b").trim(),
    visionModel: (process.env.OLLAMA_VISION_MODEL || "gemma3:4b").trim(),
    keepAlive: (process.env.OLLAMA_KEEP_ALIVE || KEEP_ALIVE_BY_PROFILE[profile]).trim(),
  }),

  groq: Object.freeze({
    enabled: parseBool(process.env.GROQ_ENABLED, true),
    apiKey: (process.env.GROQ_API_KEY || "").trim(),
    textModel: (process.env.GROQ_TEXT_MODEL || "openai/gpt-oss-120b").trim(),
    sttModel: (process.env.GROQ_STT_MODEL || "whisper-large-v3-turbo").trim(),
  }),

  gemini: Object.freeze({
    enabled: parseBool(process.env.GEMINI_ENABLED, true),
    apiKey: (process.env.GEMINI_API_KEY || "").trim(),
    liveModel: (process.env.GEMINI_LIVE_MODEL || "gemini-3.1-flash-live-preview").trim(),
    textModel: (process.env.GEMINI_TEXT_MODEL || "gemini-3.6-flash").trim(),
    visionModel: (process.env.GEMINI_VISION_MODEL || "gemini-3.6-flash").trim(),
    voice: (process.env.GEMINI_VOICE || "Kore").trim(),
  }),

  openaiLocal: Object.freeze({
    enabled: false, // mantido sempre desabilitado; ver seção 18 do plano.
  }),

  muse: Object.freeze({
    enabled: parseBool(process.env.MUSE_CLI_ENABLED, true),
    distro: (process.env.WSL_DISTRO || "Ubuntu").trim(),
    cliPath: (process.env.MUSE_CLI_PATH || "/home/ferrarioswolf/.local/bin/muse").trim(),
    model: (process.env.MUSE_CLI_MODEL || "muse-spark-1.3-contributor").trim(),
  }),

  localStt: Object.freeze({
    enabled: parseBool(process.env.LOCAL_STT_ENABLED, true),
    provider: (process.env.LOCAL_STT_PROVIDER || "faster-whisper").trim(),
    model: (process.env.LOCAL_STT_MODEL || "small").trim(),
    language: (process.env.LOCAL_STT_LANGUAGE || "pt").trim(),
    device: (process.env.LOCAL_STT_DEVICE || "cpu").trim(),
    baseUrl: (process.env.LOCAL_STT_URL || "http://127.0.0.1:8788").trim(),
  }),

  localTts: Object.freeze({
    enabled: parseBool(process.env.LOCAL_TTS_ENABLED, true),
    provider: (process.env.LOCAL_TTS_PROVIDER || "piper").trim(),
    voice: (process.env.LOCAL_TTS_VOICE || "pt_BR-faber-medium").trim(),
    baseUrl: (process.env.LOCAL_TTS_URL || "http://127.0.0.1:8789").trim(),
  }),

  voiceMode: parseVoiceMode(process.env.VOICE_MODE),

  wakeWord: Object.freeze({
    enabled: parseBool(process.env.WAKE_WORD_ENABLED, false),
    phrases: (process.env.WAKE_WORD_PHRASES || "ei quest,hey quest,ei meta,hey meta")
      .split(",").map((value) => value.trim().toLowerCase()).filter(Boolean),
  }),

  session: Object.freeze({
    idleTimeoutMs: 60_000,
  }),
});
