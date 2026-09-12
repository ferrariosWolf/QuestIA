// Interface conceitual que todo provider implementa. Unity nunca vê isso —
// só o backend conhece providers.

export const Capability = Object.freeze({
  TEXT: "TEXT",
  AUDIO_INPUT: "AUDIO_INPUT",
  AUDIO_OUTPUT: "AUDIO_OUTPUT",
  VISION: "VISION",
  REALTIME: "REALTIME",
  TOOLS: "TOOLS",
  LOCAL: "LOCAL",
  CLOUD: "CLOUD",
});

export class AIProvider {
  /** @returns {string} identificador estável do provider, ex: "local", "groq", "gemini" */
  get name() {
    throw new Error("AIProvider.name não implementado.");
  }

  async initialize() {}

  /** @returns {Promise<boolean>} */
  async isAvailable() {
    return false;
  }

  /** @returns {Promise<{ok: boolean, detail?: string}>} */
  async health() {
    const ok = await this.isAvailable();
    return { ok };
  }

  /** @returns {string[]} lista de Capability suportadas */
  capabilities() {
    return [];
  }

  /** @param {{prompt: string, model?: string}} input @returns {Promise<{text: string}>} */
  async sendText() {
    throw new Error(`${this.name}: sendText não implementado.`);
  }

  /** @param {{audio: Buffer}} input */
  async sendAudio() {
    throw new Error(`${this.name}: sendAudio não implementado.`);
  }

  /** @param {{image: Buffer, prompt?: string}} input */
  async sendImage() {
    throw new Error(`${this.name}: sendImage não implementado.`);
  }

  async startSession() {
    throw new Error(`${this.name}: startSession não implementado.`);
  }

  async interrupt() {}

  async endSession() {}
}
