import { Navigate, useLocation } from 'react-router-dom';
import { selectIsAuthenticated, useAuthStore } from '../store/authStore';
import AdminLayout from '../layouts/AdminLayout';

interface ProtectedRouteProps {
  children: React.ReactNode;
}

/**
 * Bảo vệ route admin bằng role trong React Router.
 * Lưu ý: đây KHÔNG phải lớp bảo mật duy nhất — backend vẫn kiểm tra [Authorize(Roles = "Admin")].
 */
export function ProtectedRoute({ children }: ProtectedRouteProps) {
  const isAuthenticated = useAuthStore(selectIsAuthenticated);
  const session = useAuthStore((s) => s.session);
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/admin/login" replace state={{ from: location.pathname }} />;
  }

  const roles = session?.roles ?? [];
  if (!roles.includes('Admin')) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-50 p-6">
        <div className="max-w-md rounded-2xl border border-slate-200 bg-white p-8 text-center shadow-sm">
          <h1 className="text-lg font-semibold text-slate-900">Không có quyền truy cập</h1>
          <p className="mt-2 text-sm text-slate-500">
            Tài khoản của bạn không có vai trò <strong>Admin</strong> nên không thể truy cập khu vực quản trị.
          </p>
        </div>
      </div>
    );
  }

  return <AdminLayout>{children}</AdminLayout>;
}

export default ProtectedRoute;
