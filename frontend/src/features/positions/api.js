import { http } from '@/shared/api/http';

/**
 * @param {{ search?: string, status?: string, page?: number, pageSize?: number }} params
 * @returns {Promise<{ items: object[], totalCount: number, page: number, pageSize: number }>}
 */
export async function getPositions(params) {
  const { data } = await http.get('/positions', { params });
  return data;
}

/** @param {string} id Detay; `canManage` kullanıcının düzenleme yetkisini söyler. */
export async function getPosition(id) {
  const { data } = await http.get(`/positions/${id}`);
  return data;
}

/**
 * @param {{ title: string, departmentId: string, levelId: string, headcount: number,
 *   jobDescription: string | null, responsibleHrUserId: string | null }} input
 * @returns {Promise<string>}
 */
export async function createPosition(input) {
  const { data } = await http.post('/positions', input);
  return data.id;
}

/**
 * @param {string} id
 * @param {{ title: string, headcount: number, jobDescription: string | null, responsibleHrUserId: string | null }} input
 */
export async function updatePosition(id, input) {
  await http.put(`/positions/${id}`, input);
}

/** @param {string} id @param {'Open' | 'OnHold' | 'Closed' | 'Cancelled'} status */
export async function changePositionStatus(id, status) {
  await http.post(`/positions/${id}/status`, { status });
}
