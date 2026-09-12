import { AIProvider, Capability } from "./AIProvider.js";

// REST direta (v1beta generateContent) — evita SDK depreciado. Chamadas reais
// implementadas mas NÃO executadas nesta etapa sem autorização (seção 37).
const GEMINI_BASE_URL = "https://generativelanguage.googleapis.com/v1beta/models";

export class GeminiProvider extends AIProvider {
  constructor({ enabled, apiKey, liveModel, textModel, visionModel, voice }) {
    super();
    this.enabled = enabled;
    this.apiKey = apiKey;
    this.liveModel = liveModel;
    this.textModel = textModel;
    this.visionModel = visionModel;
    this.voice = voice;
  }

  get name() {
    return "gemini";
  }

  async isAvailable() {
    return this.enabled && Boolean(this.apiKey);
  }

  capabilities() {
    return [Capability.TEXT, Capability.VISION, Capability.REALTIME, Capability.AUDIO_OUTPUT, Capability.CLOUD];
  }

  async sendText({ prompt, system }) {
    if (!(await this.isAvailable())) {
      throw new Error("Gemini indisponível: GEMINI_API_KEY não configurada.");
    }
    return this.#generateContent(this.textModel, [{ text: prompt }], system);
  }

  /** @param {{image: Buffer, prompt?: string, mimeType?: string}} input */
  async sendImage({ image, prompt, mimeType = "image/jpeg" }) {
    if (!(await this.isAvailable())) {
      throw new Error("Gemini indisponível: GEMINI_API_KEY não configurada.");
    }
    return this.#generateContent(this.visionModel, [
      { text: prompt || "Descreva o que está na imagem." },
      { inline_data: { mime_type: mimeType, data: image.toString("base64") } },
    ]);
  }

  async #generateContent(model, parts, system) {
    const body = { contents: [{ role: "user", parts }] };
    if (system) body.systemInstruction = { parts: [{ text: system }] };

    const response = await fetch(`${GEMINI_BASE_URL}/${model}:generateContent?key=${this.apiKey}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    if (!response.ok) {
      throw new Error(`Gemini respondeu ${response.status}.`);
    }
    const data = await response.json();
    const text = data.candidates?.[0]?.content?.parts?.map((part) => part.text ?? "").join("") ?? "";
    return { text, model };
  }

  // Gemini Live (REALTIME) fica preparado para seleção explícita futura —
  // não implementado nesta etapa (seção 40).
  async startSession() {
    throw new Error("Gemini Live ainda não implementado nesta versão.");
  }
}
