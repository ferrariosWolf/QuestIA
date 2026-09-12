import { execFile } from "node:child_process";
import { mkdir, rm, writeFile } from "node:fs/promises";
import path from "node:path";
import { randomUUID } from "node:crypto";
import { promisify } from "node:util";
import { AIProvider, Capability } from "./AIProvider.js";

const execFileAsync = promisify(execFile);

export class MuseCliProvider extends AIProvider {
  constructor({ enabled = true, distro = "Ubuntu", cliPath = "/home/ferrarioswolf/.local/bin/muse", model = "muse-spark-1.3-contributor", tempDir }) {
    super();
    this.enabled = enabled;
    this.distro = distro;
    this.cliPath = cliPath;
    this.model = model;
    this.tempDir = tempDir || path.resolve(".muse-tmp");
    this.availability = null;
  }

  get name() { return "muse-cli"; }
  capabilities() { return [Capability.TEXT, Capability.VISION, Capability.CLOUD]; }

  async isAvailable() {
    if (!this.enabled) return false;
    if (this.availability !== null) return this.availability;
    try {
      await execFileAsync("wsl.exe", ["-d", this.distro, "--", this.cliPath, "--version"], { timeout: 15000, windowsHide: true });
      this.availability = true;
    } catch {
      this.availability = false;
    }
    return this.availability;
  }

  async sendText({ prompt }) {
    return { text: await this.#run(prompt), model: this.model };
  }

  async sendImage({ image, prompt }) {
    await mkdir(this.tempDir, { recursive: true });
    const filename = `${randomUUID()}.jpg`;
    const windowsPath = path.join(this.tempDir, filename);
    await writeFile(windowsPath, image);
    try {
      const linuxPath = windowsPath.replace(/^([A-Za-z]):/, (_, drive) => `/mnt/${drive.toLowerCase()}`).replaceAll("\\", "/");
      return { text: await this.#run(prompt || "Descreva o que está visível.", linuxPath), model: this.model };
    } finally {
      await rm(windowsPath, { force: true });
    }
  }

  async #run(prompt, imagePath) {
    const args = ["-d", this.distro, "--", this.cliPath, "exec", "--json", "--disable-write", "--disable-shell", "--disable-web-tools", "--no-foreign-personal-context", "--no-session-log", "--max-model-steps", "1", "--model", this.model];
    if (imagePath) args.push("--image", imagePath);
    args.push(`Você é o assistente visual do Meta Quest 3. Responda em português brasileiro, de modo direto e útil. Descreva somente o que puder sustentar pela imagem; quando houver dúvida, diga isso.\n\nPedido: ${prompt}`);
    const { stdout } = await execFileAsync("wsl.exe", args, { timeout: 120000, maxBuffer: 8 * 1024 * 1024, windowsHide: true });
    let answer = "";
    for (const line of stdout.split(/\r?\n/)) {
      if (!line.startsWith("{")) continue;
      try {
        const event = JSON.parse(line);
        if (event.payload_type === "run.terminal.completed" && typeof event.payload?.text === "string") answer = event.payload.text;
      } catch { }
    }
    if (!answer.trim()) throw new Error("Muse Code terminou sem resposta.");
    return answer.trim();
  }
}
