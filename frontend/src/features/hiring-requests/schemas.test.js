import { describe, expect, it } from 'vitest';
import { emptyHiringRequestForm, hiringRequestSchema } from './schemas';

const complete = () => ({
  ...emptyHiringRequestForm('dept-1'),
  title: 'Yazılım Geliştirme Uzmanı',
  levelId: 'level-1',
  expectedStartDate: new Date(2026, 11, 1),
  competencies: ['C#'],
  description: 'Ödeme ekibi için.',
});

const messages = (values) =>
  Object.fromEntries(
    hiringRequestSchema
      .safeParse(values)
      .error?.issues.map((issue) => [issue.path[0], issue.message]) ?? [],
  );

describe('hiringRequestSchema', () => {
  it('tüm alanlar doluyken geçer', () => {
    expect(hiringRequestSchema.safeParse(complete()).success).toBe(true);
  });

  it('boş formda her zorunlu alan için ayrı mesaj verir', () => {
    expect(messages(emptyHiringRequestForm())).toMatchObject({
      title: 'Pozisyon adını seçmelisiniz.',
      departmentId: 'Birim seçmelisiniz.',
      levelId: 'Seviye seçmelisiniz.',
      expectedStartDate: 'Beklenen başlangıç tarihini seçmelisiniz.',
      competencies: 'En az bir yetkinlik eklemelisiniz.',
      description: 'Açıklamayı yazmalısınız.',
    });
  });

  it('yalnızca boşluktan oluşan açıklamayı kabul etmez', () => {
    expect(messages({ ...complete(), description: '   ' }).description).toBe(
      'Açıklamayı yazmalısınız.',
    );
  });

  it('yerine alımda çalışan adını ister, yeni kadroda istemez', () => {
    expect(messages({ ...complete(), reason: 'Replacement' }).replacedEmployeeName).toBe(
      'Yerine alım yapılacak çalışanın adını yazmalısınız.',
    );
    expect(hiringRequestSchema.safeParse({ ...complete(), reason: 'NewHeadcount' }).success).toBe(
      true,
    );
  });
});
