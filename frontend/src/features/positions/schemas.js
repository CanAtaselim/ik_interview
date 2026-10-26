import { z } from 'zod';

const detailFields = {
  title: z
    .string()
    .trim()
    .min(1, 'Pozisyon adını yazmalısınız.')
    .max(200, 'Pozisyon adı en fazla 200 karakter olabilir.'),
  headcount: z
    .number({ error: 'Kişi sayısını yazmalısınız.' })
    .int('Kişi sayısı tam sayı olmalı.')
    .min(1, 'Kişi sayısı en az 1 olmalı.')
    .max(1000, 'Kişi sayısı en fazla 1000 olabilir.'),
  jobDescription: z.string().max(10000, 'İş tanımı en fazla 10.000 karakter olabilir.'),
  responsibleHrUserId: z.string().nullable(),
};

/** Açık pozisyonun birimi ve seviyesi sonradan değişmez; düzenlemede bu alanlar yoktur. */
export const updatePositionSchema = z.object(detailFields);

export const createPositionSchema = z.object({
  ...detailFields,
  departmentId: z.string().min(1, 'Birim seçmelisiniz.'),
  levelId: z.string().min(1, 'Seviye seçmelisiniz.'),
});
