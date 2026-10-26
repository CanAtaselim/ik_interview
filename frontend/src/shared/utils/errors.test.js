import { describe, expect, it, vi } from 'vitest';
import { applyFieldErrors, getErrorMessage } from './errors';

const problem = (data) => ({ response: { status: 400, data } });

describe('getErrorMessage', () => {
  it("ProblemDetails'teki Türkçe açıklamayı döner", () => {
    expect(getErrorMessage(problem({ detail: 'Kullanıcı adı veya şifre hatalı.' }))).toBe(
      'Kullanıcı adı veya şifre hatalı.',
    );
  });

  it('sunucuya ulaşılamadıysa bağlantı mesajı döner', () => {
    expect(getErrorMessage(new Error('Network Error'))).toMatch(/Sunucuya ulaşılamadı/);
  });

  it('açıklama yoksa genel mesaj döner', () => {
    expect(getErrorMessage(problem({}))).toMatch(/İşleminiz tamamlanamadı/);
  });
});

describe('applyFieldErrors', () => {
  it('her alanın ilk hatasını forma basar', () => {
    const setError = vi.fn();
    const error = problem({
      errors: { username: ['Zorunlu.', 'İkinci mesaj.'], password: ['Kısa.'] },
    });

    expect(applyFieldErrors(error, setError)).toBe(true);
    expect(setError).toHaveBeenCalledWith('username', { type: 'server', message: 'Zorunlu.' });
    expect(setError).toHaveBeenCalledWith('password', { type: 'server', message: 'Kısa.' });
  });

  it('alan hatası yoksa forma dokunmaz', () => {
    const setError = vi.fn();

    expect(applyFieldErrors(problem({ detail: 'Genel hata.' }), setError)).toBe(false);
    expect(setError).not.toHaveBeenCalled();
  });
});
