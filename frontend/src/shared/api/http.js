import axios from 'axios';
import { useAuthStore } from '@/shared/auth/authStore';

const baseURL = import.meta.env.VITE_API_URL;

/** Uygulamanın API istemcisi: Authorization başlığını ekler, 401'de oturumu yeniler. */
export const http = axios.create({ baseURL, withCredentials: true });

/**
 * Sunucunun döndürdüğü yolu ("/api/v1/files/…") tam adrese çevirir; API başka bir adresteyse
 * (geliştirme) ona göre tamamlanır.
 * @param {string} path
 */
export function toApiUrl(path) {
  return new URL(path, new URL(baseURL, window.location.origin)).toString();
}

/** Interceptor'sız istemci; yenileme isteği 401 döngüsüne girmesin diye ayrıdır. */
export const authHttp = axios.create({ baseURL, withCredentials: true });

const REFRESH_LOCK = 'recruitment-session-refresh';

// Refresh token her kullanımda değişir. Sekmeler aynı anda yenilerse biri eski token'ı sunar
// ve reddedilir; tarayıcı kilidi yenilemeleri sekmeler arasında sıraya koyar.
function withCrossTabLock(task) {
  return navigator.locks ? navigator.locks.request(REFRESH_LOCK, task) : task();
}

let refreshPromise = null;

/**
 * Refresh cookie'siyle yeni access token alır. Eşzamanlı çağrılar tek isteği paylaşır.
 * Başarısız olursa oturum temizlenir (korumalı sayfalar girişe yönlenir).
 * @returns {Promise<{ accessToken: string, user: import('@/shared/auth/authStore').CurrentUser }>}
 */
export function refreshSession() {
  refreshPromise ??= withCrossTabLock(() => authHttp.post('/auth/refresh'))
    .then(({ data }) => {
      useAuthStore.getState().setSession(data);
      return data;
    })
    .catch((error) => {
      useAuthStore.getState().clearSession();
      throw error;
    })
    .finally(() => {
      refreshPromise = null;
    });

  return refreshPromise;
}

http.interceptors.request.use((config) => {
  const { accessToken } = useAuthStore.getState();
  if (accessToken) config.headers.Authorization = `Bearer ${accessToken}`;
  return config;
});

http.interceptors.response.use(undefined, async (error) => {
  const { config, response } = error;
  if (response?.status !== 401 || !config || config.retriedAfterRefresh) throw error;

  try {
    await refreshSession();
  } catch {
    throw error;
  }

  return http({ ...config, retriedAfterRefresh: true });
});
