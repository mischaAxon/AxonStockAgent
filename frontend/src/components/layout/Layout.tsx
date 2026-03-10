import { Outlet, NavLink } from 'react-router-dom';
import { LayoutDashboard, Activity, Eye, Briefcase } from 'lucide-react';

const navItems = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/signals', label: 'Signalen', icon: Activity },
  { to: '/watchlist', label: 'Watchlist', icon: Eye },
  { to: '/portfolio', label: 'Portfolio', icon: Briefcase },
];

export default function Layout() {
  return (
    <div className="min-h-screen bg-gray-950">
      {/* Sidebar */}
      <aside className="fixed left-0 top-0 h-full w-64 bg-gray-900 border-r border-gray-800 p-6">
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
      </aside>

      {/* Main content */}
      <main className="ml-64 p-8">
        <Outlet />
      </main>
    </div>
  );
}
