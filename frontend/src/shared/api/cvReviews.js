import { http, toApiUrl } from './http';

/**
 * @typedef {'Pending' | 'Positive' | 'Negative' | 'OnHold'} CvReviewDecision
 * @typedef {{ id: string, candidateId: string, candidateName: string, applicationId: string | null,
 *   positionId: string | null, positionTitle: string | null, departmentName: string | null,
 *   reviewerName: string, suggestedByName: string, suggestionNote: string | null,
 *   decision: CvReviewDecision, decisionReason: string | null, rejectionReasonName: string | null,
 *   suggestedAt: string, dueAt: string, decidedAt: string | null, isOverdue: boolean,
 *   canDecide: boolean }} CvReview
 */

/**
 * İK tüm değerlendirmeleri, takım lideri kendisine veya birimine önerilenleri görür.
 * @param {{ status?: 'Waiting' | 'Decided' | 'Overdue', mine?: boolean, candidateId?: string,
 *   search?: string, page?: number, pageSize?: number }} params
 * @returns {Promise<{ items: CvReview[], totalCount: number, page: number, pageSize: number }>}
 */
export async function getCvReviews(params) {
  const { data } = await http.get('/cv-reviews', { params });
  return data;
}

/**
 * Değerlendirme ve adayın kısıtlı görünümü (ücret, İK yorumu ve iletişim bilgisi içermez).
 * @param {string} id
 * @returns {Promise<{ review: CvReview, candidate: object }>}
 */
export async function getCvReview(id) {
  const { data } = await http.get(`/cv-reviews/${id}`);
  return data;
}

/**
 * Aday kara listedeyse ve `confirmBlacklist` verilmediyse 409 döner
 * (`code: 'CvReview.BlacklistMatch'`, `blacklistMatches` listesiyle).
 * @param {{ candidateId: string, applicationId?: string | null, reviewerUserIds: string[],
 *   note?: string | null, confirmBlacklist?: boolean }} input
 * @returns {Promise<string[]>} Açılan değerlendirmelerin kimlikleri.
 */
export async function suggestCandidate(input) {
  const { data } = await http.post('/cv-reviews', input);
  return data.reviewIds;
}

/** @param {{ id: string, decision: CvReviewDecision, reason: string, rejectionReasonId?: string | null }} input */
export async function decideCvReview({ id, ...body }) {
  await http.post(`/cv-reviews/${id}/decide`, body);
}

/**
 * Adayın dosyası için birkaç dakika geçerli indirme adresi (erişim sunucuda kayda geçer).
 * @param {string} reviewId @param {string} documentId @returns {Promise<string>} Tam adres.
 */
export async function getReviewDocumentUrl(reviewId, documentId) {
  const { data } = await http.post(`/cv-reviews/${reviewId}/documents/${documentId}/download-link`);
  return toApiUrl(data.url);
}
