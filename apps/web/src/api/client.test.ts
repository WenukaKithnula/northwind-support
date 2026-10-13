import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { sessionStore } from '../auth/sessionStore';
import { ApiError, request } from './client';

const fetchMock = vi.fn();

function lastHeaders(): Record<string, string> {
  const [, init] = fetchMock.mock.calls.at(-1) as [string, RequestInit];

  return init.headers as Record<string, string>;
}

function signIn() {
  sessionStore.set({
    token: 'the-token',
    expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    agent: { id: 1, fullName: 'Alex Turner', email: 'alex@example.test' },
  });
}

describe('request', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
    // A fresh response for every call: a response body can only be read once.
    fetchMock.mockImplementation(() => Promise.resolve(new Response(JSON.stringify({ ok: true }), { status: 200 })));
  });

  afterEach(() => {
    sessionStore.clear();
    vi.unstubAllGlobals();
    fetchMock.mockReset();
  });

  describe('the token', () => {
    it('is sent with every call while someone is signed in', async () => {
      signIn();

      await request('/tickets');
      expect(lastHeaders().Authorization).toBe('Bearer the-token');

      await request('/customers', { method: 'POST', body: '{}' });
      expect(lastHeaders().Authorization).toBe('Bearer the-token');
    });

    it('is not sent when nobody is signed in', async () => {
      await request('/auth/login', { method: 'POST', body: '{}' });

      expect(lastHeaders().Authorization).toBeUndefined();
    });

    it('is not put anywhere but the header', async () => {
      signIn();

      await request('/tickets?page=1');

      const [url] = fetchMock.mock.calls.at(-1) as [string];
      expect(url).not.toContain('the-token');
    });

    it('keeps the content type alongside it', async () => {
      signIn();

      await request('/tickets');

      expect(lastHeaders()['Content-Type']).toBe('application/json');
    });
  });

  describe('a 401', () => {
    it('ends the session when it answers a call that carried a token', async () => {
      signIn();
      fetchMock.mockResolvedValue(new Response(JSON.stringify({ detail: 'Sign in to use this API.' }), { status: 401 }));

      await expect(request('/tickets')).rejects.toBeInstanceOf(ApiError);

      expect(sessionStore.get()).toBeNull();
    });

    it('still reports the failure to the caller', async () => {
      signIn();
      fetchMock.mockResolvedValue(new Response(JSON.stringify({ detail: 'Sign in to use this API.' }), { status: 401 }));

      await expect(request('/tickets')).rejects.toMatchObject({ status: 401, message: 'Sign in to use this API.' });
    });

    it('does not touch anything for a call that carried no token, like a wrong password at sign-in', async () => {
      fetchMock.mockResolvedValue(new Response(JSON.stringify({ detail: 'Invalid email or password.' }), { status: 401 }));

      await expect(request('/auth/login', { method: 'POST', body: '{}' })).rejects.toMatchObject({
        status: 401,
        message: 'Invalid email or password.',
      });
    });
  });

  describe('other failures', () => {
    it.each([400, 404, 409, 500])('leave the session alone for a %i', async (status) => {
      signIn();
      fetchMock.mockResolvedValue(new Response(JSON.stringify({ detail: 'Nope.' }), { status }));

      await expect(request('/tickets')).rejects.toBeInstanceOf(ApiError);

      expect(sessionStore.get()).not.toBeNull();
    });
  });
});
