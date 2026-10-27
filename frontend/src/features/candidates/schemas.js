import { z } from 'zod';

const MIN_PHONE_DIGITS = 7;
const countDigits = (value) => value.replace(/\D/g, '').length;

const optionalUrl = (siteName) =>
  z
    .string()
    .trim()
    .max(300, `${siteName} adresi en fazla 300 karakter olabilir.`)
    .refine((value) => value === '' || /^https?:\/\/\S+$/i.test(value), {
      message: `${siteName} adresi "https://" ile başlamalı.`,
    });

/** Ad, soyad ve en az bir iletişim bilgisi zorunludur; gerisi opsiyoneldir. */
export const candidateSchema = z
  .object({
    firstName: z
      .string()
      .trim()
      .min(1, 'Adayın adını yazmalısınız.')
      .max(100, 'Ad en fazla 100 karakter olabilir.'),
    lastName: z
      .string()
      .trim()
      .min(1, 'Adayın soyadını yazmalısınız.')
      .max(100, 'Soyad en fazla 100 karakter olabilir.'),
    email: z
      .string()
      .trim()
      .max(320, 'E-posta en fazla 320 karakter olabilir.')
      .refine((value) => value === '' || z.email().safeParse(value).success, {
        message: 'Geçerli bir e-posta adresi yazın.',
      }),
    phone: z
      .string()
      .trim()
      .max(30, 'Telefon en fazla 30 karakter olabilir.')
      .refine((value) => value === '' || countDigits(value) >= MIN_PHONE_DIGITS, {
        message: 'Geçerli bir telefon numarası yazın.',
      }),
    linkedInUrl: optionalUrl('LinkedIn'),
    gitHubUrl: optionalUrl('GitHub'),
    city: z.string().trim().max(100, 'Şehir en fazla 100 karakter olabilir.'),
    sourceId: z.string().nullable(),
    tags: z
      .array(z.string().max(50, 'Bir etiket en fazla 50 karakter olabilir.'))
      .max(20, 'En fazla 20 etiket ekleyebilirsiniz.'),
    notes: z.string().max(4000, 'Notlar en fazla 4000 karakter olabilir.'),
  })
  .refine((values) => values.email !== '' || values.phone !== '', {
    path: ['email'],
    message: 'E-posta veya telefon alanlarından en az biri dolu olmalı.',
  });

/** @param {object} [candidate] API'den gelen aday; verilmezse boş form. */
export function toCandidateForm(candidate) {
  return {
    firstName: candidate?.firstName ?? '',
    lastName: candidate?.lastName ?? '',
    email: candidate?.email ?? '',
    phone: candidate?.phone ?? '',
    linkedInUrl: candidate?.linkedInUrl ?? '',
    gitHubUrl: candidate?.gitHubUrl ?? '',
    city: candidate?.city ?? '',
    sourceId: candidate?.sourceId ?? null,
    tags: candidate?.tags ?? [],
    notes: candidate?.notes ?? '',
  };
}

const CV_PREFILL_FIELDS = [
  'firstName',
  'lastName',
  'email',
  'phone',
  'linkedInUrl',
  'gitHubUrl',
  'city',
];

const MAX_TAGS = 20;

/**
 * CV'den okunan bilgilerden, formda hâlâ boş olan alanlara yazılacakları seçer (yazılanı ezmez).
 * Yetenekler `tags` alanına eklenir: formda olmayanlar, en fazla 20 etikete kadar. Dönen `tags`
 * yalnızca yeni eklenecek olanları içerir.
 */
export function cvPrefillPatch(current, profile) {
  const patch = Object.fromEntries(
    CV_PREFILL_FIELDS.filter(
      (field) => profile?.[field] && !String(current?.[field] ?? '').trim(),
    ).map((field) => [field, profile[field]]),
  );

  const existing = current?.tags ?? [];
  const known = new Set(existing.map((tag) => tag.toLocaleLowerCase('tr')));
  const added = (profile?.skills ?? [])
    .filter((skill) => skill && !known.has(skill.toLocaleLowerCase('tr')))
    .slice(0, Math.max(0, MAX_TAGS - existing.length));
  if (added.length > 0) patch.tags = added;

  return patch;
}

const emptyToNull = (value) => (value === '' ? null : value);

/**
 * Form değerleri → API gövdesi.
 * @param {object} values
 * @param {{ confirmDuplicate: boolean, confirmBlacklist: boolean }} confirmations
 *   Kullanıcının ekranda görüp kabul ettiği uyarılar.
 */
