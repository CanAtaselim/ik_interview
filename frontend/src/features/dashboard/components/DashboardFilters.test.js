import { describe, expect, it } from 'vitest';
import { resolveRange, toIsoDate } from './DashboardFilters';

const today = new Date(2026, 9, 27); // 27 Ekim 2026

describe('toIsoDate', () => {
  it('yerel günü yyyy-MM-dd yazar', () => {
    expect(toIsoDate(new Date(2026, 0, 5))).toBe('2026-01-05');
    expect(toIsoDate(new Date(2026, 11, 31))).toBe('2026-12-31');
  });
});

describe('resolveRange', () => {
  it('son 30 günü iki ucu dahil 30 gün olarak hesaplar', () => {
    expect(resolveRange('30', null, null, today)).toEqual({ from: '2026-09-28', to: '2026-10-27' });
  });

  it('son 90 günü hesaplar (varsayılan)', () => {
    expect(resolveRange('90', null, null, today)).toEqual({ from: '2026-07-30', to: '2026-10-27' });
    expect(resolveRange('bilinmeyen', null, null, today)).toEqual({
      from: '2026-07-30',
      to: '2026-10-27',
    });
  });

  it('bu yılı 1 Ocak’tan bugüne hesaplar', () => {
    expect(resolveRange('year', null, null, today)).toEqual({
      from: '2026-01-01',
      to: '2026-10-27',
    });
  });

  it('özel aralıkta adresteki tarihleri kullanır; eksikse son 90güne döner', () => {
    expect(resolveRange('custom', '2026-03-01', '2026-03-31', today)).toEqual({
      from: '2026-03-01',
      to: '2026-03-31',
    });
    expect(resolveRange('custom', null, null, today)).toEqual({
      from: '2026-07-30',
      to: '2026-10-27',
    });
  });
});
