import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { LoginResponse } from '../types/api';
import { createSessionStore } from './sessionStore';

const KEY = 'northwind.session';

function session(overrides: Partial<LoginResponse> = {}): LoginResponse {
  return {
    token: 'the-token',
    expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    agent: { id: 1, fullName: 'Alex Turner', email: 'alex@example.test' },
    ...overrides,
  };
}

describe('sessionStore', () => {
  beforeEach(() => {
    window.sessionStorage.clear();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('starts signed out', () => {
    expect(createSessionStore(window.sessionStorage).get()).toBeNull();
  });

  it('keeps the session it is given', () => {
    const store = createSessionStore(window.sessionStorage);

    store.set(session());

    expect(store.get()?.token).toBe('the-token');
    expect(store.get()?.agent.fullName).toBe('Alex Turner');
  });

  it('keeps the session across a reload of the page', () => {
    createSessionStore(window.sessionStorage).set(session());

    // A new store over the same storage is what a reload makes.
    const afterReload = createSessionStore(window.sessionStorage);

    expect(afterReload.get()?.token).toBe('the-token');
  });

  it('keeps the session in sessionStorage, which ends with the tab, not in localStorage', () => {
    createSessionStore(window.sessionStorage).set(session());

    expect(window.sessionStorage.getItem(KEY)).not.toBeNull();
    expect(window.localStorage.getItem(KEY)).toBeNull();
  });

  it('forgets the session when cleared, in memory and in storage', () => {
    const store = createSessionStore(window.sessionStorage);
    store.set(session());

    store.clear();

    expect(store.get()).toBeNull();
    expect(window.sessionStorage.getItem(KEY)).toBeNull();
    expect(createSessionStore(window.sessionStorage).get()).toBeNull();
  });

  it('does not accept a session that has already expired', () => {
    window.sessionStorage.setItem(
      KEY,
      JSON.stringify(session({ expiresAtUtc: new Date(Date.now() - 1000).toISOString() })),
    );

    expect(createSessionStore(window.sessionStorage).get()).toBeNull();
  });

  it('ends the session by itself when the token expires', () => {
    vi.useFakeTimers();
    const store = createSessionStore(window.sessionStorage);
    store.set(session({ expiresAtUtc: new Date(Date.now() + 60_000).toISOString() }));

    expect(store.get()).not.toBeNull();

    vi.advanceTimersByTime(60_001);

    expect(store.get()).toBeNull();
    expect(window.sessionStorage.getItem(KEY)).toBeNull();
  });

  it('tells everyone listening when the session changes', () => {
    const store = createSessionStore(window.sessionStorage);
    const listener = vi.fn();
    store.subscribe(listener);

    store.set(session());
    store.clear();

    expect(listener).toHaveBeenCalledTimes(2);
  });

  it('stops telling a listener that has unsubscribed', () => {
    const store = createSessionStore(window.sessionStorage);
    const listener = vi.fn();
    const unsubscribe = store.subscribe(listener);

    unsubscribe();
    store.set(session());

    expect(listener).not.toHaveBeenCalled();
  });

  it.each([
    ['not JSON', 'not json at all'],
    ['the wrong shape', JSON.stringify({ hello: 'world' })],
    ['a token that is not a string', JSON.stringify({ token: 5, expiresAtUtc: 'x', agent: {} })],
  ])('treats stored text that is %s as signed out', (_label, stored) => {
    window.sessionStorage.setItem(KEY, stored);

    expect(createSessionStore(window.sessionStorage).get()).toBeNull();
  });

  it('keeps working in memory when storage is not available', () => {
    const store = createSessionStore(undefined);

    store.set(session());

    expect(store.get()?.token).toBe('the-token');
  });

  it('keeps working in memory when storage refuses to be written to', () => {
    const refusing = {
      getItem: () => null,
      setItem: () => {
        throw new Error('storage is full');
      },
      removeItem: () => undefined,
    } as unknown as Storage;
    const store = createSessionStore(refusing);

    expect(() => store.set(session())).not.toThrow();
    expect(store.get()?.token).toBe('the-token');
  });
});
