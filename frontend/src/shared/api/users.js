import { http } from './http';

/**
 * Kullanıcı seçim listeleri için arama. Yalnızca uygulamaya en az bir kez giriş yapmış kullanıcılar döner.
 * @param {{ roles?: string[], search?: string, pageSize?: number }} params
 * @returns {Promise<{ items: { id: string, displayName: string, title: string | null, department: string | null }[] }>}
 */
export async function searchUsers({ roles, search, pageSize = 100 } = {}) {
  const { data } = await http.get('/users', {
    params: { roles, search, pageSize },
    // ASP.NET dizi parametresini "roles=A&roles=B" biçiminde bekler.
    paramsSerializer: { indexes: null },
  });
  return data;
}
