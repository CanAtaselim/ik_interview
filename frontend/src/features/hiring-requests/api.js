import { http } from '@/shared/api/http';

/**
 * @typedef {{ id: string, title: string, departmentName: string, levelName: string, headcount: number,
 *   urgency: string, status: string, awaitingRole: string | null, requestedByName: string,
 *   createdAt: string }} HiringRequestListItem
 * @typedef {{ title: string, departmentId: string, levelId: string, headcount: number, reason: string,
 *   replacedEmployeeName: string | null, urgency: string, expectedStartDate: string | null,
 *   competencies: string[], description: string | null }} HiringRequestInput
 */

/**
 * @param {{ search?: string, status?: string, awaitingMyDecision?: boolean, page?: number, pageSize?: number }} params
 * @returns {Promise<{ items: HiringRequestListItem[], totalCount: number, page: number, pageSize: number }>}
 */
export async function getHiringRequests(params) {
  const { data } = await http.get('/hiring-requests', { params });
  return data;
}

/** @param {string} id Detay; adım geçmişi ve kullanıcının yapabilecekleri (`permissions`) dahil. */
export async function getHiringRequest(id) {
  const { data } = await http.get(`/hiring-requests/${id}`);
  return data;
}

/** @param {HiringRequestInput} input @returns {Promise<string>} Oluşan taslağın kimliği. */
export async function createHiringRequest(input) {
  const { data } = await http.post('/hiring-requests', input);
  return data.id;
}

/** @param {string} id @param {HiringRequestInput} input */
export async function updateHiringRequest(id, input) {
  await http.put(`/hiring-requests/${id}`, input);
}

/** @param {string} id */
export async function submitHiringRequest(id) {
  await http.post(`/hiring-requests/${id}/submit`);
}

/** @param {string} id @param {string} [note] */
export async function approveHiringRequest(id, note) {
  await http.post(`/hiring-requests/${id}/approve`, { note: note || null });
}

/** @param {string} id @param {string} reason */
export async function rejectHiringRequest(id, reason) {
  await http.post(`/hiring-requests/${id}/reject`, { reason });
}

/** @param {string} id */
export async function cancelHiringRequest(id) {
  await http.post(`/hiring-requests/${id}/cancel`);
}
