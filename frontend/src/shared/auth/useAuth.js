import { useAuthStore } from './authStore';
import { hasAnyRole } from './roles';

/** Oturum açmış kullanıcı ve rol kontrolü. Gerçek yetki kontrolü her zaman API'dedir. */
export function useAuth() {
  const user = useAuthStore((state) => state.user);
  const status = useAuthStore((state) => state.status);

  return {
    user,
    isAuthenticated: status === 'authenticated',
    /** @param {readonly string[] | null | undefined} roles */
    hasAnyRole: (roles) => hasAnyRole(user, roles),
  };
}
