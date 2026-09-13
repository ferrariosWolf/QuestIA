import dgram from 'node:dgram';
export function startDiscovery({ port = 8786, backendPort = 8787, host = '0.0.0.0' } = {}) {
  const socket = dgram.createSocket('udp4');
  socket.on('message', (message, peer) => {
    const match = /^QUESTAI_DISCOVER_V1:([a-f0-9]{32})$/.exec(message.toString());
    if (match) socket.send(Buffer.from('QUESTAI_HERE_V1:' + match[1] + ':' + backendPort), peer.port, peer.address);
  });
  socket.on('error', error => console.error('[discovery] ' + error.message));
  socket.bind(port, host);
  return socket;
}
