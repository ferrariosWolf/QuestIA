import { AIProvider } from "./AIProvider.js";

// Reserva de arquitetura para futura integração oficialmente suportada.
// Permanece sempre disabled/unavailable — sem cookies, tokens de sessão,
// automação de site ou bypass de cobrança (seção 18 do plano).
export class OpenAILocalProvider extends AIProvider {
  get name() {
    return "openaiLocal";
  }

  async isAvailable() {
    return false;
  }

  capabilities() {
    return [];
  }
}
