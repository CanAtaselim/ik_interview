/** Tarayıcının kendi görüntüleyicisiyle gösterebildiği dosya türleri. */
const PREVIEWABLE_TYPES = new Set(['application/pdf', 'image/png', 'image/jpeg']);

/** @param {string | null | undefined} contentType */
export const canPreview = (contentType) => PREVIEWABLE_TYPES.has(contentType ?? '');

const asInline = (url) => `${url}${url.includes('?') ? '&' : '?'}inline=true`;

const download = (url) => {
  const link = document.createElement('a');
  link.href = url;
  link.rel = 'noopener';
  document.body.appendChild(link);
  link.click();
  link.remove();
};

/**
 * PDF ve resimleri yan sekmede açar; Word gibi tarayıcının gösteremediği dosyaları indirir.
 * Bağlantı sunucudan beklenirken açılır pop-up engelleyiciye takılmasın diye sekme, tıklama
 * anında (adres gelmeden) açılır; adres gelince o sekmeye yüklenir.
 * @param {() => Promise<string>} getUrl İmzalı indirme adresini alır (erişim kayda geçer).
 * @param {string | null | undefined} contentType
 */
export async function openOrDownload(getUrl, contentType) {
  if (!canPreview(contentType)) {
    download(await getUrl());
    return;
  }

  const tab = window.open('', '_blank');
  try {
    const url = asInline(await getUrl());
    if (tab) {
      tab.opener = null;
      tab.location.href = url;
    } else {
      window.open(url, '_blank', 'noopener');
    }
  } catch (error) {
    tab?.close();
    throw error;
  }
}
