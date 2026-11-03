import { downloadFile } from '@/shared/utils/downloadFile';

/** @typedef {'pdf' | 'xlsx'} ReportFormat */

/** @param {string} candidateId @param {ReportFormat} format */
export const downloadCandidateDossier = (candidateId, format) =>
  downloadFile(`/reports/candidates/${candidateId}/dossier`, { format });

/**
 * @param {string} kind Liste raporu türü (ReportsController'daki adlar).
 * @param {{ from?: string, to?: string, departmentId?: string, positionId?: string }} filters
 */
export const downloadListReport = (kind, filters) =>
  downloadFile(`/reports/lists/${kind}`, filters);

/** @param {{ from?: string, to?: string, departmentId?: string, positionId?: string }} filters */
export const downloadDashboardReport = (filters) => downloadFile('/reports/dashboard', filters);
