import { NavLink } from 'react-router-dom';

function navClass({ isActive }: { isActive: boolean }) {
  return isActive ? 'nav-link active' : 'nav-link';
}

export function Layout({ children }: { children: React.ReactNode }) {
  return (
    <div className="app-shell">
      <header className="top-bar">
        <NavLink to="/" className="brand">
          APIM Replica
        </NavLink>
      </header>
      <div className="body-row">
        <nav className="sidebar">
          <NavLink to="/" className={navClass} end>
            APIs
          </NavLink>
          <NavLink to="/add" className={navClass}>
            Register
          </NavLink>
        </nav>
        <main className="main-content">{children}</main>
      </div>
    </div>
  );
}
