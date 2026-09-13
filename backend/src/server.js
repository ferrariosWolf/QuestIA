import http from "node:http";
import { startDiscovery } from "./discovery.js";
import { WebSocketServer, WebSocket } from "ws";
import { config } from "./config.js";
import { LocalProvider } from "./providers/LocalProvider.js";
import { GroqProvider } from "./providers/GroqProvider.js";
import { GeminiProvider } from "./providers/GeminiProvider.js";
import { OpenAILocalProvider } from "./providers/OpenAILocalProvider.js";
import { MuseCliProvider } from "./providers/MuseCliProvider.js";
import { GeminiLiveSession } from "./providers/GeminiLiveSession.js";
import { ProviderRouter } from "./router/ProviderRouter.js";
import { VisionRouter } from "./router/VisionRouter.js";
import { VoiceRouter } from "./router/VoiceRouter.js";
import { ModelResourceManager } from "./resource/ModelResourceManager.js";
import { SessionManager } from "./session/SessionManager.js";
import { VoiceActivityDetector } from "./audio/VoiceActivityDetector.js";
import { SpeechToTextRouter } from "./stt/SpeechToTextRouter.js";
import { TextToSpeechRouter } from "./tts/TextToSpeechRouter.js";
import { LatencyTracker } from "./util/LatencyTracker.js";
import { cleanSpeechText } from "./util/speechText.js";

const QUEST_INPUT_SAMPLE_RATE = 24000;

function normalizeSpeech(value) {
  return value.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase().trim();
}

function commandAfterWakeWord(value) {
  if (!config.wakeWord.enabled) return value.trim();
  const normalized = normalizeSpeech(value);
  for (const phrase of config.wakeWord.phrases) {
    const normalizedPhrase = normalizeSpeech(phrase);
    if (normalized === normalizedPhrase) return "";
    if (normalized.startsWith(`${normalizedPhrase} `) || normalized.startsWith(`${normalizedPhrase},`)) {
      return value.slice(phrase.length).replace(/^[\s,.:;!?-]+/, "").trim();
    }
  }
  return null;
}

export const providers = {
  local: new LocalProvider(config.local),
  groq: new GroqProvider(config.groq),
  gemini: new GeminiProvider(config.gemini),
  openaiLocal: new OpenAILocalProvider(),
  muse: new MuseCliProvider(config.muse),
};

export const resourceManager = new ModelResourceManager({ localProvider: providers.local, profile: config.profile });
export const router = new ProviderRouter({ providers, mode: config.routingMode, resourceManager });
export const visionRouter = new VisionRouter({ localProvider: providers.local, geminiProvider: providers.gemini });
export const sttRouter = new SpeechToTextRouter({ localConfig: config.localStt, groqProvider: providers.groq });
export const ttsRouter = new TextToSpeechRouter({ localConfig: config.localTts });

async function buildHealthPayload() {
  const [localAvailable, groqAvailable, geminiAvailable, museAvailable, localSttAvailable, localTtsAvailable] = await Promise.all([
    providers.local.isAvailable(),
    providers.groq.isAvailable(),
    providers.gemini.isAvailable(),
    providers.muse.isAvailable(),
    sttRouter.local.isAvailable(),
    ttsRouter.local.isAvailable(),
  ]);
  const groqSttAvailable = await sttRouter.groq.isAvailable();

  return {
    ok: true,
    service: "quest-ai-backend",
    profile: config.profile,
    routingMode: config.routingMode,
    voiceMode: config.voiceMode,
    providers: {
      local: {
        enabled: config.local.enabled,
        available: localAvailable,
        textModel: config.local.textModel,
        lightModel: config.local.lightModel,
        visionModel: config.local.visionModel,
      },
      groq: { enabled: config.groq.enabled, available: groqAvailable },
      gemini: { enabled: config.gemini.enabled, available: geminiAvailable },
      openaiLocal: { enabled: config.openaiLocal.enabled, available: false },
      muse: { enabled: config.muse.enabled, available: museAvailable, model: config.muse.model },
    },
    voice: {
      localSTT: localSttAvailable,
      groqSTT: groqSttAvailable,
      localTTS: localTtsAvailable,
      geminiLive: geminiAvailable, // sessão sob demanda; ver seção 8 do plano.
      wakeWord: config.wakeWord.enabled,
    },
  };
}

