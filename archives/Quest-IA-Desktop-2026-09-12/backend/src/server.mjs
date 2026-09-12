import http from 'node:http';
import { randomUUID, timingSafeEqual } from 'node:crypto';
import { pathToFileURL } from 'node:url';

export function createServer(env = process.env, fetcher = fetch) {
  const provider = env.AI_PROVIDER || 'mock';
  if (!['mock', 'meta'].includes(provider)) throw Error('AI_PROVIDER inválido');
  if (provider === 'meta' && (!env.META_API_KEY || !env.META_MODEL || !env.META_CHAT_URL?.startsWith('https://')))
    throw Error('Configure META_API_KEY, META_MODEL e META_CHAT_URL HTTPS');
  const slots = new Map();
  return http.createServer(async (req, res) => {
    const reply = (status, body) => { res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' }); res.end(JSON.stringify(body)); };
    if (req.url === '/health' && req.method === 'GET') return reply(200, { status: 'ok', provider });
    if (req.url !== '/api/chat' || req.method !== 'POST') return reply(404, { error: 'Rota inexistente' });
    if (env.APP_TOKEN) {
      const actual = Buffer.from(req.headers.authorization || '');
      const expected = Buffer.from(`Bearer ${env.APP_TOKEN}`);
      if (actual.length !== expected.length || !timingSafeEqual(actual, expected)) return reply(401, { error: 'Acesso não autorizado' });
    }
    const now = Date.now();
    for (const [key, slot] of slots) if (slot.until < now) slots.delete(key);
    const key = req.socket.remoteAddress;
    const slot = slots.get(key) || { count: 0, until: now + 60000 };
    slots.set(key, slot);
    if (++slot.count > 20) return reply(429, { error: 'Limite de mensagens. Aguarde um minuto.' });
    if (!req.headers['content-type']?.startsWith('application/json')) return reply(415, { error: 'Envie application/json' });
    try {
      let size = 0; const chunks = [];
      for await (const chunk of req) {
        size += chunk.length;
        if (size > 16384) { reply(413, { error: 'Mensagem muito grande' }); return; }
        chunks.push(chunk);
      }
      let body;
      try { body = JSON.parse(Buffer.concat(chunks).toString()); } catch { return reply(400, { error: 'JSON inválido' }); }
      if (!body || typeof body.message !== 'string' || !body.message.trim() || body.message.length > 2000 ||
          (body.sessionId !== undefined && (typeof body.sessionId !== 'string' || !/^[a-zA-Z0-9_-]{1,80}$/.test(body.sessionId))))
        return reply(400, { error: 'Mensagem obrigatória (até 2000 caracteres) e sessionId válido' });
      const sessionId = body.sessionId || randomUUID();
      if (provider === 'mock') return reply(200, { sessionId, provider, answer: 'Demonstração: recebi sua mensagem. Configure o acesso à Meta para respostas de IA.' });
      const upstream = await fetcher(env.META_CHAT_URL, {
        method: 'POST', signal: AbortSignal.timeout(25000),
        headers: { Authorization: `Bearer ${env.META_API_KEY}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ model: env.META_MODEL, messages: [
          { role: 'system', content: 'Você é um assistente no Meta Quest 3. Responda em português, de forma breve. Você recebe apenas texto; não afirme ver o ambiente ou executar ações.' },
          { role: 'user', content: body.message.trim() }
        ], max_tokens: 600 })
      });
      if (!upstream.ok) return reply(502, { error: 'Provedor indisponível. Verifique a configuração no servidor.' });
      const data = await upstream.json();
      const answer = data.choices?.[0]?.message?.content;
      if (typeof answer !== 'string' || !answer.trim()) return reply(502, { error: 'Resposta inesperada do provedor' });
      reply(200, { sessionId, provider, answer });
    } catch (error) {
      reply(error.name === 'TimeoutError' ? 504 : 502, { error: 'Falha na comunicação. Tente novamente.' });
    }
  });
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const host = process.env.HOST || '127.0.0.1';
  if (!['127.0.0.1', 'localhost', '::1'].includes(host) && !process.env.APP_TOKEN) throw Error('APP_TOKEN obrigatório para acesso pela rede');
  const server = createServer();
  server.requestTimeout = 30000;
  server.listen(Number(process.env.PORT || 8787), host, () => console.log(`Quest AI: ${host}:${process.env.PORT || 8787} (${process.env.AI_PROVIDER || 'mock'})`));
}
