import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from '../src/server.mjs';

async function run(env, fetcher, work) {
  const server = createServer(env, fetcher);
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const send = (body, token = '') => fetch(`http://127.0.0.1:${server.address().port}/api/chat`, {
    method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: token }, body: JSON.stringify(body)
  });
  try { await work(send); } finally { await new Promise(resolve => server.close(resolve)); }
}
test('mock, validation and authentication', async () => {
  await run({ APP_TOKEN: 'test' }, undefined, async send => {
    assert.equal((await send({ message: 'Oi' })).status, 401);
    assert.equal((await send({ message: '' }, 'Bearer test')).status, 400);
    const data = await (await send({ message: 'Oi', sessionId: 'one' }, 'Bearer test')).json();
    assert.equal(data.provider, 'mock'); assert.equal(data.sessionId, 'one');
    assert.match(data.answer, /Demonstração/);
  });
});
test('provider request and response', async () => {
  await run({ AI_PROVIDER: 'meta', META_API_KEY: 'secret', META_MODEL: 'configured', META_CHAT_URL: 'https://example.com/chat' }, async (url, options) => {
    assert.equal(options.headers.Authorization, 'Bearer secret');
    assert.equal(JSON.parse(options.body).messages[1].content, 'Oi');
    return Response.json({ choices: [{ message: { content: 'Olá' } }] });
  }, async send => { assert.equal((await (await send({ message: 'Oi' })).json()).answer, 'Olá'); });
});
test('upstream errors do not expose secrets', async () => {
  await run({ AI_PROVIDER: 'meta', META_API_KEY: 'secret', META_MODEL: 'configured', META_CHAT_URL: 'https://example.com/chat' }, async () => new Response('secret', { status: 401 }), async send => {
    const result = await send({ message: 'Oi' }); assert.equal(result.status, 502); assert.doesNotMatch(await result.text(), /secret/);
  });
});
