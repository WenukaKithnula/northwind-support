import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useSession } from './useSession';

/**
 * Guards every page behind it. Without a session the visitor is sent to the login page, which
 * remembers where they were going, so signing in brings them back. It also catches a session that
 * ends while they are on a page, whether they signed out, the token ran out, or the API said 401.
 */
export function RequireAuth() {
  const session = useSession();
  const location = useLocation();

  if (!session) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  return <Outlet />;
}
