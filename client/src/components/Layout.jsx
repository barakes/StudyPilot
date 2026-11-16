import { Link, NavLink, Outlet } from "react-router";
import { useAuth } from "../hooks/useAuth";

export function Layout() {
  const { user, logout } = useAuth();

  return (
    <>
      <header className="topbar">
        <div className="topbar-inner">
          <Link to="/" className="logo">
            📘 StudyPilot
          </Link>

          <nav className="nav-links">
            <NavLink to="/" end>
              המסמכים שלי
            </NavLink>
            <NavLink to="/history">ההתקדמות שלי</NavLink>
            <span className="muted nav-greeting">
              שלום, {user?.displayName}
            </span>
            <button type="button" className="btn-link" onClick={logout}>
              התנתקות
            </button>
          </nav>
        </div>
      </header>

      <main className="container">
        <Outlet />
      </main>
    </>
  );
}
