import { describe, expect, it } from 'vitest';
import { formatMonths, formatMoneyRange } from '@/shared/utils/format';
import { districtsOf, PROVINCES } from '@/shared/data/turkeyLocations';
import {
  cvPrefillPatch,
  joinMonths,
  splitMonths,
  toScreeningForm,
  toScreeningInput,
} from './schemas';

const apiScreening = (overrides = {}) => ({
  channel: null,
  interviewedAt: null,
  educations: [],
  educationNote: null,
  languages: [],
  languagesNote: null,
  softSkills: null,
  technicalSkillTags: [],
  technicalSkills: null,
  experienceSummary: null,
  motivation: null,
  totalExperienceMonths: null,
  fieldExperienceMonths: null,
  expectedSalary: null,
  expectedSalaryMax: null,
  noticePeriodWeeks: null,
  hrComment: null,
  currentSalary: null,
  workModel: null,
  locationProvince: null,
  locationDistrict: null,
  locationNote: null,
  openToRelocation: null,
  hasTravelRestriction: null,
  militaryStatus: null,
  overallRating: null,
  ...overrides,
});

describe('toplam iş deneyimi (yıl ve ay)', () => {
  it('toplam ayı yıl ve aya böler, tersini birleştirir', () => {
    expect(splitMonths(40)).toEqual({ years: 3, months: 4 });
    expect(splitMonths(6)).toEqual({ years: 0, months: 6 });
    expect(splitMonths(null)).toEqual({ years: null, months: null });
    expect(joinMonths(3, 4)).toBe(40);
    expect(joinMonths(null, 6)).toBe(6);
    expect(joinMonths(2, null)).toBe(24);
    expect(joinMonths(null, null)).toBeNull();
  });

  it('gösterimi Türkçe yazar', () => {
    expect(formatMonths(40)).toBe('3 yıl 4 ay');
    expect(formatMonths(6)).toBe('6 ay');
    expect(formatMonths(36)).toBe('3 yıl');
    expect(formatMonths(0)).toBe('0 ay');
    expect(formatMonths(null)).toBe('');
  });
});

describe('ön görüşme formu ↔ API', () => {
  it('kayıtlı toplam ayı yıl ve ay alanlarına açar', () => {
    const form = toScreeningForm(
      apiScreening({ totalExperienceMonths: 40, fieldExperienceMonths: 62 }),
    );
    expect(form.experienceYears).toBe(3);
    expect(form.experienceMonths).toBe(4);
    expect(form.fieldYears).toBe(5);
    expect(form.fieldMonths).toBe(2);
  });

  it('yıl ve ayı tek "toplam ay" olarak gönderir, yardımcı alanları göndermez', () => {
    const input = toScreeningInput({
      ...toScreeningForm(apiScreening()),
      experienceYears: 3,
      experienceMonths: 4,
      fieldYears: 2,
      fieldMonths: null,
    });
    expect(input.totalExperienceMonths).toBe(40);
    expect(input.fieldExperienceMonths).toBe(24);
    for (const helper of ['experienceYears', 'experienceMonths', 'fieldYears', 'fieldMonths']) {
      expect(input).not.toHaveProperty(helper);
    }
  });

  it('kaldırılan alanlar (yan haklar, diğer süreçler, eski lokasyon) gönderilmez', () => {
    const input = toScreeningInput(toScreeningForm(apiScreening()));
    for (const removed of [
      'currentBenefits',
      'otherProcesses',
      'location',
      'totalExperienceYears',
    ]) {
      expect(input).not.toHaveProperty(removed);
    }
  });

  it('ücret üst değeri alt değer olmadan gönderilmez', () => {
    const form = toScreeningForm(apiScreening());
    expect(
      toScreeningInput({ ...form, expectedSalary: null, expectedSalaryMax: 130000 })
        .expectedSalaryMax,
    ).toBeNull();
    const money = { amount: 100000, currency: 'TRY', isNet: true };
    expect(
      toScreeningInput({ ...form, expectedSalary: money, expectedSalaryMax: 130000 })
        .expectedSalaryMax,
    ).toBe(130000);
  });

  it('ilçe, il olmadan gönderilmez; boş açıklamalar null olur', () => {
    const form = toScreeningForm(apiScreening());
    const input = toScreeningInput({
      ...form,
      locationProvince: null,
      locationDistrict: 'Kadıköy',
      locationNote: '  ',
      educationNote: '',
      languagesNote: 'Teknik İngilizce',
    });
    expect(input.locationDistrict).toBeNull();
    expect(input.locationNote).toBeNull();
    expect(input.educationNote).toBeNull();
    expect(input.languagesNote).toBe('Teknik İngilizce');
  });

  it('seyahat engeli "yok" (false) ile "sorulmadı" (null) ayrı kalır', () => {
    const form = toScreeningForm(apiScreening());
    expect(toScreeningInput({ ...form, hasTravelRestriction: false }).hasTravelRestriction).toBe(
      false,
    );
    expect(
      toScreeningInput({ ...form, hasTravelRestriction: null }).hasTravelRestriction,
    ).toBeNull();
  });
});

