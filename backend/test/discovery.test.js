import test from 'node:test';
import assert from 'node:assert/strict';
import dgram from 'node:dgram';
import { once } from 'node:events';
import { startDiscovery } from '../src/discovery.js';
test('LAN discovery echoes the nonce and advertises the voice port', async () => {
  const server = startDiscovery({port:0,host:'127.0.0.1',backendPort:8787});
  const client = dgram.createSocket('udp4');
  try {
    await once(server,'listening');
    const reply = once(client,'message');
    client.send(Buffer.from('QUESTAI_DISCOVER_V1:0123456789abcdef0123456789abcdef'),server.address().port,'127.0.0.1');
    const [data] = await reply;
    assert.equal(data.toString(),'QUESTAI_HERE_V1:0123456789abcdef0123456789abcdef:8787');
  } finally {client.close();server.close();}
});
test('LAN discovery ignores invalid requests', async () => {
  const server = startDiscovery({port:0,host:'127.0.0.1'});
  const client = dgram.createSocket('udp4');
  try {
    await once(server,'listening');let received = false;
    client.on('message',()=>{received=true;});
    client.send(Buffer.from('unrelated broadcast'),server.address().port,'127.0.0.1');
    await new Promise(r=>setTimeout(r,100));assert.equal(received,false);
  } finally {client.close();server.close();}
});
