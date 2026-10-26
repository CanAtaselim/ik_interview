import { http } from './http';

/**
 * @typedef {{ entryId: string, candidateId: string, fullName: string, reasonName: string,
 *   kind: 'Exact' | 'Possible' }} BlacklistMatch
 */

/** Görüntüleme sunucuda erişim kaydına yazılır. */
export async function getBlacklistEntries(params) {
  const { data } = await http.get('/blacklist-entries', { params });
  return data;
}

/** Kaydın ayrıntısı (gerekçe dahil); görüntüleme erişim kaydına yazılır. @param {string} id */
export async function getBlacklistEntry(id) {
  const { data } = await http.get(`/blacklist-entries/${id}`);
  return data;
}

/**
 * @param {{ email?: string, phone?: string, firstName?: string, lastName?: string, excludeCandidateId?: string }} params
 * @returns {Promise<BlacklistMatch[]>}
 */
export async function findBlacklistMatches(params) {
  const { data } = await http.get('/blacklist-entries/matches', { params });
  return data;
}

/** @param {{ candidateId: string, reasonId: string, description: string, expiresAt: string | null }} input */
export async function requestBlacklistEntry(input) {
  const { data } = await http.post('/blacklist-entries', input);
  return data.id;
}

/** @param {string} id */
export async function approveBlacklistEntry(id) {
  await http.post(`/blacklist-entries/${id}/approve`);
}

/** @param {{ id: string, reason: string }} input */
export async function removeBlacklistEntry({ id, reason }) {
  await http.post(`/blacklist-entries/${id}/remove`, { reason });
}
