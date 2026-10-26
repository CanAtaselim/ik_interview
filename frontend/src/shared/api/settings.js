import { http } from './http';

/**
 * @typedef {{ id: string, code: string, name: string, subject: string, body: string,
 *   variables: string, isModified: boolean }} EmailTemplate
 * @typedef {{ id: string, name: string, kind: string, order: number, isOptional: boolean,
 *   canBeOptional: boolean, activeCount: number }} StageSetting
 * @typedef {{ id: string, name: string, date: string, isRecurring: boolean }} Holiday
 * @typedef {{ id: string, groupName: string, role: string }} RoleMapping
 */

/** @returns {Promise<EmailTemplate[]>} */
export async function getEmailTemplates() {
  const { data } = await http.get('/settings/email-templates');
  return data;
}

/**
 * Tanımsız bir alan ({Yanlis}) varsa 400 döner; `errors.subject` / `errors.body` hangi alanlar
 * kullanılabileceğini söyler.
 * @param {{ id: string, subject: string, body: string }} input
 */
export async function updateEmailTemplate({ id, ...body }) {
  await http.put(`/settings/email-templates/${id}`, body);
}

/** Şablonu varsayılan metnine döndürür. @param {string} id */
export async function resetEmailTemplate(id) {
  await http.post(`/settings/email-templates/${id}/reset`);
}

/**
 * Kaydedilmemiş metnin örnek verilerle önizlemesi.
 * @param {{ id: string, subject: string, body: string }} input
 * @returns {Promise<{ subject: string, html: string }>}
 */
export async function previewEmailTemplate({ id, ...body }) {
  const { data } = await http.post(`/settings/email-templates/${id}/preview`, body);
  return data;
}

/** @returns {Promise<{ roles: string[], allowedRoles: string[] }>} */
export async function getApprovalFlow() {
  const { data } = await http.get('/settings/approval-flow');
  return data;
}

/** Yalnızca bundan sonra onaya gönderilen talepleri etkiler. @param {string[]} roles */
export async function updateApprovalFlow(roles) {
  await http.put('/settings/approval-flow', { roles });
}

/** @returns {Promise<{ name: string, stages: StageSetting[] }>} */
export async function getPipelineStages() {
  const { data } = await http.get('/settings/pipeline-stages');
  return data;
}

/** @param {{ id: string, name: string, isOptional: boolean }[]} stages */
export async function updatePipelineStages(stages) {
  await http.put('/settings/pipeline-stages', { stages });
}

/** @param {number} year @returns {Promise<Holiday[]>} */
export async function getHolidays(year) {
  const { data } = await http.get('/settings/holidays', { params: { year } });
  return data;
}

/** @param {{ name: string, date: string, recurring: boolean }} input */
export async function createHoliday(input) {
  const { data } = await http.post('/settings/holidays', input);
  return data.id;
}

/** @param {string} id */
export async function deleteHoliday(id) {
  await http.delete(`/settings/holidays/${id}`);
}

/** @returns {Promise<RoleMapping[]>} */
export async function getRoleMappings() {
  const { data } = await http.get('/settings/role-mappings');
  return data;
}

/** @param {{ groupName: string, role: string }} input */
export async function createRoleMapping(input) {
  const { data } = await http.post('/settings/role-mappings', input);
  return data.id;
}

/** Sistem Admin rolüne eşlenmiş son grup silinemez (422). @param {string} id */
export async function deleteRoleMapping(id) {
  await http.delete(`/settings/role-mappings/${id}`);
}
