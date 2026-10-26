import { Role } from '@/shared/auth/roles';

/** Backend enum değeri → arayüzde gösterilen Türkçe etiket. */
export const ROLE_LABELS = Object.freeze({
  [Role.SystemAdmin]: 'Sistem Admin',
  [Role.HrManager]: 'İK Yöneticisi',
  [Role.HrSpecialist]: 'İK Uzmanı',
  [Role.TeamLead]: 'Takım Lideri',
  [Role.DepartmentManager]: 'Departman Yöneticisi',
  [Role.Executive]: 'Yönetim',
});

/** @param {string[]} roles */
export function formatRoles(roles) {
  return roles.map((role) => ROLE_LABELS[role] ?? role).join(', ');
}

// Durum haritaları: StatusTag bileşeni etiketi ve rengi buradan alır.
export const HIRING_REQUEST_STATUS = Object.freeze({
  Draft: { label: 'Taslak', severity: 'secondary' },
  PendingApproval: { label: 'Onay bekliyor', severity: 'warning' },
  Approved: { label: 'Onaylandı', severity: 'success' },
  Rejected: { label: 'Reddedildi', severity: 'danger' },
  Cancelled: { label: 'İptal edildi', severity: 'secondary' },
});

export const APPROVAL_DECISION = Object.freeze({
  Pending: { label: 'Bekliyor', severity: 'warning' },
  Approved: { label: 'Onayladı', severity: 'success' },
  Rejected: { label: 'Reddetti', severity: 'danger' },
});

export const URGENCY = Object.freeze({
  Low: { label: 'Düşük', severity: 'secondary' },
  Normal: { label: 'Normal', severity: 'info' },
  High: { label: 'Yüksek', severity: 'danger' },
});

export const HIRING_REASON_LABELS = Object.freeze({
  NewHeadcount: 'Yeni kadro',
  Replacement: 'Ayrılan çalışanın yerine',
});

export const POSITION_STATUS = Object.freeze({
  Open: { label: 'Açık', severity: 'success' },
  OnHold: { label: 'Beklemede', severity: 'warning' },
  Closed: { label: 'Kapatıldı', severity: 'secondary' },
  Cancelled: { label: 'İptal edildi', severity: 'secondary' },
});

export const APPLICATION_STATUS = Object.freeze({
  Active: { label: 'Devam ediyor', severity: 'info' },
  Hired: { label: 'İşe alındı', severity: 'success' },
  Rejected: { label: 'Olumsuz', severity: 'danger' },
  Withdrawn: { label: 'Aday çekildi', severity: 'secondary' },
});

export const CV_REVIEW_DECISION = Object.freeze({
  Pending: { label: 'Karar bekliyor', severity: 'warning' },
  Positive: { label: 'Olumlu', severity: 'success' },
  Negative: { label: 'Olumsuz', severity: 'danger' },
  OnHold: { label: 'Bekletiliyor', severity: 'info' },
});

export const INTERVIEW_STATUS = Object.freeze({
  Scheduled: { label: 'Planlandı', severity: 'info' },
  Completed: { label: 'Yapıldı', severity: 'success' },
  Cancelled: { label: 'İptal edildi', severity: 'secondary' },
  NoShow: { label: 'Aday gelmedi', severity: 'danger' },
});

export const INTERVIEW_MODE_LABELS = Object.freeze({
  InPerson: 'Yüz yüze',
  Online: 'Online',
});

export const QUESTION_TYPE_LABELS = Object.freeze({
  Technical: 'Teknik',
  Behavioral: 'Davranışsal',
  Case: 'Vaka',
});

export const DIFFICULTY_LABELS = Object.freeze({
  1: 'Kolay',
  2: 'Orta',
  3: 'Zor',
});

export const RECOMMENDATION = Object.freeze({
  StrongHire: { label: 'Kesinlikle alınsın', severity: 'success' },
  Hire: { label: 'Alınsın', severity: 'success' },
  Undecided: { label: 'Kararsız', severity: 'secondary' },
  NoHire: { label: 'Alınmasın', severity: 'danger' },
  StrongNoHire: { label: 'Kesinlikle alınmasın', severity: 'danger' },
});

