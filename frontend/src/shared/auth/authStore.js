import { create } from 'zustand';

/**
 * @typedef {{ id: string, username: string, displayName: string, email: string | null,
 *   department: string | null, departmentId: string | null, title: string | null, roles: string[], scope: string }} CurrentUser
 * @typedef {'unknown' | 'authenticated' | 'anonymous'} AuthStatus
 */

const ANONYMOUS = { status: 'anonymous', accessToken: null, user: null };

/**
 * Oturum durumu. Access token yalnızca bellekte tutulur, localStorage'a yazılmaz;
 * sayfa yenilenince refresh cookie'siyle yeniden alınır.
 */
export const useAuthStore = create((set) => ({
  /** @type {AuthStatus} */
  status: 'unknown',
  /** @type {string | null} */
  accessToken: null,
  /** @type {CurrentUser | null} */
  user: null,
  /** Kullanıcı kendi isteğiyle mi çıktı? (Oturumun düşmesinden ayırt etmek için.) */
  signedOut: false,

  /** @param {{ accessToken: string, user: CurrentUser }} session */
  setSession: ({ accessToken, user }) =>
    set({ status: 'authenticated', accessToken, user, signedOut: false }),

  /** Oturum düştü (süresi doldu, iptal edildi); kullanıcı girişten sonra kaldığı sayfaya döner. */
  clearSession: () => set({ ...ANONYMOUS, signedOut: false }),

  /** Kullanıcı çıkış yaptı; bir sonraki giriş ana sayfadan başlar. */
  signOut: () => set({ ...ANONYMOUS, signedOut: true }),
}));