export function toCandidateInput(values, { confirmDuplicate, confirmBlacklist }) {
  return {
    ...values,
    email: emptyToNull(values.email),
    phone: emptyToNull(values.phone),
    linkedInUrl: emptyToNull(values.linkedInUrl),
    gitHubUrl: emptyToNull(values.gitHubUrl),
    city: emptyToNull(values.city),
    notes: emptyToNull(values.notes),
    confirmDuplicate,
    confirmBlacklist,
  };
}

/** Toplam ay → yıl ve ay alanları (3 yıl 4 ay = 40). */
export function splitMonths(totalMonths) {
  if (totalMonths === null || totalMonths === undefined) return { years: null, months: null };
  return { years: Math.floor(totalMonths / 12), months: totalMonths % 12 };
}

/** Yıl ve ay alanları → toplam ay; ikisi de boşsa `null`. */
export function joinMonths(years, months) {
  if ((years ?? null) === null && (months ?? null) === null) return null;
  return (years ?? 0) * 12 + (months ?? 0);
}

/** API'den gelen ön görüşme kaydı → form değerleri. */
export function toScreeningForm(screening) {
  const experience = splitMonths(screening.totalExperienceMonths);
  const fieldExperience = splitMonths(screening.fieldExperienceMonths);

  return {
    channel: screening.channel,
    interviewedAt: screening.interviewedAt ? new Date(screening.interviewedAt) : null,
    educations: screening.educations.map((education) => ({
      school: education.school ?? '',
      department: education.department ?? '',
      degree: education.degree ?? '',
      graduationYear: education.graduationYear,
    })),
    educationNote: screening.educationNote ?? '',
    languages: screening.languages.map((item) => ({
      language: item.language ?? '',
      level: item.level,
    })),
    languagesNote: screening.languagesNote ?? '',
    softSkills: screening.softSkills ?? '',
    technicalSkillTags: screening.technicalSkillTags,
    technicalSkills: screening.technicalSkills ?? '',
    experienceSummary: screening.experienceSummary ?? '',
    motivation: screening.motivation ?? '',
    experienceYears: experience.years,
    experienceMonths: experience.months,
    fieldYears: fieldExperience.years,
    fieldMonths: fieldExperience.months,
    expectedSalary: screening.expectedSalary,
    expectedSalaryMax: screening.expectedSalaryMax,
    noticePeriodWeeks: screening.noticePeriodWeeks,
    hrComment: screening.hrComment ?? '',
    currentSalary: screening.currentSalary,
    workModel: screening.workModel,
    locationProvince: screening.locationProvince ?? null,
    locationDistrict: screening.locationDistrict ?? null,
    locationNote: screening.locationNote ?? '',
    openToRelocation: screening.openToRelocation,
    hasTravelRestriction: screening.hasTravelRestriction,
    militaryStatus: screening.militaryStatus,
    overallRating: screening.overallRating,
  };
}

/** Form değerleri → API gövdesi. Boş metinler `null` gider; hiçbir alan zorunlu değildir. */
export function toScreeningInput(values) {
  const text = (value) => (value?.trim() ? value : null);
  const { experienceYears, experienceMonths, fieldYears, fieldMonths, ...rest } = values;

  return {
    ...rest,
    interviewedAt: values.interviewedAt ? values.interviewedAt.toISOString() : null,
    educations: values.educations.map((education) => ({
      school: text(education.school),
      department: text(education.department),
      degree: text(education.degree),
      graduationYear: education.graduationYear ?? null,
    })),
    educationNote: text(values.educationNote),
    languages: values.languages.map((item) => ({
      language: text(item.language),
      level: item.level ?? null,
    })),
    languagesNote: text(values.languagesNote),
    softSkills: text(values.softSkills),
    technicalSkills: text(values.technicalSkills),
    experienceSummary: text(values.experienceSummary),
    motivation: text(values.motivation),
    hrComment: text(values.hrComment),
    totalExperienceMonths: joinMonths(experienceYears, experienceMonths),
    fieldExperienceMonths: joinMonths(fieldYears, fieldMonths),
    // Üst değer, alt değer olmadan gönderilmez.
    expectedSalaryMax: values.expectedSalary ? (values.expectedSalaryMax ?? null) : null,
    locationProvince: values.locationProvince || null,
    locationDistrict: values.locationProvince ? values.locationDistrict || null : null,
    locationNote: text(values.locationNote),
    hasTravelRestriction: values.hasTravelRestriction ?? null,
  };
}
