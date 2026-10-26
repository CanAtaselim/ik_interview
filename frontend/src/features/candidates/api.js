import { http, toApiUrl } from '@/shared/api/http';

/**
 * @typedef {{ id: string, fullName: string, email: string | null, phone: string | null }} DuplicateCandidate
 * @typedef {{ firstName: string, lastName: string, email: string | null, phone: string | null,
 *   linkedInUrl?: string | null, gitHubUrl?: string | null, city?: string | null,
 *   sourceId?: string | null, tags?: string[], notes?: string | null,
 *   confirmDuplicate?: boolean }} CandidateInput
 */

/**
 * @param {{ search?: string, sourceId?: string, inTalentPool?: boolean, tag?: string,
 *   page?: number, pageSize?: number }} params
 * @returns {Promise<{ items: object[], totalCount: number, page: number, pageSize: number }>}
 */
export async function getCandidates(params) {
  const { data } = await http.get('/candidates', { params });
  return data;
}

/** @param {string} id Aday kartı: profil, dosyalar, ön görüşme kayıtları ve olası mükerrerler. */
export async function getCandidate(id) {
  const { data } = await http.get(`/candidates/${id}`);
  return data;
}

/**
 * @param {{ email?: string, phone?: string, excludeId?: string }} params
 * @returns {Promise<DuplicateCandidate[]>}
 */
export async function findDuplicates(params) {
  const { data } = await http.get('/candidates/duplicates', { params });
  return data;
}

/**
 * Aynı e-posta/telefonla aday varsa ve `confirmDuplicate` verilmediyse 409 döner
 * (`code: 'Candidate.PossibleDuplicate'`, `duplicates` listesiyle).
 * @param {CandidateInput} input @returns {Promise<string>}
 */
export async function createCandidate(input) {
  const { data } = await http.post('/candidates', input);
  return data.id;
}

/** @param {string} id @param {CandidateInput} input */
export async function updateCandidate(id, input) {
  await http.put(`/candidates/${id}`, input);
}

/** @param {string} id @param {boolean} inTalentPool */
export async function setTalentPool(id, inTalentPool) {
  await http.post(`/candidates/${id}/talent-pool`, { inTalentPool });
}

/**
 * CV'den okunabilen bilgiler (dosya saklanmaz). Okunamayan alan `null` gelir.
 * @param {File} file
 * @returns {Promise<{ firstName: string | null, lastName: string | null, email: string | null,
 *   phone: string | null, linkedInUrl: string | null, gitHubUrl: string | null,
 *   city: string | null }>}
 */
export async function prefillFromCv(file) {
  const form = new FormData();
  form.append('file', file);
  const { data } = await http.post('/candidates/cv-prefill', form);
  return data;
}

/**
 * @param {string} id @param {File} file
 * @param {string | null} [typeId] Dosya türü (sözlükte DocumentType); boşsa CV sayılır.
 * @returns {Promise<string>}
 */
export async function uploadDocument(id, file, typeId = null) {
  const form = new FormData();
  form.append('file', file);
  if (typeId) form.append('typeId', typeId);
  const { data } = await http.post(`/candidates/${id}/documents`, form);
  return data.id;
}

/** @param {string} id @param {string} documentId */
export async function deleteDocument(id, documentId) {
  await http.delete(`/candidates/${id}/documents/${documentId}`);
}

/**
 * Dosya için birkaç dakika geçerli indirme adresi alır (erişim sunucuda kayda geçer).
 * @param {string} id @param {string} documentId @returns {Promise<string>} Tam adres.
 */
export async function getDocumentDownloadUrl(id, documentId) {
  const { data } = await http.post(`/candidates/${id}/documents/${documentId}/download-link`);
  return toApiUrl(data.url);
}

/** @param {string} id @returns {Promise<string>} Açılan boş ön görüşme kaydının kimliği. */
export async function startScreening(id) {
  const { data } = await http.post(`/candidates/${id}/screenings`);
  return data.id;
}

/** @param {string} id @param {string} screeningId @param {object} input */
export async function updateScreening(id, screeningId, input) {
  await http.put(`/candidates/${id}/screenings/${screeningId}`, input);
}

/** @param {string} id @param {string} screeningId */
export async function deleteScreening(id, screeningId) {
  await http.delete(`/candidates/${id}/screenings/${screeningId}`);
}

/** @param {string} id */
export async function getTimeline(id) {
  const { data } = await http.get(`/candidates/${id}/timeline`, { params: { pageSize: 100 } });
  return data;
}
