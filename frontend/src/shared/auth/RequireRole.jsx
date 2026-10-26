import ForbiddenPage from '@/shared/pages/ForbiddenPage';
import { useAuth } from './useAuth';

/**
 * Kullanıcı verilen rollerden birine sahip değilse sayfa yerine 403 gösterir.
 * @param {{ roles?: readonly string[] | null, children: import('react').ReactNode }} props
 */
export default function RequireRole({ roles, children }) {
  const { hasAnyRole } = useAuth();

  return hasAnyRole(roles) ? children : <ForbiddenPage />;
}
