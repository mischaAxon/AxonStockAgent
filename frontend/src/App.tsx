import { Routes, Route } from 'react-router-dom';
import Layout from './components/layout/Layout';
import ProtectedRoute from './components/auth/ProtectedRoute';
import DashboardPage from './pages/DashboardPage';
import SignalsPage from './pages/SignalsPage';
import WatchlistPage from './pages/WatchlistPage';
import PortfolioPage from './pages/PortfolioPage';
import SectorsPage from './pages/SectorsPage';
import NewsPage from './pages/NewsPage';
import LoginPage from './pages/LoginPage';
import RegisterPage from './pages/RegisterPage';
import AdminUsersPage from './pages/AdminUsersPage';
import AdminProvidersPage from './pages/AdminProvidersPage';

export default function App() {
  return (
    <Routes>
      {/* Public routes */}
      <Route path="/login"    element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />

      {/* Protected routes */}
      <Route
        element={
          <ProtectedRoute>
            <Layout />
          </ProtectedRoute>
        }
      >
        <Route path="/"          element={<DashboardPage />} />
        <Route path="/signals"   element={<SignalsPage />} />
        <Route path="/watchlist" element={<WatchlistPage />} />
        <Route path="/sectors"   element={<SectorsPage />} />
        <Route path="/portfolio" element={<PortfolioPage />} />
        <Route path="/news"      element={<NewsPage />} />

        {/* Admin-only routes */}
        <Route
          path="/admin/users"
          element={
            <ProtectedRoute requireAdmin>
              <AdminUsersPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/admin/providers"
          element={
            <ProtectedRoute requireAdmin>
              <AdminProvidersPage />
            </ProtectedRoute>
          }
        />
      </Route>
    </Routes>
  );
}
