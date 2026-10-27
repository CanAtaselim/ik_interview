import { http } from '@/shared/api/http';

/**
 * @typedef {{ from?: string, to?: string, departmentId?: string, positionId?: string }} DashboardFilters
 *   Tarihler "yyyy-MM-dd" (Türkiye günü, iki uç dahil). Boşsa son 90 gün.
 * @typedef {'summary' | 'requests' | 'candidates' | 'pipeline' | 'cv-reviews' | 'interviews'
 *   | 'scorecards' | 'blacklist'} DashboardSection
 */

/**
 * Bir dashboard temasının verisi. Her tema ayrı istektir; kartlar birbirini beklemeden dolar.
 * @param {DashboardSection} section @param {DashboardFilters} filters
 */
export async function getDashboardSection(section, filters) {
  const { data } = await http.get(`/dashboard/${section}`, { params: filters });
  return data;
}

const LAYOUT_KEY = 'dashboard.layout';

/** Kullanıcının kayıtlı kart düzeni; hiç kaydetmediyse `value` null'dır. */
export async function getDashboardLayout() {
  const { data } = await http.get(`/me/preferences/${LAYOUT_KEY}`);
  return data.value;
}

/** @param {import('./layout').DashboardLayout} layout */
export async function saveDashboardLayout(layout) {
  await http.put(`/me/preferences/${LAYOUT_KEY}`, { value: layout });
}

export async function resetDashboardLayout() {
  await http.delete(`/me/preferences/${LAYOUT_KEY}`);
}
