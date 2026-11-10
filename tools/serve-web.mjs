import http from 'node:http';
import { createReadStream, statSync } from 'node:fs';
import path from 'node:path';
const option = (name, fallback) => { const i = process.argv.indexOf(name); return i >= 0 ? process.argv[i + 1] : fallback; };
const root = path.resolve(option('--root', 'Builds/Web')), port = Number(option('--port', '8080'));
const types = { '.html': 'text/html', '.js': 'application/javascript', '.json': 'application/json', '.wasm': 'application/wasm', '.css': 'text/css', '.png': 'image/png', '.ico': 'image/x-icon' };
http.createServer((request, response) => {
  let file;
  try {
    const pathname = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
    file = path.resolve(root, '.' + (pathname.endsWith('/') ? pathname + 'index.html' : pathname));
    if (file !== root && !file.startsWith(root + path.sep)) throw new Error('Outside web root');
    if (!statSync(file).isFile()) throw new Error('Not a file');
  } catch { response.writeHead(404); response.end('Not found'); return; }
  const gzip = file.endsWith('.gz'), extension = path.extname(gzip ? file.slice(0, -3) : file);
  response.writeHead(200, { 'Content-Type': types[extension] ?? 'application/octet-stream', ...(gzip ? { 'Content-Encoding': 'gzip' } : {}), 'Cache-Control': 'no-cache' });
  createReadStream(file).pipe(response);
}).listen(port, '127.0.0.1', () => console.log(`Unity Web: http://127.0.0.1:${port} (${root})`));
