export function cleanSpeechText(value) {
  return String(value ?? "")
    .replace(/```[\s\S]*?```/g, "")
    .replace(/\*{1,3}/g, "")
    .replace(/^\s*#{1,6}\s*/gm, "")
    .replace(/^\s*[-•]\s+/gm, "")
    .replace(/\[([^\]]+)\]\([^)]*\)/g, "$1")
    .replace(/[ \t]{2,}/g, " ")
    .replace(/\n{3,}/g, "\n\n")
    .trim();
}
