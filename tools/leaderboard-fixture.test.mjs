import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createFixtureServer } from './leaderboard-fixture.mjs';

test('actual HTTP protocol: identity, best score, five boards, ranks, errors and CORS', async () => {
  const server = createFixtureServer();
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const base = `http://127.0.0.1:${server.address().port}`;
  const project = 'af360e97-8619-400d-8287-97c7d0b04335';
  const call = async (path, { method = 'GET', token, body } = {}) => {
    const response = await fetch(base + path, { method, headers: { ProjectId: project, UnityEnvironment: 'production',
      'Content-Type': 'application/json', ...(token ? { Authorization: 'Bearer ' + token } : {}) }, body: body === undefined ? undefined : JSON.stringify(body) });
    return { status: response.status, body: response.status === 204 ? null : await response.json(), headers: response.headers };
  };
  const route = level => `/v1/projects/${project}/leaderboards/dodge-line-level-${level}-v1/scores`;
  try {
    const first = (await call('/v1/authentication/anonymous', { method: 'POST', body: {} })).body;
    const other = (await call('/v1/authentication/anonymous', { method: 'POST', body: {} })).body;
    assert.notEqual(first.userId, other.userId);
    const refreshed = (await call('/v1/authentication/session-token', { method: 'POST', body: { sessionToken: first.sessionToken } })).body;
    assert.equal(refreshed.userId, first.userId);
    assert.equal((await call(route(1))).status, 401);
    assert.equal((await call('/v1/authentication/session-token', { method: 'POST', body: { sessionToken: 'invalid' } })).status, 401);
    const submit = (identity, score, level = 1) => call(route(level) + '/players/' + identity.userId, { method: 'POST', token: identity.idToken,
      body: { score, metadata: { displayName: 'Test player', baseHp: 20 } } });
    await submit(first, 4000); await submit(first, 100); await submit(other, 5000);
    const page = await call(route(1), { token: first.idToken });
    assert.equal(page.body.total, 2); assert.equal(page.body.results[0].playerId, other.userId);
    assert.equal(page.body.results[1].score, 4000); assert.equal(page.body.results[1].rank, 1);
    assert.equal(page.body.results[1].metadata.displayName, 'Test player');
    assert.equal((await call(route(1).replace('-v1', '-v2'), { token: first.idToken })).body.total, 0, 'Different placement-rules boards must not mix scores');
    assert.equal((await call(route(2), { token: first.idToken })).body.total, 0);
    for (let stage = 2; stage <= 5; stage++) {
      await submit(first, stage * 100, stage);
      assert.equal((await call(route(stage), { token: first.idToken })).body.total, 1);
    }
    assert.equal((await call(route(1) + '/players/' + first.userId, { method: 'POST', token: other.idToken, body: { score: 9000 } })).status, 403);
    assert.equal((await submit(first, -1)).status, 400);
    for (let i = 0; i < 10; i++) {
      const identity = (await call('/v1/authentication/anonymous', { method: 'POST', body: {} })).body; await submit(identity, 6000 + i);
    }
    assert.equal((await call(route(1), { token: first.idToken })).body.results.length, 10);
    assert.equal((await call(route(1) + '/players/' + first.userId, { token: first.idToken })).body.rank, 11);
    await call('/__test/control', { method: 'POST', body: { nextStatus: 429 } });
    const limited = await call(route(1), { token: first.idToken }); assert.equal(limited.status, 429); assert.equal(limited.headers.get('retry-after'), '1');
    const cors = await call(route(1), { method: 'OPTIONS' }); assert.equal(cors.status, 204); assert.equal(cors.headers.get('access-control-allow-origin'), '*');
  } finally { await new Promise(resolve => server.close(resolve)); }
});
