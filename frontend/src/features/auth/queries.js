import { useMutation } from '@tanstack/react-query';
import { login } from '@/shared/auth/api';
import { useAuthStore } from '@/shared/auth/authStore';

/** Giriş yapar; başarılıysa oturumu belleğe yazar. */
export function useLogin() {
  const setSession = useAuthStore((state) => state.setSession);

  return useMutation({ mutationFn: login, onSuccess: setSession });
}
