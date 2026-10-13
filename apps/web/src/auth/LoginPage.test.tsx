import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, type To } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/client';
import { authApi } from '../api/auth';
import type { LoginResponse } from '../types/api';
import { LoginPage } from './LoginPage';
import { sessionStore } from './sessionStore';

vi.mock('../api/auth', () => ({
  authApi: {
    login: vi.fn(),
  },
}));

const login = vi.mocked(authApi.login);

const signedIn: LoginResponse = {
  token: 'the-token',
  expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  agent: { id: 1, fullName: 'Alex Turner', email: 'alex.turner@northwind-support.example' },
};

function setUp(entry: To | { pathname: string; state: unknown } = '/login') {
  render(
    <MemoryRouter initialEntries={[entry as To]}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/tickets" element={<p>The ticket list</p>} />
        <Route path="/tickets/7" element={<p>Ticket seven</p>} />
      </Routes>
    </MemoryRouter>,
  );

  return userEvent.setup();
}

async function fillIn(
  user: ReturnType<typeof userEvent.setup>,
  email = 'alex.turner@northwind-support.example',
  password = 'LocalDevOnly-Password-1',
) {
  await user.type(screen.getByLabelText('Email'), email);
  await user.type(screen.getByLabelText('Password'), password);
}

describe('LoginPage', () => {
  beforeEach(() => {
    sessionStore.clear();
  });

  afterEach(() => {
    sessionStore.clear();
    vi.clearAllMocks();
  });

  describe('the form', () => {
    it('has an email and a password field, each with a label', () => {
      setUp();

      expect(screen.getByLabelText('Email')).toBeInTheDocument();
      expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'password');
      expect(screen.getByRole('button', { name: 'Sign in' })).toBeInTheDocument();
    });

    it('tells the browser which fields hold a username and a password, so a password manager can help', () => {
      setUp();

      expect(screen.getByLabelText('Email')).toHaveAttribute('autocomplete', 'username');
      expect(screen.getByLabelText('Password')).toHaveAttribute('autocomplete', 'current-password');
    });
  });

  describe('validation', () => {
    it('asks for both fields when they are empty, and does not call the API', async () => {
      const user = setUp();

      await user.click(screen.getByRole('button', { name: 'Sign in' }));

      expect(screen.getByText('Enter your email address.')).toBeInTheDocument();
      expect(screen.getByText('Enter your password.')).toBeInTheDocument();
      expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true');
      expect(login).not.toHaveBeenCalled();
    });

    it('rejects an email that is not one, without calling the API', async () => {
      const user = setUp();

      await fillIn(user, 'not-an-email', 'a-password');
      await user.click(screen.getByRole('button', { name: 'Sign in' }));

      expect(screen.getByText('Enter a valid email address.')).toBeInTheDocument();
      expect(login).not.toHaveBeenCalled();
    });

    it('does not count spaces as an email', async () => {
      const user = setUp();

      await fillIn(user, '   ', 'a-password');
      await user.click(screen.getByRole('button', { name: 'Sign in' }));

      expect(screen.getByText('Enter your email address.')).toBeInTheDocument();
      expect(login).not.toHaveBeenCalled();
    });
  });

  describe('signing in', () => {
    it('sends the email, trimmed, and the password as typed', async () => {
      login.mockResolvedValue(signedIn);
      const user = setUp();

      await fillIn(user, '  alex.turner@northwind-support.example  ', ' a password with spaces ');
      await user.click(screen.getByRole('button', { name: 'Sign in' }));

      await waitFor(() =>
        expect(login).toHaveBeenCalledWith('alex.turner@northwind-support.example', ' a password with spaces '),
      );
    });

    it('shows a pending state while it waits, and cannot be sent twice', async () => {
      let finish: (value: LoginResponse) => void = () => undefined;
      login.mockReturnValue(new Promise((resolve) => (finish = resolve)));
      const user = setUp();

      await fillIn(user);
      await user.click(screen.getByRole('button', { name: 'Sign in' }));

      const pending = await screen.findByRole('button', { name: 'Signing in...' });
      expect(pending).toBeDisabled();

      await user.click(pending);
      expect(login).toHaveBeenCalledTimes(1);

      finish(signedIn);
      await screen.findByText('The ticket list');
    });

    it('keeps the session and goes to the ticket list', async () => {
      login.mockResolvedValue(signedIn);
      const user = setUp();

      await fillIn(user);
      await user.click(screen.getByRole('button', { name: 'Sign in' }));

      expect(await screen.findByText('The ticket list')).toBeInTheDocument();
      expect(sessionStore.get()?.token).toBe('the-token');
      expect(sessionStore.get()?.agent.fullName).toBe('Alex Turner');
    });

    it('goes back to the page the visitor was trying to reach', async () => {
      login.mockResolvedValue(signedIn);
      const user = setUp({ pathname: '/login', state: { from: { pathname: '/tickets/7', search: '' } } });

      await fillIn(user);
      await user.click(screen.getByRole('button', { name: 'Sign in' }));

      expect(await screen.findByText('Ticket seven')).toBeInTheDocument();
    });

    it('does not follow a return address that points back at the login page or off the site', async () => {
      login.mockResolvedValue(signedIn);
      const user = setUp({ pathname: '/login', state: { from: { pathname: '//evil.example.test/steal', search: '' } } });

      await fillIn(user);
      await user.click(screen.getByRole('button', { name: 'Sign in' }));

      expect(await screen.findByText('The ticket list')).toBeInTheDocument();
    });
  });

  describe('when the credentials are refused', () => {
    it('shows one clear message, whichever half was wrong, and keeps nobody signed in', async () => {
      login.mockRejectedValue(new ApiError(401, 'Invalid email or password.'));
      const user = setUp();

      await fillIn(user);
      await user.click(screen.getByRole('button', { name: 'Sign in' }));

      expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.');
      expect(sessionStore.get()).toBeNull();
      expect(screen.getByRole('button', { name: 'Sign in' })).toBeEnabled();
    });

    it('keeps the email that was typed and clears the password', async () => {
      login.mockRejectedValue(new ApiError(401, 'Invalid email or password.'));
      const user = setUp();

      await fillIn(user);
      await user.click(screen.getByRole('button', { name: 'Sign in' }));
      await screen.findByRole('alert');

      expect(screen.getByLabelText('Email')).toHaveValue('alex.turner@northwind-support.example');
      expect(screen.getByLabelText('Password')).toHaveValue('');
    });

    it('says the sign-in itself failed when the server could not be reached', async () => {
      login.mockRejectedValue(new TypeError('Failed to fetch'));
      const user = setUp();

      await fillIn(user);
      await user.click(screen.getByRole('button', { name: 'Sign in' }));

      expect(await screen.findByRole('alert')).toHaveTextContent('Could not sign in. Please try again.');
    });
  });

  it('sends someone who is already signed in straight to the ticket list', () => {
    sessionStore.set(signedIn);

    setUp();

    expect(screen.getByText('The ticket list')).toBeInTheDocument();
  });
});
