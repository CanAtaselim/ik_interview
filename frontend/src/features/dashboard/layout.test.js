import { describe, expect, it } from 'vitest';
import { applyOrder, resolveWidth } from './layout';

describe('applyOrder', () => {
  const defaults = ['a', 'b', 'c', 'd'];

  it('kayıt yoksa varsayılan sırayı verir', () => {
    expect(applyOrder(defaults, undefined)).toEqual(defaults);
    expect(applyOrder(defaults, [])).toEqual(defaults);
  });

  it('kayıtlı sırayı uygular', () => {
    expect(applyOrder(defaults, ['c', 'a', 'b', 'd'])).toEqual(['c', 'a', 'b', 'd']);
  });

  it('sonradan eklenen kartı sona koyar, kaldırılanı yok sayar, tekrarı eler', () => {
    expect(applyOrder(defaults, ['c', 'x', 'a', 'c'])).toEqual(['c', 'a', 'b', 'd']);
  });
});

describe('resolveWidth', () => {
  it('geçerli kayıtlı genişliği kullanır, aksi halde varsayılanı', () => {
    expect(resolveWidth(3, 6)).toBe(6);
    expect(resolveWidth(3, undefined)).toBe(3);
    expect(resolveWidth(3, 5)).toBe(3);
    expect(resolveWidth(3, '4')).toBe(3);
  });
});
