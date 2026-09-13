import { AIProvider, Capability } from "./AIProvider.js";

// Provider local via Ollama. Único provider com chamadas reais autorizadas
// nos testes desta etapa (seção 36 do plano).
export class LocalProvider extends AIProvider {
  constructor({ baseUrl, textModel, lightModel, visionModel, keepAlive, enabled }) {
    super();
    this.baseUrl = baseUrl.replace(/\/+$/, "");
    this.textModel = textModel;
    this.lightModel = lightModel;
    this.visionModel = visionModel;
    this.keepAlive = keepAlive;
    this.enabled = enabled;
  }

  get name() {
    return "local";
  }

  async isAvailable() {
    if (!this.enabled) return false;
    try {
      const response = await fetch(`${this.baseUrl}/api/tags`, { signal: AbortSignal.timeout(2000) });
      return response.ok;
    } catch {
      return false;
    }
  }

  async health() {
    const available = await this.isAvailable();
    if (!available) return { ok: false, detail: "Ollama indisponível." };
    const models = await this.listModels();
    return { ok: true, detail: `${models.length} modelo(s) instalado(s).` };
  }

  async listModels() {
    try {
      const response = await fetch(`${this.baseUrl}/api/tags`, { signal: AbortSignal.timeout(2000) });
      if (!response.ok) return [];
      const data = await response.json();
      return (data.models ?? []).map((model) => model.name);
    } catch {
      return [];
    }
  }

  capabilities() {
    return [Capability.TEXT, Capability.VISION, Capability.LOCAL];
  }

  /**
   * @param {{prompt: string, model?: string, system?: string}} input
   * @returns {Promise<{text: string, model: string}>}
   */
  async sendText({ prompt, model, system }) {
    const targetModel = model || this.textModel;
    const messages = [];
    if (system) messages.push({ role: "system", content: system });
    messages.push({ role: "user", content: prompt });

    const response = await fetch(`${this.baseUrl}/api/chat`, {
      method: "POST",
      signal: AbortSignal.timeout(60000),
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        model: targetModel,
        messages,
        stream: false,
        think: false,
        options: { num_predict: 256 },
        keep_alive: this.keepAlive,
      }),
    });

    if (!response.ok) {
      throw new Error(`Ollama respondeu ${response.status} para o modelo ${targetModel}.`);
    }
    const data = await response.json();
    return { text: data.message?.content ?? "", model: targetModel };
  }

  /**
   * Visão local via gemma3 (imagem base64). Ainda sem captura real de câmera
   * no Quest — método pronto para quando o VisionRouter tiver frames.
   * @param {{image: Buffer, prompt?: string}} input
   */
  async sendImage({ image, prompt }) {
    const response = await fetch(`${this.baseUrl}/api/chat`, {
      method: "POST",
      signal: AbortSignal.timeout(60000),
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        model: this.visionModel,
        messages: [
          { role: "user", content: prompt || "Descreva o que está na imagem.", images: [image.toString("base64")] },
        ],
        stream: false,
        think: false,
        options: { num_predict: 256 },
        keep_alive: this.keepAlive,
      }),
    });
    if (!response.ok) {
      throw new Error(`Ollama (visão) respondeu ${response.status}.`);
    }
    const data = await response.json();
    return { text: data.message?.content ?? "", model: this.visionModel };
  }
}
