import { z } from 'zod';
import { fromDateOnly, toDateOnly } from '@/shared/utils/format';

const MAX_COMPETENCIES = 20;

export const hiringRequestSchema = z
  .object({
    title: z
      .string()
      .trim()
      .min(1, 'Pozisyon adını seçmelisiniz.')
      .max(200, 'Pozisyon adı en fazla 200 karakter olabilir.'),
    departmentId: z.string().min(1, 'Birim seçmelisiniz.'),
    levelId: z.string().min(1, 'Seviye seçmelisiniz.'),
    headcount: z
      .number({ error: 'Kişi sayısını yazmalısınız.' })
      .int('Kişi sayısı tam sayı olmalı.')
      .min(1, 'Kişi sayısı en az 1 olmalı.')
      .max(1000, 'Kişi sayısı en fazla 1000 olabilir.'),
    reason: z.enum(['NewHeadcount', 'Replacement']),
    replacedEmployeeName: z.string().trim().max(200, 'Çalışan adı en fazla 200 karakter olabilir.'),
    urgency: z.enum(['Low', 'Normal', 'High']),
    expectedStartDate: z.date({ error: 'Beklenen başlangıç tarihini seçmelisiniz.' }),
    competencies: z
      .array(z.string().max(50, 'Bir yetkinlik en fazla 50 karakter olabilir.'))
      .min(1, 'En az bir yetkinlik eklemelisiniz.')
      .max(MAX_COMPETENCIES, `En fazla ${MAX_COMPETENCIES} yetkinlik ekleyebilirsiniz.`),
    description: z
      .string()
      .trim()
      .min(1, 'Açıklamayı yazmalısınız.')
      .max(4000, 'Açıklama en fazla 4000 karakter olabilir.'),
  })
  .refine((values) => values.reason !== 'Replacement' || values.replacedEmployeeName.length > 0, {
    path: ['replacedEmployeeName'],
    message: 'Yerine alım yapılacak çalışanın adını yazmalısınız.',
  });

/** Boş form; `departmentId` kullanıcının kendi birimiyle önceden doldurulabilir. */
export function emptyHiringRequestForm(departmentId = '') {
  return {
    title: '',
    departmentId,
    levelId: '',
    headcount: 1,
    reason: 'NewHeadcount',
    replacedEmployeeName: '',
    urgency: 'Normal',
    expectedStartDate: null,
    competencies: [],
    description: '',
  };
}

/** API'den gelen talep → form değerleri. */
export function toHiringRequestForm(request) {
  return {
    title: request.title,
    departmentId: request.departmentId,
    levelId: request.levelId,
    headcount: request.headcount,
    reason: request.reason,
    replacedEmployeeName: request.replacedEmployeeName ?? '',
    urgency: request.urgency,
    expectedStartDate: fromDateOnly(request.expectedStartDate),
    competencies: request.competencies,
    description: request.description ?? '',
  };
}

/** Form değerleri → API'nin beklediği gövde. */
export function toHiringRequestInput(values) {
  return {
    ...values,
    replacedEmployeeName: values.reason === 'Replacement' ? values.replacedEmployeeName : null,
    expectedStartDate: toDateOnly(values.expectedStartDate),
    description: values.description || null,
  };
}
