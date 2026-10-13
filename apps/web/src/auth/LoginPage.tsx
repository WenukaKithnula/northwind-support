import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { authApi } from '../api/auth';
import { ApiError } from '../api/client';
import { Field } from '../components/Field';
import { sessionStore } from './sessionStore';
import { useSession } from './useSession';

const DEFAULT_LANDING = '/tickets';

/** The same message for every refusal: the API does not say which half was wrong, and neither do we. */
const REFUSED_MESSAGE = 'Invalid email or password.';

interface FormErrors {
  email?: string;
  password?: string;
}

/** Signs an agent in, then returns them to the page they were trying to reach. */
export function LoginPage() {
  const session = useSession();
  const location = useLocation();
  const navigate = useNavigate();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<FormErrors>({});
  const [submitError, setSubmitError] = useState<string>();
  const [isSigningIn, setIsSigningIn] = useState(false);

  // Someone who is already signed in has nothing to do here.
  if (session && !isSigningIn) {
    return <Navigate to={returnPath(location.state)} replace />;
  }

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const problems = validate(email, password);
    setErrors(problems);
    setSubmitError(undefined);

    if (Object.keys(problems).length > 0) {
      return;
    }

    setIsSigningIn(true);

    try {
      sessionStore.set(await authApi.login(email.trim(), password));
      void navigate(returnPath(location.state), { replace: true });
    } catch (caught) {
      setPassword('');
      setSubmitError(
        caught instanceof ApiError && caught.status === 401
          ? REFUSED_MESSAGE
          : 'Could not sign in. Please try again.',
      );
    } finally {
      setIsSigningIn(false);
    }
  };

  return (
    <main className="login">
      <form className="card form login__form" onSubmit={submit} noValidate aria-busy={isSigningIn}>
        <h1>Sign in</h1>
        <p className="field__hint">Northwind Support</p>

        <Field id="login-email" label="Email" error={errors.email}>
          {(fieldProps) => (
            <input
              {...fieldProps}
              type="email"
              autoComplete="username"
              value={email}
              disabled={isSigningIn}
              onChange={(event) => setEmail(event.target.value)}
            />
          )}
        </Field>

        <Field id="login-password" label="Password" error={errors.password}>
          {(fieldProps) => (
            <input
              {...fieldProps}
              type="password"
              autoComplete="current-password"
              value={password}
              disabled={isSigningIn}
              onChange={(event) => setPassword(event.target.value)}
            />
          )}
        </Field>

        {submitError && (
          <p className="field__error" role="alert">
            {submitError}
          </p>
        )}

        <div className="button-row">
          <button type="submit" className="button button--primary" disabled={isSigningIn}>
            {isSigningIn ? 'Signing in...' : 'Sign in'}
          </button>
        </div>
      </form>
    </main>
  );
}

function validate(email: string, password: string): FormErrors {
  const errors: FormErrors = {};
  const trimmed = email.trim();

  if (trimmed.length === 0) {
    errors.email = 'Enter your email address.';
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmed)) {
    errors.email = 'Enter a valid email address.';
  }

  if (password.length === 0) {
    errors.password = 'Enter your password.';
  }

  return errors;
}

/**
 * Where to go once signed in: the page the visitor was turned away from, if there is one and it is
 * a page of this app, otherwise the ticket list. An address that points at another site, or back at
 * the login page, is ignored.
 */
function returnPath(state: unknown): string {
  const from = (state as { from?: { pathname?: unknown; search?: unknown } } | null)?.from;
  const pathname = typeof from?.pathname === 'string' ? from.pathname : undefined;

  if (!pathname || !pathname.startsWith('/') || pathname.startsWith('//') || pathname === '/login') {
    return DEFAULT_LANDING;
  }

  return pathname + (typeof from?.search === 'string' ? from.search : '');
}
