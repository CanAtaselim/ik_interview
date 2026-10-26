import { afterEach, describe, expect, it, vi } from 'vitest';
import { canPreview, openOrDownload } from './openDocument';

afterEach(() => vi.restoreAllMocks());

describe('canPreview', () => {
  it('PDF ve resimleri tarayıcıda gösterir, Word dosyalarını göstermez', () => {
    expect(canPreview('application/pdf')).toBe(true);
    expect(canPreview('image/png')).toBe(true);
    expect(canPreview('image/jpeg')).toBe(true);
    expect(canPreview('application/msword')).toBe(false);
    expect(
      canPreview('application/vnd.openxmlformats-officedocument.wordprocessingml.document'),
    ).toBe(false);
    expect(canPreview(null)).toBe(false);
  });
});

describe('openOrDownload', () => {
  it('PDF için sekmeyi hemen açar, adres gelince satır içi adresi yükler', async () => {
    const tab = { location: { href: '' }, opener: {}, close: vi.fn() };
    const open = vi.spyOn(window, 'open').mockReturnValue(tab);

    await openOrDownload(() => Promise.resolve('/api/v1/files/abc'), 'application/pdf');

    expect(open).toHaveBeenCalledWith('', '_blank');
    expect(tab.location.href).toBe('/api/v1/files/abc?inline=true');
    expect(tab.opener).toBeNull();
  });

  it('adres alınamazsa açılan boş sekmeyi kapatır ve hatayı iletir', async () => {
    const tab = { location: { href: '' }, opener: {}, close: vi.fn() };
    vi.spyOn(window, 'open').mockReturnValue(tab);

    await expect(
      openOrDownload(() => Promise.reject(new Error('yetki yok')), 'application/pdf'),
    ).rejects.toThrow('yetki yok');
    expect(tab.close).toHaveBeenCalled();
  });

  it('Word dosyası için sekme açmaz, indirir', async () => {
    const open = vi.spyOn(window, 'open');
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    await openOrDownload(() => Promise.resolve('/api/v1/files/xyz'), 'application/msword');

    expect(open).not.toHaveBeenCalled();
    expect(click).toHaveBeenCalledTimes(1);
  });
});
