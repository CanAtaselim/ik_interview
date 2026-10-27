/** Kod içinde yol yazmak yerine bu sabitler kullanılır. */
export const ROUTES = Object.freeze({
  home: '/',
  login: '/login',
  dashboard: '/dashboard',
  hiringRequests: '/hiring-requests',
  /** @param {string} id Listeyi, verilen talebin detayı açık halde gösterir. */
  hiringRequest: (id) => `/hiring-requests?selected=${id}`,
  positions: '/positions',
  /** @param {string} id */
  position: (id) => `/positions/${id}`,
  candidates: '/candidates',
  /** @param {string} id */
  candidate: (id) => `/candidates/${id}`,
  cvReviews: '/cv-reviews',
  /** @param {string} id Değerlendirme ve adayın takım lideri görünümü. */
  cvReview: (id) => `/cv-reviews/${id}`,
  interviews: '/interviews',
  /** @param {string} id */
  interview: (id) => `/interviews/${id}`,
  availability: '/availability',
  notificationSettings: '/settings/notifications',
  /** @param {string} id Mülakatçının değerlendirme (puanlama) ekranı. */
  scorecard: (id) => `/interviews/${id}/scorecard`,
  /** @param {string} id Pozisyondaki adayların puan karşılaştırması. */
  comparison: (id) => `/positions/${id}/compare`,
  questions: '/questions',
  interviewTemplates: '/interview-templates',
  /** @param {string} id Pozisyonun süreç hattı (kanban). */
  pipeline: (id) => `/positions/${id}/pipeline`,
  blacklist: '/blacklist',
  /** @param {string} id Listeyi, verilen kaydın ayrıntısı açık halde gösterir. */
  blacklistEntry: (id) => `/blacklist?selected=${id}`,
  admin: '/admin',
  dictionaries: '/admin/dictionaries',
  approvalFlow: '/admin/approval-flow',
  pipelineStages: '/admin/pipeline-stages',
  emailTemplates: '/admin/email-templates',
  holidays: '/admin/holidays',
  roleMappings: '/admin/role-mappings',
});
