import { authHttp, http } from '@/shared/api/http';

/**
 * Giriş yapar. 401 burada "şifre hatalı" demektir; oturum yenileme denenmesin diye
 * interceptor'sız istemci kullanılır.
 * @param {{ username: string, password: string }} credentials
 * @returns {Promise<{ accessToken: string, expiresAt: string, user: import('./authStore').CurrentUser }>}
 */
export async function login(credentials) {
  const { data } = await authHttp.post('/auth/login', credentials);
  return data;
}

/** Sunucudaki oturumu (refresh token) kapatır. */
export async function logout() {
  await http.post('/auth/logout');
}