describe('ücret aralığı gösterimi', () => {
  const money = { amount: 100000, currency: 'TRY', isNet: true };

  it('aralığı ve tek değeri yazar', () => {
    expect(formatMoneyRange(money, 130000)).toBe('100.000 – 130.000 ₺ net');
    expect(formatMoneyRange(money, null)).toBe('100.000 ₺ net');
    expect(formatMoneyRange(null, 130000)).toBe('');
  });
});

describe('il ve ilçe listesi', () => {
  it('81 il ve ilçeleri içerir', () => {
    expect(PROVINCES).toHaveLength(81);
    expect(districtsOf('İstanbul')).toHaveLength(39);
    expect(districtsOf('İstanbul')).toContain('Kadıköy');
    expect(districtsOf('Ankara')).toContain('Çankaya');
    expect(districtsOf(null)).toEqual([]);
    expect(districtsOf('Yok Böyle İl')).toEqual([]);
  });
});

describe('CV’den formu önceden doldurma', () => {
  const profile = {
    firstName: 'Ayşe',
    lastName: 'Yılmaz',
    email: 'ayse@ornek.com',
    phone: '0532 123 45 67',
    linkedInUrl: null,
    gitHubUrl: 'https://github.com/ayse',
    city: 'İstanbul',
  };

  it('boş alanları doldurur, bulunamayanı atlar', () => {
    expect(
      cvPrefillPatch({ firstName: '', lastName: ' ', email: '', phone: '', city: '' }, profile),
    ).toEqual({
      firstName: 'Ayşe',
      lastName: 'Yılmaz',
      email: 'ayse@ornek.com',
      phone: '0532 123 45 67',
      gitHubUrl: 'https://github.com/ayse',
      city: 'İstanbul',
    });
  });

  it('kullanıcının yazdığı alanı ezmez', () => {
    const patch = cvPrefillPatch({ firstName: 'Mehmet', email: 'm@ornek.com' }, profile);
    expect(patch).not.toHaveProperty('firstName');
    expect(patch).not.toHaveProperty('email');
    expect(patch.lastName).toBe('Yılmaz');
  });

  it('okunamayan CV’de (boş yanıt) değişiklik yapmaz', () => {
    expect(cvPrefillPatch({}, null)).toEqual({});
    expect(cvPrefillPatch({}, { firstName: null, email: null })).toEqual({});
  });

  it('yetenekleri etiketlere ekler; formda olanı tekrarlamaz', () => {
    const patch = cvPrefillPatch({ tags: ['c#'] }, { skills: ['C#', 'Docker', 'SQL'] });
    expect(patch.tags).toEqual(['Docker', 'SQL']);
  });

  it('etiket sınırını (20) aşmaz', () => {
    const current = { tags: Array.from({ length: 19 }, (_, i) => `etiket${i}`) };
    expect(cvPrefillPatch(current, { skills: ['A', 'B', 'C'] }).tags).toEqual(['A']);
    expect(cvPrefillPatch({ tags: Array(20).fill('x') }, { skills: ['A'] })).not.toHaveProperty('tags');
  });

  it('yetenek yoksa tags eklemez', () => {
    expect(cvPrefillPatch({ tags: [] }, { skills: [] })).not.toHaveProperty('tags');
  });
});