/** Sabit puan rubriği: her puanın anlamı puan düğmelerinin yanında gösterilir. */
export const SCORE_RUBRIC = Object.freeze({
  1: 'Yetersiz',
  2: 'Beklentinin altında',
  3: 'Beklentiyi karşılıyor',
  4: 'Beklentinin üstünde',
  5: 'Mükemmel',
});

export const DEBRIEF_DECISION = Object.freeze({
  Proceed: { label: 'Sonraki aşamaya geçsin', severity: 'success' },
  Hold: { label: 'Beklesin', severity: 'warning' },
  Reject: { label: 'Olumsuz', severity: 'danger' },
});

export const SCORECARD_PROGRESS = Object.freeze({
  NotStarted: { label: 'Başlamadı', severity: 'secondary' },
  Draft: { label: 'Taslak', severity: 'warning' },
  Submitted: { label: 'Gönderildi', severity: 'success' },
});

/** Bildirim türleri; tercih sayfasında açıklamasıyla gösterilir. */
export const NOTIFICATION_TYPE_LABELS = Object.freeze({
  HiringRequestAwaitingApproval: {
    label: 'Onayımı bekleyen kadro talebi',
    description: 'Bir talep onay sırası size geldiğinde.',
  },
  HiringRequestDecided: {
    label: 'Kadro talebimin sonucu',
    description: 'Açtığınız talep onaylandığında veya reddedildiğinde.',
  },
  CvReviewRequested: {
    label: 'Bana CV önerildi',
    description: 'İK bir adayın CV’sini size önerdiğinde.',
  },
  CvReviewReminder: {
    label: 'CV değerlendirme hatırlatması',
    description: 'Size önerilen CV’nin son tarihi geçtiğinde.',
  },
  CvReviewEscalated: {
    label: 'Geciken CV değerlendirmesi',
    description: 'Önerdiğiniz CV için takım lideri hatırlatmaya rağmen karar vermediğinde.',
  },
  CvReviewDecided: {
    label: 'CV kararı',
    description: 'Önerdiğiniz CV için karar verildiğinde (olumluda mülakat planlama kısayolu).',
  },
  InterviewInvitation: {
    label: 'Mülakat daveti',
    description: 'Mülakatçı olarak atandığınızda; e-postada takvim daveti (.ics) bulunur.',
  },
  InterviewUpdated: {
    label: 'Mülakat değişikliği',
    description: 'Atandığınız mülakatın saati, yeri veya katılımcıları değiştiğinde.',
  },
  InterviewCancelled: {
    label: 'Mülakat iptali',
    description: 'Atandığınız mülakat iptal edildiğinde veya mülakattan çıkarıldığınızda.',
  },
  InterviewReminder: {
    label: 'Mülakat hatırlatması',
    description: 'Mülakattan 24 saat ve 1 saat önce.',
  },
  DebriefReady: {
    label: 'Tüm değerlendirmeler geldi',
    description: 'Planladığınız mülakatın tüm değerlendirmeleri gönderildiğinde.',
  },
  ScorecardReopened: {
    label: 'Değerlendirmem düzeltmeye açıldı',
    description: 'İK Yöneticisi gönderdiğiniz değerlendirmeyi düzeltme için açtığında.',
  },
  BlacklistApprovalPending: {
    label: 'Onay bekleyen kara liste talebi',
    description: 'Bir İK Uzmanı kara liste talebi açtığında (İK Yöneticisi).',
  },
});

export const NOTIFICATION_GROUPS = Object.freeze([
  {
    key: 'hiring',
    title: 'Kadro talepleri',
    types: ['HiringRequestAwaitingApproval', 'HiringRequestDecided'],
  },
  {
    key: 'cv',
    title: 'CV değerlendirme',
    types: ['CvReviewRequested', 'CvReviewReminder', 'CvReviewEscalated', 'CvReviewDecided'],
  },
  {
    key: 'interview',
    title: 'Mülakatlar',
    types: ['InterviewInvitation', 'InterviewUpdated', 'InterviewCancelled', 'InterviewReminder'],
  },
  { key: 'scorecard', title: 'Değerlendirme', types: ['DebriefReady', 'ScorecardReopened'] },
  { key: 'blacklist', title: 'Kara liste', types: ['BlacklistApprovalPending'] },
]);

