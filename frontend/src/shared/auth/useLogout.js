import { useMutation, useQueryClient } from '@tanstack/react-query';
import { logout } from './api';
import { useAuthStore } from './authStore';

/** Çıkış yapar. Sunucu isteği başarısız olsa bile bu tarayıcıdaki oturum kapatılır. */
export function useLogout() {
  const queryClient = useQueryClient();
  const signOut = useAuthStore((state) => state.signOut);

  return useMutation({
    mutationFn: logout,
    onSettled: () => {
      signOut();
      // Sonraki kullanıcı öncekinin verisini görmesin.
      queryClient.clear();
    },
  });
}
