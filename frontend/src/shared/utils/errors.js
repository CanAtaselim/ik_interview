const NETWORK_ERROR = 'Sunucuya ulaşılamadı. Bağlantınızı kontrol edip tekrar deneyin.';
const UNKNOWN_ERROR = 'İşleminiz tamamlanamadı. Lütfen daha sonra tekrar deneyin.';

/**
 * Backend'in ProblemDetails yanıtından kullanıcıya gösterilecek Türkçe mesajı çıkarır.
 * @param {unknown} error axios hatası
 * @returns {string}
 */
export function getErrorMessage(error) {
  if (!error?.response) return NETWORK_ERROR;
  return error.response.data?.detail ?? UNKNOWN_ERROR;
}

/**
 * ProblemDetails içindeki alan hatalarını react-hook-form'a basar.
 * @param {unknown} error axios hatası
 * @param {(field: string, error: { type: string, message: string }) => void} setError
 * @returns {boolean} En az bir alan hatası basıldıysa true.
 */
export function applyFieldErrors(error, setError) {
  const fieldErrors = error?.response?.data?.errors;
  if (!fieldErrors) return false;

  const entries = Object.entries(fieldErrors);
  entries.forEach(([field, messages]) => setError(field, { type: 'server', message: messages[0] }));
  return entries.length > 0;
}
