// Local protocol fixture for automated tests. Production uses Unity Gaming Services.
// Never deploy this test server as a public leaderboard.
import http from 'node:http';
import { randomUUID } from 'node:crypto';
import { appendFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

export function createFixtureServer({ trace } = {}) {
  const sessions = new Map(), access = new Map(), boards = new Map();
  const stats = { anonymous: 0, refreshed: 0, submitted: 0, reads: 0 };
  let nextStatus = 0, malformed = false, delayMs = 0;
  function issue(id, sessionToken = randomUUID()) {
    sessions.set(sessionToken, id);
    const idToken = randomUUID(); access.set(idToken, id);
    return { userId: id, sessionToken, idToken, expiresIn: 3600, lastNotificationDate: '1' };
  }
  const server = http.createServer(async (req, res) => {
    const url = new URL(req.url, 'http://127.0.0.1');
    const send = (status, body = {}) => {
      res.writeHead(status, { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*',
        'Access-Control-Allow-Headers': 'Authorization,Content-Type,ProjectId,UnityEnvironment',
        'Access-Control-Allow-Methods': 'GET,POST,OPTIONS', ...(status === 429 ? { 'Retry-After': '1' } : {}) });
      res.end(status === 204 ? '' : JSON.stringify(body));
      if (trace) appendFileSync(trace, JSON.stringify({ method: req.method, path: url.pathname, status }) + '\n');
    };
    if (req.method === 'OPTIONS') return send(204);
    let body = {};
    try {
      let raw = '';
      for await (const chunk of req) { raw += chunk; if (raw.length > 65536) return send(413); }
      if (raw) body = JSON.parse(raw);
    } catch { return send(400); }
    if (url.pathname === '/__test/stats') return send(200, stats);
    if (url.pathname === '/__test/control' && req.method === 'POST') {
      nextStatus = body.nextStatus ?? 0; malformed = body.malformed ?? false; delayMs = body.delayMs ?? 0; return send(200);
    }
    if (url.pathname.startsWith('/v1/projects/') && delayMs) { const delay = delayMs; delayMs = 0; await new Promise(resolve => setTimeout(resolve, delay)); }
    if (url.pathname.startsWith('/v1/projects/') && nextStatus) { const status = nextStatus; nextStatus = 0; return send(status); }
    if (url.pathname.startsWith('/v1/projects/') && malformed) { malformed = false; return send(200, { invalid: true }); }
    if (url.pathname === '/v1/authentication/anonymous' && req.method === 'POST') {
      if (!req.headers.projectid || req.headers.unityenvironment !== 'production') return send(400);
      stats.anonymous++; return send(200, issue(randomUUID()));
    }
    if (url.pathname === '/v1/authentication/session-token' && req.method === 'POST') {
      const id = sessions.get(body.sessionToken); if (!id) return send(401);
      stats.refreshed++; return send(200, issue(id, body.sessionToken));
    }
    const playerId = access.get((req.headers.authorization ?? '').replace(/^Bearer /, ''));
    if (!playerId) return send(401);
    if (/^\/v1\/users\/[^/]+\/notifications$/.test(url.pathname)) {
      if (!req.headers.projectid) return send(400);
      return send(200, { notifications: [{ id: 'fixture-notice', message: 'Test account notification' }] });
    }
    const match = /^\/v1\/projects\/([^/]+)\/leaderboards\/(dodge-line-level-[1-5]-v\d+)\/scores(?:\/players\/([^/]+))?$/.exec(url.pathname);
    if (!match) return send(404);
    const key = match[1] + '/' + match[2];
    if (!boards.has(key)) boards.set(key, new Map());
    const board = boards.get(key);
    const entries = () => [...board.values()].sort((a, b) => b.score - a.score || a.playerId.localeCompare(b.playerId)).map((entry, rank) => ({ ...entry, rank }));
    if (req.method === 'POST') {
      if (match[3] !== playerId) return send(403);
      if (!Number.isSafeInteger(body.score) || body.score < 0) return send(400);
      const current = board.get(playerId);
      if (!current || body.score > current.score) board.set(playerId, { playerId, playerName: '', score: body.score, metadata: body.metadata ?? {} });
      stats.submitted++; return send(200, entries().find(entry => entry.playerId === playerId));
    }
    if (req.method !== 'GET') return send(405);
    stats.reads++;
    if (match[3]) {
      const entry = entries().find(entry => entry.playerId === match[3]); return send(entry ? 200 : 404, entry);
    }
    const all = entries(), limit = Math.min(100, Math.max(1, Number(url.searchParams.get('limit') ?? 10)));
    return send(200, { offset: 0, limit, total: all.length, results: all.slice(0, limit) });
  });
  return server;
}
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const option = (name, fallback) => { const i = process.argv.indexOf(name); return i >= 0 ? process.argv[i + 1] : fallback; };
  const port = Number(option('--port', '8787'));
  createFixtureServer({ trace: option('--trace') }).listen(port, '127.0.0.1', () => console.log(`Local test fixture: http://127.0.0.1:${port}`));
}
