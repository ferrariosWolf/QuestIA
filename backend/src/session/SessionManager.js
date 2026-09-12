// Estado da conversa por conexão Quest. Memória persistente ainda não
// implementada (seção 28) — cada sessão vive só durante a conexão WS.
const STATES = [
  "Inactive",
  "Standby",
  "WakeDetected",
  "Connecting",
  "Listening",
  "Thinking",
  "Speaking",
  "Error",
];

export class SessionManager {
  constructor({ idleTimeoutMs = 60_000, onIdleTimeout } = {}) {
    this.state = "Inactive";
    this.currentProvider = null;
    this.idleTimeoutMs = idleTimeoutMs;
    this.onIdleTimeout = onIdleTimeout;
    this.idleTimer = null;
  }

  setState(nextState) {
    if (!STATES.includes(nextState)) {
      throw new Error(`Estado inválido: ${nextState}`);
    }
    this.state = nextState;
    this.#resetIdleTimer();
  }

  setProvider(provider) {
    this.currentProvider = provider;
  }

  async interrupt() {
    if (this.currentProvider?.interrupt) await this.currentProvider.interrupt();
    this.setState("Listening");
  }

  end() {
    this.#clearIdleTimer();
    this.state = "Inactive";
    this.currentProvider = null;
  }

  #resetIdleTimer() {
    this.#clearIdleTimer();
    if (this.state === "Inactive" || this.state === "Standby") return;
    this.idleTimer = setTimeout(() => {
      this.state = "Standby";
      this.onIdleTimeout?.();
    }, this.idleTimeoutMs);
    this.idleTimer.unref?.();
  }

  #clearIdleTimer() {
    if (this.idleTimer) {
      clearTimeout(this.idleTimer);
      this.idleTimer = null;
    }
  }
}
