import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RouterProvider, createMemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { appRoutes } from '../routes';
import { page, ticketDetail } from '../test/fixtures';
import type { LoginResponse } from '../types/api';
import { sessionStore } from './sessionStore';

const signedIn: LoginResponse = {
  token: 'the-token',
  expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  agent: { id: 1, fullName: 'Alex Turner', email: 'alex.turner@northwind-support.example' },
};

type Handler = (init?: RequestInit) => Response;

const json = (body: unknown, status = 200): Response => new Response(JSON.stringify(body), { status });

/** Answers the API calls the screens make, by path. A test overrides only what it cares about. */
function stubApi(overrides: Record<string, Handler> = {}) {
  const handlers: Record<string, Handler> = {
    '/api/tickets': () => json(page([])),
    '/api/tickets/7': () => json(ticketDetail({ id: 7, title: 'Ticket seven title' })),
    '/api/tickets/7/escalations': () => json([]),
    '/api/agents': () => json([]),
    '/api/categories': () => json([]),
    '/api/customers': () => json([]),
    '/api/auth/login': () => json(signedIn),
    ...overrides,
  };

  const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
    const path = new URL(input, 'http://localhost').pathname;
    const handler = handlers[path];

    return handler ? handler(init) : json({ title: 'Not Found' }, 404);
  });

  vi.stubGlobal('fetch', fetchMock);

  return fetchMock;
}

function renderApp(path: string) {
  const router = createMemoryRouter(appRoutes, { initialEntries: [path] });

  render(<RouterProvider router={router} />);

  return { router, user: userEvent.setup() };
}

describe('authentication in the app', () => {
  beforeEach(() => {
    sessionStore.clear();
  });

  afterEach(() => {
    sessionStore.clear();
    vi.unstubAllGlobals();
  });

  describe('a visitor who is not signed in', () => {
    it.each(['/', '/tickets', '/tickets/7', '/tickets/new', '/customers', '/customers/3', '/no/such/page'])(
      'is sent to the login page from %s',
      async (path) => {
        stubApi();

        const { router } = renderApp(path);

        expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
        expect(router.state.location.pathname).toBe('/login');
      },
    );

    it('is sent to the login page without any data being requested first', async () => {
      const fetchMock = stubApi();

      renderApp('/tickets');
      await screen.findByRole('heading', { name: 'Sign in' });

      expect(fetchMock).not.toHaveBeenCalled();
    });

    it('lands on the page they wanted after signing in', async () => {
      stubApi();
      const { user } = renderApp('/tickets/7');

      await user.type(await screen.findByLabelText('Email'), 'alex.turner@northwind-support.example');
      await user.type(screen.getByLabelText('Password'), 'LocalDevOnly-Password-1');
      await user.click(screen.getByRole('button', { name: 'Sign in' }));

      expect(await screen.findByRole('heading', { name: 'Ticket seven title' })).toBeInTheDocument();
    });

    it('lands on the ticket list after signing in from the login page itself', async () => {
      stubApi();
      const { user, router } = renderApp('/login');

      await user.type(await screen.findByLabelText('Email'), 'alex.turner@northwind-support.example');
      await user.type(screen.getByLabelText('Password'), 'LocalDevOnly-Password-1');
      await user.click(screen.getByRole('button', { name: 'Sign in' }));

      await waitFor(() => expect(router.state.location.pathname).toBe('/tickets'));
    });

    it('sends the token with the calls made after signing in', async () => {
      const fetchMock = stubApi();
      const { user } = renderApp('/tickets');

      await user.type(await screen.findByLabelText('Email'), 'alex.turner@northwind-support.example');
      await user.type(screen.getByLabelText('Password'), 'LocalDevOnly-Password-1');
      await user.click(screen.getByRole('button', { name: 'Sign in' }));
      await screen.findByRole('heading', { name: 'Tickets' });

      const listCall = fetchMock.mock.calls.find(([url]) => new URL(url, 'http://localhost').pathname === '/api/tickets');
      const headers = (listCall?.[1] as RequestInit).headers as Record<string, string>;

      expect(headers.Authorization).toBe('Bearer the-token');
    });
  });

  describe('a signed-in agent', () => {
    beforeEach(() => {
      sessionStore.set(signedIn);
    });

    it('sees the ticket list, not the login page', async () => {
      stubApi();

      renderApp('/tickets');

      expect(await screen.findByRole('heading', { name: 'Tickets' })).toBeInTheDocument();
    });

    it('sees their own name in the header', async () => {
      stubApi();

      renderApp('/tickets');

      expect(await screen.findByText('Alex Turner')).toBeInTheDocument();
    });

    it('can sign out, which clears the session and returns to the login page', async () => {
      stubApi();
      const { user, router } = renderApp('/tickets');

      await user.click(await screen.findByRole('button', { name: 'Sign out' }));

      expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
      expect(router.state.location.pathname).toBe('/login');
      expect(sessionStore.get()).toBeNull();
    });

    it('is taken back to the page they were on when they sign out and in again', async () => {
      stubApi();
      const { user, router } = renderApp('/tickets/7');

      await user.click(await screen.findByRole('button', { name: 'Sign out' }));
      await user.type(await screen.findByLabelText('Email'), 'alex.turner@northwind-support.example');
      await user.type(screen.getByLabelText('Password'), 'LocalDevOnly-Password-1');
      await user.click(screen.getByRole('button', { name: 'Sign in' }));

      await waitFor(() => expect(router.state.location.pathname).toBe('/tickets/7'));
    });

    it('is sent back to the login page when a call comes back 401, as when the token has expired', async () => {
      stubApi({ '/api/tickets': () => json({ title: 'Unauthorized', status: 401, detail: 'Sign in to use this API.' }, 401) });

      const { router } = renderApp('/tickets');

      expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
      expect(router.state.location.pathname).toBe('/login');
      expect(sessionStore.get()).toBeNull();
    });

    it('is taken back to the page they were on after signing in again following an expired session', async () => {
      stubApi({ '/api/tickets/7': () => json({ status: 401 }, 401) });
      const { user } = renderApp('/tickets/7');

      await screen.findByRole('heading', { name: 'Sign in' });

      stubApi();
      await user.type(screen.getByLabelText('Email'), 'alex.turner@northwind-support.example');
      await user.type(screen.getByLabelText('Password'), 'LocalDevOnly-Password-1');
      await user.click(screen.getByRole('button', { name: 'Sign in' }));

      expect(await screen.findByRole('heading', { name: 'Ticket seven title' })).toBeInTheDocument();
    });
  });
});
