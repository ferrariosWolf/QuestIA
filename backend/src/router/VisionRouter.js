// Sem câmera ainda (seção 26-27) — recebe um frame já capturado sob demanda
// ("Quest, olhe isso") e decide local (gemma3) vs cloud (Gemini Vision).
export class VisionRouter {
  constructor({ localProvider, geminiProvider }) {
    this.localProvider = localProvider;
    this.geminiProvider = geminiProvider;
  }

  /** @param {{image: Buffer, prompt?: string, complexity?: "simple"|"complex"}} input */
  async describe({ image, prompt, complexity = "simple" }) {
    if (complexity === "simple" && (await this.localProvider.isAvailable())) {
      return this.localProvider.sendImage({ image, prompt });
    }
    if (await this.geminiProvider.isAvailable()) {
      return this.geminiProvider.sendImage({ image, prompt });
    }
    if (await this.localProvider.isAvailable()) {
      return this.localProvider.sendImage({ image, prompt });
    }
    throw new Error("Nenhum provider de visão disponível.");
  }
}
