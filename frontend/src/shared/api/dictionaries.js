import { http } from './http';

/**
 * @typedef {{ id: string, name: string, sortOrder: number, isActive: boolean }} DictionaryItem
 */

/**
 * @param {string} type Backend'deki `DictionaryType` değeri (ör. 'Department').
 * @param {{ includeInactive?: boolean }} [options]
 * @returns {Promise<DictionaryItem[]>}
 */
export async function getDictionaryItems(type, { includeInactive = false } = {}) {
  const { data } = await http.get(`/dictionaries/${type}/items`, { params: { includeInactive } });
  return data;
}

/** @param {string} type @param {string} name */
export async function createDictionaryItem(type, name) {
  const { data } = await http.post(`/dictionaries/${type}/items`, { name });
  return data.id;
}

/** @param {string} id @param {{ name: string, sortOrder: number, isActive: boolean }} item */
export async function updateDictionaryItem(id, item) {
  await http.put(`/dictionary-items/${id}`, item);
}

/**
 * @typedef {{ area: string, count: number }} DictionaryUsage
 * @typedef {DictionaryItem & { usageCount: number, usages: DictionaryUsage[], isSystem: boolean }} DictionaryAdminItem
 */

/**
 * Yönetim ekranı listesi: sunucuda sayfalanır, aranır ve durumuna göre süzülür; her satır kullanım
 * dökümüyle gelir.
 * @param {string} type
 * @param {{ search?: string, isActive?: boolean, page?: number, pageSize?: number }} params
 * @returns {Promise<{ items: DictionaryAdminItem[], totalCount: number, page: number, pageSize: number }>}
 */
export async function getDictionaryAdminItems(type, params) {
  const { data } = await http.get(`/dictionaries/${type}/admin-items`, { params });
  return data;
}

/**
 * Kullanılmayan öğeyi siler. Kullanılıyorsa 409 (`code: 'Dictionary.InUse'`, `usages` ile) döner.
 * @param {string} id
 */
export async function deleteDictionaryItem(id) {
  await http.delete(`/dictionary-items/${id}`);
}