/** Süreç aşamasının türü; geçiş kuralları buna bağlıdır, bu yüzden değiştirilemez. */
export const STAGE_KIND_LABELS = Object.freeze({
  Applied: 'Başvuru',
  HrScreening: 'İK görüşmesi',
  CvReview: 'CV kararı bekler',
  TechnicalInterview: 'Mülakat (değerlendirme gerekir)',
  CaseStudy: 'Case / ödev',
  FinalInterview: 'Mülakat (değerlendirme gerekir)',
  ReferenceCheck: 'Referans kontrolü',
  Offer: 'Teklif',
  Hired: 'İşe alım (pozisyon dolar)',
});

export const BLACKLIST_STATUS = Object.freeze({
  PendingApproval: { label: 'Onay bekliyor', severity: 'warning' },
  Active: { label: 'Yürürlükte', severity: 'danger' },
  Expired: { label: 'Süresi doldu', severity: 'secondary' },
  Removed: { label: 'Çıkarıldı', severity: 'secondary' },
});

export const BLACKLIST_MATCH_KIND_LABELS = Object.freeze({
  Exact: 'e-posta veya telefon aynı',
  Possible: 'ad soyad benziyor, farklı kişi olabilir',
});

export const SCREENING_CHANNEL_LABELS = Object.freeze({
  Phone: 'Telefon',
  InPerson: 'Yüz yüze',
  Teams: 'Teams',
});

export const LANGUAGE_LEVEL_LABELS = Object.freeze({
  Beginner: 'Başlangıç',
  Intermediate: 'Orta',
  Advanced: 'İleri',
  Native: 'Ana dil',
});

export const WORK_MODEL_LABELS = Object.freeze({
  Office: 'Ofis',
  Hybrid: 'Hibrit',
  Remote: 'Uzaktan',
  Any: 'Fark etmez',
});

export const MILITARY_STATUS_LABELS = Object.freeze({
  Done: 'Yapıldı',
  Postponed: 'Tecilli',
  Exempt: 'Muaf',
  NotApplicable: 'Yükümlü değil',
});

/** Zaman çizelgesi olay türü → ikon. */
export const TIMELINE_ICONS = Object.freeze({
  CandidateAdded: 'pi pi-user-plus',
  DocumentAdded: 'pi pi-file',
  ScreeningStarted: 'pi pi-comments',
  ScreeningDeleted: 'pi pi-trash',
  AddedToTalentPool: 'pi pi-star',
  RemovedFromTalentPool: 'pi pi-star',
  ApplicationStarted: 'pi pi-briefcase',
  StageChanged: 'pi pi-arrow-right',
  ApplicationClosed: 'pi pi-times-circle',
  ChangeUndone: 'pi pi-undo',
  CvSuggested: 'pi pi-send',
  CvReviewed: 'pi pi-check-square',
  InterviewScheduled: 'pi pi-calendar-plus',
  InterviewRescheduled: 'pi pi-calendar',
  InterviewCancelled: 'pi pi-calendar-times',
  InterviewCompleted: 'pi pi-check-circle',
  InterviewNoShow: 'pi pi-user-minus',
  ScorecardSubmitted: 'pi pi-star',
  ScorecardReopened: 'pi pi-lock-open',
  DebriefRecorded: 'pi pi-users',
  EmailSent: 'pi pi-envelope',
});

export const DICTIONARY_TYPES = Object.freeze([
  { value: 'Department', label: 'Birimler' },
  { value: 'PositionTitle', label: 'Pozisyon adları' },
  { value: 'Level', label: 'Seviyeler' },
  { value: 'EducationLevel', label: 'Eğitim seviyeleri' },
  { value: 'DocumentType', label: 'Dosya türleri' },
  { value: 'Competency', label: 'Yetkinlikler' },
  { value: 'RejectionReason', label: 'Red nedenleri' },
  { value: 'BlacklistReason', label: 'Kara liste nedenleri' },
  { value: 'CandidateSource', label: 'Aday kaynakları' },
  { value: 'InterviewType', label: 'Mülakat tipleri' },
  { value: 'Room', label: 'Toplantı odaları' },
]);

/**
 * Etiket haritasını seçim bileşenlerinin beklediği `{ label, value }` listesine çevirir.
 * @param {Record<string, string | { label: string }>} map
 */
export function toOptions(map) {
  return Object.entries(map).map(([value, entry]) => ({
    value,
    label: typeof entry === 'string' ? entry : entry.label,
  }));
}
