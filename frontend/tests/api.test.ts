import { afterEach, test } from 'node:test';
import assert from 'node:assert/strict';
import { createAgent, getPost, listPosts, revokeAgent } from '../src/api.ts';

const originalFetch = globalThis.fetch;
afterEach(() => { globalThis.fetch = originalFetch; });

test('feed preserves filter, continuation and credentials', async () => {
  globalThis.fetch = async (url, init) => {
    assert.equal(url, '/api/posts?filter=activeThreads&continuation=a%2Bb');
    assert.equal(init?.credentials, 'include');
    return Response.json({ items: [], continuation: null });
  };
  assert.deepEqual(await listPosts('activeThreads', 'a+b'), { items: [], continuation: null });
});

test('agent creation sends JSON and reports service errors', async () => {
  globalThis.fetch = async (url, init) => {
    assert.equal(url, '/api/agents');
    assert.equal(init?.method, 'POST');
    assert.deepEqual(JSON.parse(String(init?.body)), { displayName: 'Test', scopes: ['read'] });
    return new Response('denied', { status: 403 });
  };
  await assert.rejects(createAgent('Test', ['read']), /HTTP 403: denied/);
});

test('path segments are encoded and empty revoke succeeds', async () => {
  globalThis.fetch = async (url) => {
    assert.equal(url, '/api/agents/a%2Fb/revoke');
    return new Response(null, { status: 204 });
  };
  assert.equal(await revokeAgent('a/b'), undefined);
  globalThis.fetch = async (url) => {
    assert.equal(url, '/api/posts/a%2Fb');
    return Response.json({ postId: 'a/b' });
  };
  assert.equal((await getPost('a/b')).postId, 'a/b');
});
