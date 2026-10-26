import { http } from './http';

/**
 * @typedef {{ applicationId: string, candidateId: string, candidateName: string, stageId: string,
 *   status: string, stageEnteredAt: string, closingReason: string | null, canUndo: boolean,
 *   cvDecision: import('./cvReviews').CvReviewDecision | null,
 *   nextInterviewAt: string | null }} PipelineCard
 */

/**
 * Pozisyonun süreç hattı: aşamalar, devam eden/işe alınan süreçler (`active`) ve sonlananlar (`closed`).
 * @param {string} positionId
 */
export async function getPositionPipeline(positionId) {
  const { data } = await http.get(`/positions/${positionId}/pipeline`);
  return data;
}

/** @param {string} candidateId Adayın tüm süreçleri, en yenisi başta. */
export async function getCandidateApplications(candidateId) {
  const { data } = await http.get(`/candidates/${candidateId}/applications`);
  return data;
}

/** Adayı pozisyona bağlar. @returns {Promise<string>} */
export async function startApplication({ candidateId, positionId }) {
  const { data } = await http.post('/applications', { candidateId, positionId });
  return data.id;
}

/**
 * Geçiş kurallara uymuyorsa 422 döner; `detail` nedeni söyler. Bekleyen mülakat değerlendirmeleri
 * yüzündense `code: 'Application.ScorecardsPending'` ve `canBypass` gelir; İK Yöneticisi
 * `bypassReason` ile yeniden gönderebilir.
 * @param {{ applicationId: string, stageId: string, bypassReason?: string }} input
 */
export async function moveApplication({ applicationId, stageId, bypassReason }) {
  await http.post(`/applications/${applicationId}/move`, { stageId, bypassReason });
}

/** @param {{ applicationId: string, status: 'Rejected' | 'Withdrawn', rejectionReasonId: string, note?: string | null }} input */
export async function closeApplication({ applicationId, ...body }) {
  await http.post(`/applications/${applicationId}/close`, body);
}

/** Son değişikliği geri alır (10 dakika içinde). @param {string} applicationId */
export async function undoApplicationChange(applicationId) {
  await http.post(`/applications/${applicationId}/undo`);
}

/** Aday eklenebilecek (açık) pozisyonlar. */
export async function getOpenPositions() {
  const { data } = await http.get('/positions', { params: { status: 'Open', pageSize: 100 } });
  return data.items;
}
