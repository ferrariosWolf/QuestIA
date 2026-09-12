// Wake word "Quest" — interface real + fallback manual.
//
// Decisão: nenhuma lib gratuita/local/ARM64/Unity6 de wake word foi validada
// com confiabilidade suficiente para esta etapa sem instalar dependência
// obscura (proibido pela seção 20). Fallback: ativação manual (botão na UI,
// já existente em AssistantStateController.Activate()) continua sendo o
// caminho até um detector local ser adotado.
export class WakeWordDetector {
  constructor({ keyword = "quest", enabled = false } = {}) {
    this.keyword = keyword;
    this.enabled = enabled; // false = fallback manual ativo
  }

  isAvailable() {
    return this.enabled;
  }

  /**
   * @param {Buffer} _pcm16Frame reservado para futura detecção local
   * @returns {boolean} sempre false até um detector real ser plugado
   */
  detect(_pcm16Frame) {
    if (!this.enabled) return false;
    throw new Error("WakeWordDetector: nenhum detector local plugado ainda.");
  }
}
