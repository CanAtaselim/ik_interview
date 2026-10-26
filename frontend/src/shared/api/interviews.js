import { http, toApiUrl } from './http';

/**
 * @typedef {'Scheduled' | 'Completed' | 'Cancelled' | 'NoShow'} InterviewStatus
 * @typedef {{ id: string, applicationId: string, candidateId: string, candidateName: string,
 *   positionId: string, positionTitle: string, typeId: string, typeName: string,
 *   mode: 'InPerson' | 'Online', startsAt: string, endsAt: string, durationMinutes: number,
 *   roomId: string | null, roomName: string | null, meetingUrl: string | null,
 *   status: InterviewStatus, interviewers: { userId: string, displayName: string }[],
 *   isMine: boolean, awaitingOutcome: boolean }} Interview
 * @typedef {{ typeId: string, mode: 'InPerson' | 'Online', startsAt: string, durationMinutes: number,
 *   roomId: string | null, meetingUrl: string | null, note: string | null,
 *   interviewerUserIds: string[], confirmConflicts?: boolean }} InterviewPlan
 */

// ASP.NET dizi parametresini "userIds=A&userIds=B" biçiminde bekler.
const arrayParams = { indexes: null };

/**
 * İK hepsini, diğer kullanıcılar mülakatçı olarak atandıklarını görür.
 * @param {{ from?: string, to?: string, interviewerUserId?: string, roomId?: string,
 *   candidateId?: string, positionId?: string, status?: InterviewStatus, mine?: boolean,
 *   awaitingOutcome?: boolean }} params
 * @returns {Promise<Interview[]>}
 */
export async function getInterviews(params) {
  const { data } = await http.get('/interviews', { params });
  return data;
}

/** Mülakat ve adayın kısıtlı görünümü. @param {string} id */
export async function getInterview(id) {
  const { data } = await http.get(`/interviews/${id}`);
  return data;
}

/**
 * Çakışma varsa ve onaylanmadıysa 409 (`code: 'Interview.Conflicts'`, `conflicts`), aday kara
 * listedeyse 409 (`code: 'Interview.BlacklistMatch'`, `blacklistMatches`) döner.
 * @param {InterviewPlan & { applicationId: string, confirmBlacklist?: boolean }} input
 * @returns {Promise<string>}
 */
export async function scheduleInterview(input) {
  const { data } = await http.post('/interviews', input);
  return data.id;
}

/** @param {InterviewPlan & { id: string }} input */
export async function rescheduleInterview({ id, ...plan }) {
  await http.put(`/interviews/${id}`, plan);
}

/** @param {{ id: string, reason: string }} input */
export async function cancelInterview({ id, reason }) {
  await http.post(`/interviews/${id}/cancel`, { reason });
}

/** @param {{ id: string, outcome: 'Completed' | 'NoShow' }} input */
export async function recordInterviewOutcome({ id, outcome }) {
  await http.post(`/interviews/${id}/outcome`, { outcome });
}

/**
 * Planlama için mülakatçıların müsaitlikleri ve dolu saatleri (yalnızca İK).
 * @param {{ from: string, to: string, userIds: string[], roomId?: string | null,
 *   excludeInterviewId?: string | null }} params
 */
export async function getPlanningSchedule(params) {
  const { data } = await http.get('/interviews/schedules', {
    params,
    paramsSerializer: arrayParams,
  });
  return data;
}

/** @param {string} interviewId @param {string} documentId @returns {Promise<string>} Tam adres. */
export async function getInterviewDocumentUrl(interviewId, documentId) {
  const { data } = await http.post(
    `/interviews/${interviewId}/documents/${documentId}/download-link`,
  );
  return toApiUrl(data.url);
}

/**
 * Devam eden süreçleri aday adıyla arar.
 * @param {string} search
 * @returns {Promise<{ applicationId: string, candidateId: string, candidateName: string, positionTitle: string }[]>}
 */
export async function searchActiveApplications(search) {
  const { data } = await http.get('/applications/active', { params: { search } });
  return data;
}

/** @param {{ from: string, to: string }} params @returns {Promise<{ id: string, startsAt: string, endsAt: string }[]>} */
export async function getMyAvailability(params) {
  const { data } = await http.get('/me/availability', { params });
  return data;
}

/** @param {{ startsAt: string, endsAt: string }} input @returns {Promise<string>} */
export async function addAvailability(input) {
  const { data } = await http.post('/me/availability', input);
  return data.id;
}

/** @param {string} id */
export async function deleteAvailability(id) {
  await http.delete(`/me/availability/${id}`);
}