const server = http.createServer(async (request, response) => {
  if (request.method === "GET" && request.url === "/health") {
    const payload = JSON.stringify(await buildHealthPayload());
    response.writeHead(200, { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-store" });
    response.end(payload);
    return;
  }
  response.writeHead(404, { "Content-Type": "application/json; charset=utf-8" });
  response.end(JSON.stringify({ ok: false, error: "not_found" }));
});

const questSockets = new WebSocketServer({ server, maxPayload: 3_000_000 });

server.on("upgrade", (request) => {
  console.log(`[WS] Incoming connection remoteAddress=${request.socket.remoteAddress ?? "unknown"} url/path=${request.url ?? "/"}`);
});

function sendStatus(socket, state, message = "") {
  if (socket.readyState === WebSocket.OPEN) {
    socket.send(JSON.stringify({ type: "status", state, message }));
  }
}

function sendError(socket, message) {
  if (socket.readyState === WebSocket.OPEN) {
    socket.send(JSON.stringify({ type: "error", message }));
  }
}

function sendResponse(socket, text) {
  if (socket.readyState === WebSocket.OPEN) {
    socket.send(JSON.stringify({ type: "response", text }));
  }
}

function sendProviderSelected(socket, stage, providerName) {
  if (socket.readyState === WebSocket.OPEN) {
    socket.send(JSON.stringify({ type: "provider_selected", stage, provider: providerName }));
  }
}

function sendAudio(socket, pcmBuffer) {
  if (socket.readyState === WebSocket.OPEN) {
    socket.send(pcmBuffer, { binary: true });
  }
}

questSockets.on("connection", (questSocket, request) => {
  const peer = request.socket.remoteAddress ?? "unknown";
  console.log(`[WS] connected remoteAddress=${peer} url/path=${request.url ?? "/"}`);

  const session = new SessionManager({
    idleTimeoutMs: config.session.idleTimeoutMs,
    onIdleTimeout: () => sendStatus(questSocket, "listening"),
  });
  const voiceRouter = new VoiceRouter({ profile: config.profile, voiceMode: config.voiceMode });
  const vad = new VoiceActivityDetector();
  let utteranceFrames = [];
  let busy = false;
  async function runTurn(work) {
    if (busy) return;
    busy = true;
    try { await work(); } finally { busy = false; }
  }
  let speaking = false; // true enquanto o backend está enviando áudio de resposta (para barge-in)
  let liveSession = null; // sessão Gemini Live ativa, se voiceMode = gemini_live
  let latestVisionFrame = null;

  questSocket.send(JSON.stringify({ type: "hello", service: "quest-ai-backend", protocol: 1 }));
  session.setState("Connecting");
  sendStatus(questSocket, "connected");
  session.setState("Listening");
  sendStatus(questSocket, "listening");

  async function handleTextQuery(text, latency) {
    const decision = voiceRouter.decide();
    if (!latency) {
      latency = new LatencyTracker();
      latency.mark("vadEnd");
    }

    session.setState("Thinking");
    sendStatus(questSocket, "thinking");
    try {
      const frame = latestVisionFrame && Date.now() - latestVisionFrame.receivedAt <= 10000 ? latestVisionFrame : null;
      let replyText;
      if (frame && config.routingMode === "auto" && !decision.llmForceMode && await providers.muse.isAvailable()) {
        const result = await providers.muse.sendImage({
          image: frame.image,
          prompt: `${text}\n\nUse a imagem atual como contexto. Identifique ingredientes e objetos visíveis e explique o que são ou como podem ser usados, conforme o pedido.`,
        });
        replyText = result.text;
        sendProviderSelected(questSocket, "vision", "muse-cli");
      } else {
        const provider = await router.select(decision.llmTask ?? { modality: "text", complexity: "low" }, decision.llmForceMode);
        session.setProvider(provider);
        sendProviderSelected(questSocket, "llm", provider.name);
        const result = await provider.sendText({ prompt: text, model: provider.name === "local" ? config.local.lightModel : undefined,
          system: "Você é o Quest AI. Responda em português brasileiro, em até três frases curtas, naturais e sem Markdown." });
        replyText = result.text;
      }
      replyText = cleanSpeechText(replyText);
      latency.mark("llmDone");

      session.setState("Speaking");
      sendStatus(questSocket, "speaking");
      sendResponse(questSocket, replyText);

      speaking = true;
      const synthesis = await ttsRouter.synthesize(decision.tts, replyText);
      latency.mark("ttsFirstAudio");
      if (synthesis && speaking) {
        sendProviderSelected(questSocket, "tts", synthesis.provider);
        sendAudio(questSocket, synthesis.pcm);
      }
      console.log(`[latency] ${JSON.stringify(latency.summary())}`);
    } catch (error) {
      console.error(`[pipeline] ${error.message}`);
      sendError(questSocket, "Falha ao processar sua pergunta.");
      sendResponse(questSocket, "Desculpe, não consegui processar agora.");
    } finally {
      speaking = false;
      session.setState("Listening");
      sendStatus(questSocket, "listening");
    }
  }

  async function handleUtterance(pcmBuffer) {
    const decision = voiceRouter.decide();
    const latency = new LatencyTracker();
    latency.mark("vadEnd");

    if (decision.stt === "gemini_live") {
      await handleGeminiLiveUtterance(pcmBuffer);
      return;
    }

    const localReady = decision.stt === "local" && (await sttRouter.local.isAvailable());
    const groqReady = await sttRouter.groq.isAvailable();
    if (!localReady && !groqReady) {
      console.warn("[stt] utterance descartada: nenhum STT disponível.");
      return;
    }

    session.setState("Thinking");
    sendStatus(questSocket, "thinking");
    try {
      const { text, provider: sttProvider } = await sttRouter.transcribe(decision.stt, pcmBuffer, QUEST_INPUT_SAMPLE_RATE);
      latency.mark("sttFinal");
      sendProviderSelected(questSocket, "stt", sttProvider);
      if (text?.trim()) {
        const command = commandAfterWakeWord(text.trim());
        if (command === null) {
          session.setState("Listening");
          sendStatus(questSocket, "listening");
        } else if (!command) {
          sendResponse(questSocket, "Estou ouvindo.");
          session.setState("Listening");
          sendStatus(questSocket, "listening");
        } else {
          await handleTextQuery(command, latency);
        }
      } else {
        session.setState("Listening");
        sendStatus(questSocket, "listening");
      }
    } catch (error) {
      console.error(`[stt] ${error.message}`);
      sendError(questSocket, "Falha no reconhecimento de voz.");
      session.setState("Listening");
      sendStatus(questSocket, "listening");
    }
  }

  async function handleGeminiLiveUtterance(pcmBuffer) {
    if (!(await providers.gemini.isAvailable())) {
      console.warn("[gemini-live] indisponível: GEMINI_API_KEY não configurada.");
      session.setState("Listening");
      sendStatus(questSocket, "listening");
      return;
    }
    session.setState("Thinking");
    sendStatus(questSocket, "thinking");
    sendProviderSelected(questSocket, "voice", "gemini");

    liveSession = new GeminiLiveSession({
      apiKey: config.gemini.apiKey,
      model: config.gemini.liveModel,
      voice: config.gemini.voice,
      instructions: "Você é o Quest AI. Responda em português brasileiro, curto e natural.",
      onStatus: (state) => {
        if (state === "connected") {
          liveSession.sendAudioChunk(pcmBuffer, QUEST_INPUT_SAMPLE_RATE);
          liveSession.endAudioStream();
        }
        if (state === "turnComplete") {
          speaking = false;
          liveSession?.close();
          liveSession = null;
          session.setState("Listening");
          sendStatus(questSocket, "listening");
        }
      },
      onAudio: (audioBuffer) => {
        if (!speaking) {
          speaking = true;
          session.setState("Speaking");
          sendStatus(questSocket, "speaking");
        }
        sendAudio(questSocket, audioBuffer);
      },
      onText: (text) => sendResponse(questSocket, text),
      onError: (error) => {
        console.error(`[gemini-live] ${error.message}`);
        sendError(questSocket, "Falha na sessão Gemini Live.");
      },
    });

    try {
      await liveSession.connect();
    } catch (error) {
      console.error(`[gemini-live] conexão falhou: ${error.message}`);
      sendError(questSocket, "Não foi possível conectar ao Gemini Live.");
      session.setState("Listening");
      sendStatus(questSocket, "listening");
    }
  }

  function handleBargeIn() {
    speaking = false;
    void session.interrupt();
    if (liveSession) {
      liveSession.interrupt();
      liveSession.close();
      liveSession = null;
    }
    // Estado distinto: o cliente só deve limpar áudio pendente quando houve
    // interrupção explícita, nunca no listening normal após uma resposta.
    sendStatus(questSocket, "interrupted");
  }

  questSocket.on("message", (data, isBinary) => {
    if (isBinary) {
      if (busy || data.length % 2 !== 0) return;
      const frame = Buffer.from(data);
      const event = vad.process(frame, Date.now());

      if (event === "speech_started" && speaking) {
        handleBargeIn(); // usuário começou a falar enquanto o backend respondia
      }

      if (event === "speech_started" || event === "speech_continues") {
        utteranceFrames.push(frame);
      } else if (event === "speech_stopped") {
        utteranceFrames.push(frame);
        const utterance = Buffer.concat(utteranceFrames);
        utteranceFrames = [];
        void runTurn(() => handleUtterance(utterance));
      }
      return;
    }

    let message;
    try {
      message = JSON.parse(data.toString("utf8"));
    } catch {
      return;
    }

    if (!message || typeof message !== "object") return;

    if (message.type === "text" && typeof message.text === "string") {
      if (message.text.trim() && message.text.length <= 2000) void runTurn(() => handleTextQuery(message.text));
    } else if (message.type === "vision_frame" && typeof message.imageBase64 === "string") {
      try {
        const image = Buffer.from(message.imageBase64, "base64");
        if (image.length > 0 && image.length <= 2_000_000) {
          latestVisionFrame = { image, receivedAt: Date.now() };
          console.log(`[vision] quadro local recebido bytes=${image.length} ${message.width ?? "?"}x${message.height ?? "?"}`);
        }
      } catch (error) {
        console.warn(`[vision] quadro inválido: ${error.message}`);
      }
    } else if (message.type === "interrupt") {
      handleBargeIn();
    } else if (message.type === "activate" || message.type === "deactivate") {
      // reservado: Unity ainda controla a sessão via conexão/desconexão WS.
    }
  });

  questSocket.on("close", (code, reason) => {
    session.end();
    liveSession?.close();
    console.log(`[WS] close remoteAddress=${peer} code=${code} reason=${reason.toString()}`);
  });
  questSocket.on("error", (error) => {
    console.error(`[WS] error remoteAddress=${peer} type=${error?.constructor?.name ?? "Error"} message=${error.message}`);
  });
});

questSockets.on("error", (error) => {
  console.error(`[WS] server error type=${error?.constructor?.name ?? "Error"} message=${error.message}`);
});

if (config.profile === "PCVR") {
  void resourceManager.unloadAllLocal(); // libera VRAM para o jogo assim que o backend sobe.
}

server.listen(config.port, config.host, () => {
  console.log(`[server] Quest AI Backend em http://${config.host}:${config.port}`);
  console.log(`[server] health: http://localhost:${config.port}/health`);
  console.log(`[server] perfil: ${config.profile} | roteamento: ${config.routingMode} | voz: ${config.voiceMode}`);
  console.log(`[server] modelo local padrão: ${config.local.textModel}`);
});

const discovery = process.env.LAN_DISCOVERY === "true" ? startDiscovery({ backendPort: config.port }) : null;

function shutdown(signal) {
  discovery?.close();
  console.log(`[server] encerrando (${signal})`);
  questSockets.clients.forEach((socket) => socket.close(1001, "Servidor encerrando"));
  server.close(() => process.exit(0));
}

process.on("SIGINT", () => shutdown("SIGINT"));
process.on("SIGTERM", () => shutdown("SIGTERM"));
