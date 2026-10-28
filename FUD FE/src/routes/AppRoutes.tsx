import { Navigate, Route, Routes } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';
import ProtectedRoute from './ProtectedRoute';
import AdminLoginPage from '../pages/admin/AdminLoginPage';
import AccountListPage from '../pages/admin/AccountListPage';
import CreateAccountPage from '../pages/admin/CreateAccountPage';
import App from '../App';

export function AppRoutes() {
  return (
    <>
      <Toaster position="top-right" toastOptions={{ duration: 4000 }} />
      <Routes>
        {/* Trang chủ */}
        <Route path="/" element={<App />} />

        {/* Admin area */}
        <Route path="/admin/login" element={<AdminLoginPage />} />
        <Route
          path="/admin/accounts"
          element={
            <ProtectedRoute>
              <AccountListPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/admin/accounts/new"
          element={
            <ProtectedRoute>
              <CreateAccountPage />
            </ProtectedRoute>
          }
        />
        <Route path="/admin" element={<Navigate to="/admin/accounts" replace />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </>
  );
}

export default AppRoutes;
