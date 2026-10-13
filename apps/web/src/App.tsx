import { Link, NavLink, Outlet } from 'react-router-dom';
import { sessionStore } from './auth/sessionStore';
import { useSession } from './auth/useSession';

/** Application shell: the header, the navigation, who is signed in, and wherever the router puts the page. */
export function App() {
  const session = useSession();

  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">
          Northwind Support
        </Link>

        <nav className="app__nav" aria-label="Main">
          <NavLink to="/tickets">Tickets</NavLink>
          <NavLink to="/customers">Customers</NavLink>
        </nav>

        {session && (
          <div className="app__user">
            <span className="app__user-name">{session.agent.fullName}</span>
            {/* Ending the session is all it takes: the route guard then sends the visitor to the login page. */}
            <button type="button" className="button button--ghost" onClick={sessionStore.clear}>
              Sign out
            </button>
          </div>
        )}
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
