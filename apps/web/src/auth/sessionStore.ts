import type { LoginResponse } from '../types/api';

/**
 * Where the signed-in session is kept. In `sessionStorage`: it lives for the tab and is gone when
 * the tab is closed, and it is not shared between tabs. The trade-off is that any script running on
 * the page can read it, so a cross-site scripting bug would expose the token; `localStorage` would
 * be no safer and would keep the token after the browser closes. An httpOnly cookie hides it from
 * scripts, but needs the server to set it and protection against cross-site request forgery.
 */
const STORAGE_KEY = 'northwind.session';

export interface SessionStore {
  /** The session, or null when nobody is signed in or the token has expired. */
  get: () => LoginResponse | null;
  set: (session: LoginResponse) => void;
  clear: () => void;
  /** Calls the listener whenever the session changes; returns a function that stops it. */
  subscribe: (listener: () => void) => () => void;
}

// The longest delay setTimeout accepts. A longer wait is simply scheduled again when it fires.
const LONGEST_TIMEOUT_MS = 2 ** 31 - 1;

/**
 * Makes a store over the given storage. Kept as a function so a test can hand it its own storage;
 * the app uses the one made at the bottom of this file.
 */
export function createSessionStore(storage: Storage | undefined): SessionStore {
  const listeners = new Set<() => void>();
  let current = read(storage);
  let expiryTimer: ReturnType<typeof setTimeout> | undefined;

  const notify = () => listeners.forEach((listener) => listener());

  // The session ends by itself when its token expires, whether or not the page is doing anything.
  const scheduleExpiry = () => {
    clearTimeout(expiryTimer);

    if (!current) {
      return;
    }

    const wait = Math.min(Math.max(Date.parse(current.expiresAtUtc) - Date.now(), 0), LONGEST_TIMEOUT_MS);

    expiryTimer = setTimeout(() => (isExpired(current) ? clear() : scheduleExpiry()), wait);
  };

  const clear = () => {
    clearTimeout(expiryTimer);
    current = null;
    write(storage, null);
    notify();
  };

  scheduleExpiry();

  return {
    // Not changing anything here: React calls this while rendering.
    get: () => (current && !isExpired(current) ? current : null),

    set: (session) => {
      current = session;
      write(storage, session);
      scheduleExpiry();
      notify();
    },

    clear,

    subscribe: (listener) => {
      listeners.add(listener);

      return () => listeners.delete(listener);
    },
  };
}

function isExpired(session: LoginResponse | null): boolean {
  return !session || Date.parse(session.expiresAtUtc) <= Date.now();
}

function read(storage: Storage | undefined): LoginResponse | null {
  try {
    const stored = storage?.getItem(STORAGE_KEY);

    if (!stored) {
      return null;
    }

    const parsed: unknown = JSON.parse(stored);

    if (!isSession(parsed) || isExpired(parsed)) {
      storage?.removeItem(STORAGE_KEY);

      return null;
    }

    return parsed;
  } catch {
    // Storage that cannot be read, or text that is not a session, means nobody is signed in.
    return null;
  }
}

function write(storage: Storage | undefined, session: LoginResponse | null): void {
  try {
    if (session) {
      storage?.setItem(STORAGE_KEY, JSON.stringify(session));
    } else {
      storage?.removeItem(STORAGE_KEY);
    }
  } catch {
    // Storage may be full or blocked (a private window). The session still works in memory.
  }
}

function isSession(value: unknown): value is LoginResponse {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const candidate = value as Partial<LoginResponse>;

  return (
    typeof candidate.token === 'string' &&
    typeof candidate.expiresAtUtc === 'string' &&
    typeof candidate.agent?.id === 'number' &&
    typeof candidate.agent.fullName === 'string' &&
    typeof candidate.agent.email === 'string'
  );
}

function browserSessionStorage(): Storage | undefined {
  try {
    return window.sessionStorage;
  } catch {
    return undefined;
  }
}

/** The one session the whole app shares. */
export const sessionStore = createSessionStore(browserSessionStorage());
