/** Backend'deki `Role` enum'ı ile birebir aynı değerler. */
export const Role = Object.freeze({
  SystemAdmin: 'SystemAdmin',
  HrManager: 'HrManager',
  HrSpecialist: 'HrSpecialist',
  TeamLead: 'TeamLead',
  DepartmentManager: 'DepartmentManager',
  Executive: 'Executive',
});

export const HR_ROLES = Object.freeze([Role.HrSpecialist, Role.HrManager]);

/** E-posta şablonları, onay akışı, süreç aşamaları ve resmi tatilleri yönetebilen roller. */
export const SETTINGS_ROLES = Object.freeze([Role.SystemAdmin, Role.HrManager]);

/** Soru bankası ve şablonları yönetebilen roller (takım lideri yalnızca kendi birimini). */
export const QUESTION_ROLES = Object.freeze([Role.TeamLead, Role.HrSpecialist, Role.HrManager]);

/** Mülakatçı olabilecek roller (backend'deki `CanInterview` politikasıyla aynı). */
export const INTERVIEWER_ROLES = Object.freeze([
  Role.TeamLead,
  Role.DepartmentManager,
  Role.HrSpecialist,
  Role.HrManager,
  Role.Executive,
]);

/**
 * @param {{ roles: string[] } | null} user
 * @param {readonly string[] | null | undefined} roles Boş bırakılırsa oturum açmış herkes yetkilidir.
 */
export function hasAnyRole(user, roles) {
  if (!user) return false;
  if (!roles || roles.length === 0) return true;
  return roles.some((role) => user.roles.includes(role));
}
