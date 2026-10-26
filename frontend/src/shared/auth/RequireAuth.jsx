import { Navigate, useLocation } from 'react-router';
import { ROUTES } from '@/shared/constants/routes';
import { useAuthStore } from './authStore';
import { useAuth } from './useAuth';

/**
 * Oturum yoksa girişe yönlendirir. Oturum kendiliğinden düştüyse kullanıcı girişten sonra
 * kaldığı sayfaya döner; bilinçli çıkışta dönmez, çünkü aynı bilgisayarda giriş yapan bir
 * sonraki kişi öncekinin sayfasına düşmemelidir.
 * @param {{ children: import('react').ReactNode }} props
 */
export default function RequireAuth({ children }) {
  const { isAuthenticated } = useAuth();
  const signedOut = useAuthStore((state) => state.signedOut);
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to={ROUTES.login} replace state={signedOut ? null : { from: location }} />;
  }

  return children;
}
