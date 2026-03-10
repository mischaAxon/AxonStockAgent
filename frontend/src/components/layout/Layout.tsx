import { Outlet, NavLink } from 'react-router-dom';
import { LayoutDashboard, Activity, Eye, Grid2x2, Briefcase, Users, Plug, LogOut, Newspaper } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import { NewsTicker } from '../NewsTicker';

const navItems = [
  { to: '/',          label: 'Dashboard', icon: LayoutDashboard, end: true  },
  { to: '/signals',   label: 'Signalen',  icon: Activity,        end: false },
  { to: '/watchlist', label: 'Watchlist', icon: Eye,             end: false },
  { to: '/sectors',   label: 'Sectoren',  icon: Grid2x2,         end: false },
  { to: '/portfolio', label: 'Portfolio', icon: Briefcase,       end: false },
  { to: '/news',      label: 'Nieuws',    icon: Newspaper,       end: false },
];

const adminItems = [
  { to: '/admin/users',     label: 'Gebruikers', icon: Users },
  { to: '/admin/providers', label: 'Providers',  icon: Plug  },
];

const navLinkClass = (isActive: boolean) =>
  `flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors ${
    isActive
      ? 'bg-axon-600/20 text-axon-400'
      : 'text-gray-400 hover:text-white hover:bg-gray-800'
  }`;

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

        {/* Main nav */}
        <nav className="space-y-1">
          {navItems.map(({ to, label, icon: Icon, end }) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              className={({ isActive }) => navLinkClass(isActive)}
            >
              <Icon size={18} />
              {label}
            </NavLink>
          ))}
        </nav>

        {/* Admin nav */}
        {isAdmin && (
          <div className="mt-6">
            <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider px-3 mb-2">
              Admin
            </p>
            <nav className="space-y-1">
              {adminItems.map(({ to, label, icon: Icon }) => (
                <NavLink
                  key={to}
                  to={to}
                  className={({ isActive }) => navLinkClass(isActive)}
                >
                  <Icon size={18} />
                  {label}
                </NavLink>
              ))}
            </nav>
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
      <main className="ml-64 flex flex-col min-h-screen">
        <NewsTicker />
        <div className="flex-1 p-8">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
