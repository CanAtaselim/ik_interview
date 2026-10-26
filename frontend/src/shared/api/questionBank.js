import { http } from './http';

/**
 * @typedef {'Technical' | 'Behavioral' | 'Case'} QuestionType
 * @typedef {{ id: string, departmentId: string, departmentName: string, positionTitle: string | null,
 *   levelId: string | null, levelName: string | null, competencyId: string, competencyName: string,
 *   type: QuestionType, difficulty: number, weight: number, text: string, guidance: string | null,
 *   isActive: boolean, version: number, templateCount: number, canManage: boolean }} Question
 * @typedef {{ departmentId: string, positionTitle?: string | null, levelId?: string | null,
 *   competencyId: string, type: QuestionType, difficulty: number, weight: number, text: string,
 *   guidance?: string | null }} QuestionInput
 * @typedef {{ id: string, name: string, departmentId: string, departmentName: string,
 *   levelId: string | null, levelName: string | null, interviewTypeId: string | null,
 *   interviewTypeName: string | null, isActive: boolean, questionCount: number,
 *   canManage: boolean }} InterviewTemplate
 * @typedef {{ name: string, departmentId: string, levelId?: string | null,
 *   interviewTypeId?: string | null, questionIds: string[] }} InterviewTemplateInput
 */

/** İK tüm birimleri, takım lideri kendi birimini görür. */
export async function getQuestions(params) {
  const { data } = await http.get('/questions', { params });
  return data;
}

/** Soru ve sürüm geçmişi. @param {string} id */
export async function getQuestion(id) {
  const { data } = await http.get(`/questions/${id}`);
  return data;
}

/** @param {QuestionInput} input @returns {Promise<string>} */
export async function createQuestion(input) {
  const { data } = await http.post('/questions', input);
  return data.id;
}

/** İçerik değiştiyse yeni sürüm oluşur. @param {QuestionInput & { id: string }} input */
export async function updateQuestion({ id, ...input }) {
  await http.put(`/questions/${id}`, input);
}

/** @param {{ id: string, isActive: boolean }} input */
export async function setQuestionActive({ id, isActive }) {
  await http.post(`/questions/${id}/active`, { isActive });
}

/** Şablonda kullanılan soru silinemez (422, `code: 'Question.InUse'`). @param {string} id */
export async function deleteQuestion(id) {
  await http.delete(`/questions/${id}`);
}

/** @param {{ departmentId?: string, isActive?: boolean, search?: string }} params @returns {Promise<InterviewTemplate[]>} */
export async function getInterviewTemplates(params) {
  const { data } = await http.get('/interview-templates', { params });
  return data;
}

/** Şablon ve sıralı soruları. @param {string} id */
export async function getInterviewTemplate(id) {
  const { data } = await http.get(`/interview-templates/${id}`);
  return data;
}

/** @param {InterviewTemplateInput} input @returns {Promise<string>} */
export async function createInterviewTemplate(input) {
  const { data } = await http.post('/interview-templates', input);
  return data.id;
}

/** @param {InterviewTemplateInput & { id: string }} input */
export async function updateInterviewTemplate({ id, ...input }) {
  await http.put(`/interview-templates/${id}`, input);
}

/** @param {{ id: string, isActive: boolean }} input */
export async function setInterviewTemplateActive({ id, isActive }) {
  await http.post(`/interview-templates/${id}/active`, { isActive });
}

/** Pozisyonun mülakat şablonları (yalnızca İK). @param {{ positionId: string, templateIds: string[] }} input */
export async function setPositionInterviewTemplates({ positionId, templateIds }) {
  await http.put(`/positions/${positionId}/interview-templates`, { templateIds });
}
