import { http } from '@/shared/api/http';

const FALLBACK_NAME = 'rapor';

/**
 * Content-Disposition başlığından dosya adını okur; Türkçe karakterli `filename*` (RFC 5987) önceliklidir.
 * @param {string | null | undefined} header
 */
export function fileNameFrom(header) {
  const encoded = /filename\*=(?:UTF-8|utf-8)''([^;]+)/.exec(header ?? '')?.[1];
  if (encoded) {
    try {
      return decodeURIComponent(encoded);
    } catch {
      // Bozuk kodlama: düz ada geç.
    }
  }
  return /filename="?([^";]+)"?/.exec(header ?? '')?.[1] ?? FALLBACK_NAME;
}

/**
 * Hata yanıtı blob olarak geldiğinde ProblemDetails içeriğini okunur hale getirir; böylece
 * `getErrorMessage` indirme hatalarında da Türkçe mesajı gösterir.
 * @param {unknown} error axios hatası
 */
async function readableError(error) {
  const data = error?.response?.data;
  if (data instanceof Blob) {
    try {
      error.response.data = JSON.parse(await data.text());
    } catch {
      error.response.data = undefined;
    }
  }
  return error;
}

/**
 * Yetkili bir uç noktadan dosyayı alır ve tarayıcıya indirtir (Authorization başlığı gerektiği için
 * düz bağlantı yerine istek atılır).
 * @param {string} path API yolu (örn. "/reports/lists/candidates")
 * @param {Record<string, string | undefined>} [params] Boş değerler gönderilmez.
 */
export async function downloadFile(path, params = {}) {
  const query = Object.fromEntries(Object.entries(params).filter(([, value]) => value));
  let response;
  try {
    response = await http.get(path, { params: query, responseType: 'blob' });
  } catch (error) {
    throw await readableError(error);
  }

  const url = URL.createObjectURL(response.data);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileNameFrom(response.headers['content-disposition']);
  link.rel = 'noopener';
  document.body.appendChild(link);
  link.click();
  link.remove();
  // İndirme başladıktan sonra bellek serbest bırakılır.
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}
