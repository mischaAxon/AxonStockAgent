import { Outlet, NavLink } from 'react-router-dom';
import { LayoutDashboard, Activity, Eye, Briefcase, Users, LogOut } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';

const navItems = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/signals', label: 'Signalen', icon: Activity },
  { to: '/watchlist', label: 'Watchlist', icon: Eye },
  { to: '/portfolio', label: 'Portfolio', icon: Briefcase },
];

export default function Layout() {
  const { user, isAdmin, logout } = useAuth();

  return (
    <div className="min-h-screen bg-gray-950">
      {/* Sidebar */}
      <aside className="fixed left-0 top-0 h-full w-64 bg-gray-900 border-r border-gray-800 p-6 flex flex-col">
        <div className="mb-10">
          <h1 className="text-xl font-bold text-white tracking-tight">
            Axon<span className="text-axon-400">Stock</span>Agent
          </h1>
          <p className="text-xs text-gray-500 mt-1">AI-Powered Screener</p>
        </div>

        <nav className="space-y-1">
          {navItems.map(({ to, label, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              end={to === '/'}
              className={({ isActive }) =>
                `flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors ${
                  isActive
                    ? 'bg-axon-600/20 text-axon-400'
                    : 'text-gray-400 hover:text-white hover:bg-gray-800'
                }`
              }
            >
              <Icon size={18} />
              {label}
            </NavLink>
          ))}
        </nav>

        {isAdmin && (
          <div className="mt-6">
            <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider px-3 mb-2">
              Admin
            </p>
            <NavLink
              to="/admin/users"
              className={({ isActive }) =>
                `flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors ${
                  isActive
                    ? 'bg-axon-600/20 text-axon-400'
                    : 'text-gray-400 hover:text-white hover:bg-gray-800'
                }`
              }
            >
              <Users size={18} />
              Gebruikers
            </NavLink>
          </div>
        )}

        {/* User info + logout */}
        <div className="mt-auto pt-6 border-t border-gray-800">
          <div className="flex items-center gap-3 px-1 mb-3">
            <div className="w-8 h-8 rounded-full bg-axon-600/30 flex items-center justify-center flex-shrink-0">
              <span className="text-axon-400 text-xs font-semibold">
                {user?.displayName?.charAt(0).toUpperCase() ?? '?'}
              </span>
            </div>
            <div className="min-w-0">
              <p className="text-sm font-medium text-white truncate">{user?.displayName}</p>
              <p className="text-xs text-gray-500 truncate capitalize">{user?.role}</p>
            </div>
          </div>
          <button
            onClick={logout}
            className="flex items-center gap-3 w-full px-3 py-2.5 rounded-lg text-sm font-medium text-gray-400 hover:text-white hover:bg-gray-800 transition-colors"
          >
            <LogOut size={18} />
            Uitloggen
          </button>
        </div>
      </aside>

      {/* Main content */}
      <main className="ml-64 p-8">
        <Outlet />
      </main>
    </div>
  );
}
