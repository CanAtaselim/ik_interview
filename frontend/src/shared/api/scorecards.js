import { http } from './http';

/**
 * @typedef {'StrongHire' | 'Hire' | 'Undecided' | 'NoHire' | 'StrongNoHire'} Recommendation
 * @typedef {{ id: string, questionId: string | null, text: string, guidance: string | null,
 *   competencyId: string | null, competencyName: string | null, weight: number, score: number | null,
 *   note: string | null, isCustom: boolean }} ScorecardItem
 * @typedef {{ id: string | null, text?: string | null, competencyId?: string | null,
 *   weight?: number | null, score: number | null, note: string | null }} ScorecardItemInput
 */

/** Mülakatçının değerlendirmesini açar (yoksa oluşturur). @param {string} interviewId @returns {Promise<string>} */
export async function startScorecard(interviewId) {
  const { data } = await http.post(`/interviews/${interviewId}/scorecard`);
  return data.id;
}

/** @param {string} id */
export async function getScorecard(id) {
  const { data } = await http.get(`/scorecards/${id}`);
  return data;
}

/** Taslağı kaydeder. @param {{ id: string, items: ScorecardItemInput[], generalNote: string | null, recommendation: Recommendation | null }} input */
export async function updateScorecard({ id, ...body }) {
  await http.put(`/scorecards/${id}`, body);
}

/** @param {string} id */
export async function submitScorecard(id) {
  await http.post(`/scorecards/${id}/submit`);
}

/** Yalnızca İK Yöneticisi. @param {{ id: string, reason: string }} input */
export async function reopenScorecard({ id, reason }) {
  await http.post(`/scorecards/${id}/reopen`, { reason });
}

/** Mülakatın değerlendirmeleri (bağımsız değerlendirme kuralına göre süzülmüş). @param {string} interviewId */
export async function getInterviewScorecards(interviewId) {
  const { data } = await http.get(`/interviews/${interviewId}/scorecards`);
  return data;
}

/** @param {{ interviewId: string, decision: 'Proceed' | 'Hold' | 'Reject', reason: string }} input */
export async function recordDebrief({ interviewId, ...body }) {
  await http.post(`/interviews/${interviewId}/debrief`, body);
}

/** @param {string} positionId */
export async function getPositionComparison(positionId) {
  const { data } = await http.get(`/positions/${positionId}/comparison`);
  return data;
}
