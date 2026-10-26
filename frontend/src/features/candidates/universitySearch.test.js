import { describe, expect, it } from 'vitest';
import { normalizeSearch, suggestUniversities } from './universitySearch';

describe('suggestUniversities', () => {
  it('Türkçe harflere bakmadan arar', () => {
    expect(suggestUniversities('istanbul tek')).toContain('İstanbul Teknik Üniversitesi');
    expect(suggestUniversities('ISPARTA')).toContain('Isparta Uygulamalı Bilimler Üniversitesi');
    expect(suggestUniversities('ODTÜ')[0]).toBe('Orta Doğu Teknik Üniversitesi');
    expect(suggestUniversities('itü')[0]).toBe('İstanbul Teknik Üniversitesi');
  });

  it('devlet ve vakıf üniversitelerini içerir', () => {
    expect(suggestUniversities('ankara')).toContain('Ankara Üniversitesi');
    expect(suggestUniversities('bilkent')).toContain('İhsan Doğramacı Bilkent Üniversitesi');
    expect(suggestUniversities('koç')).toContain('Koç Üniversitesi');
  });

  it('adın başıyla eşleşenleri öne alır ve sınırlar', () => {
    const result = suggestUniversities('ankara', 5);
    expect(result).toHaveLength(5);
    expect(normalizeSearch(result[0]).startsWith('ankara')).toBe(true);
  });

  it('boş sorguda öneri vermez', () => {
    expect(suggestUniversities('  ')).toEqual([]);
  });
});
