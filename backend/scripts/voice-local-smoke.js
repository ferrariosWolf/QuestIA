import assert from 'node:assert/strict';
import { WebSocket } from 'ws';
import { resamplePcm16 } from '../src/util/resamplePcm16.js';
const base = 'http://127.0.0.1:8787';
const health = await (await fetch(`${base}/health`)).json();
assert.equal(health.voiceMode, 'local');
assert.equal(health.routingMode, 'local_only');
assert.equal(health.providers.groq.enabled, false);
assert.equal(health.providers.gemini.enabled, false);
assert.equal(health.providers.muse.enabled, false);
assert.ok(health.voice.localSTT && health.voice.localTTS);
const speech = await fetch('http://127.0.0.1:8789/synthesize?text='+encodeURIComponent('Quanto é dois mais dois? Responda em uma frase.'));
assert.ok(speech.ok);
const pcm = resamplePcm16(Buffer.from(await speech.arrayBuffer()), Number(speech.headers.get('X-Sample-Rate')), 24000);
const ws = new WebSocket('ws://127.0.0.1:8787');
await new Promise((resolve,reject)=>{ws.once('open',resolve);ws.once('error',reject);});
const events=[];let audioBytes=0;let complete;
const finished=new Promise((resolve,reject)=>{complete=resolve;setTimeout(()=>reject(Error('Voice pipeline timeout')),90000).unref();});
ws.on('message',(data,binary)=>{
 if(binary){audioBytes+=data.length;return;}
 const msg=JSON.parse(data.toString());events.push(msg);
 if(msg.type==='error') throw Error(msg.message);
 if(msg.type==='status' && msg.state==='listening' && audioBytes>0) complete();
});
ws.send('null');ws.send('{invalid');
const start=Date.now();
const input=Buffer.concat([pcm,Buffer.alloc(48000)]);
for(let offset=0;offset<input.length;offset+=960){ws.send(input.subarray(offset,offset+960));await new Promise(r=>setTimeout(r,20));}
await finished;
assert.ok(events.some(e=>e.type==='response' && e.text.trim()));
assert.ok(events.some(e=>e.type==='provider_selected' && e.stage==='stt' && e.provider==='local'));
assert.ok(events.some(e=>e.type==='provider_selected' && e.stage==='llm' && e.provider==='local'));
assert.ok(audioBytes>2400);
assert.match(events.find(e=>e.type==='response').text, /quatro|4/i);
assert.ok(events.find(e=>e.type==='response').text.length < 300, 'Resposta deve ser curta, sem raciocínio narrado');
console.log(JSON.stringify({ok:true,elapsedMs:Date.now()-start,audioBytes,response:events.find(e=>e.type==='response').text}));
ws.close();
