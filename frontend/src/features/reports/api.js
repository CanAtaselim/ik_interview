import { downloadFile } from '@/shared/utils/downloadFile';

/** @typedef {'pdf' | 'xlsx'} ReportFormat */

/** @param {string} candidateId @param {ReportFormat} format */
export const downloadCandidateDossier = (candidateId, format) =>
  downloadFile(`/reports/candidates/${candidateId}/dossier`, { format });

/**
 * Liste raporunun filtreleri; ilgili listenin ekranındaki filtrelerle aynı anlamdadır. Bir rapora ait
 * olmayan filtre sunucuda yok sayılır.
 * @typedef {object} ListReportFilters
 * @property {string} [from] yyyy-MM-dd
 * @property {string} [to] yyyy-MM-dd
 * @property {string} [departmentId]
 * @property {string} [positionId]
 * @property {string} [search]
 * @property {string} [status] Raporun kendi durum değeri (enum adı)
 * @property {string} [sourceId]
 * @property {boolean} [inTalentPool]
 * @property {string} [tag]
 * @property {string} [process] Active | None | Hired | Closed
 * @property {boolean} [blacklisted]
 * @property {string} [city]
 * @property {string} [interviewerUserId]
 * @property {string} [roomId]
 */

/**
 * @param {string} kind Liste raporu türü (ReportsController'daki adlar).
 * @param {ListReportFilters} filters
 */
export const downloadListReport = (kind, filters) =>
  downloadFile(
    `/reports/lists/${kind}`,
    // false de anlamlı bir filtredir ("kara listede olmayanlar"); downloadFile boş değerleri eler.
    Object.fromEntries(
      Object.entries(filters).map(([key, value]) => [
        key,
        typeof value === 'boolean' ? String(value) : value,
      ]),
    ),
  );

/** @param {{ from?: string, to?: string, departmentId?: string, positionId?: string }} filters */
export const downloadDashboardReport = (filters) => downloadFile('/reports/dashboard', filters);
