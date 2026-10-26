import dayjs from 'dayjs';
import 'dayjs/locale/tr';
import relativeTime from 'dayjs/plugin/relativeTime';
import timezone from 'dayjs/plugin/timezone';
import utc from 'dayjs/plugin/utc';

dayjs.extend(utc);
dayjs.extend(timezone);
dayjs.extend(relativeTime);
dayjs.locale('tr');

const ZONE = 'Europe/Istanbul';

/**
 * Avatar için baş harfler: "Ayşe Yılmaz" → "AY".
 * @param {string} displayName
 */
export function getInitials(displayName) {
  const words = displayName.trim().split(/\s+/).filter(Boolean);
  if (words.length === 0) return '';

  const letters = words.length === 1 ? [words[0][0]] : [words[0][0], words.at(-1)[0]];
  return letters.join('').toLocaleUpperCase('tr');
}

/**
 * Sunucudan gelen UTC zamanı İstanbul saatiyle "09.10.2026 14:32" biçiminde yazar.
 * @param {string | null | undefined} value
 */
export function formatDateTime(value) {
  return value ? dayjs(value).tz(ZONE).format('DD.MM.YYYY HH:mm') : '';
}

/** Geçen süre: "5 dakika önce", "2 gün önce". @param {string} value */
export function formatRelative(value) {
  return value ? dayjs(value).fromNow() : '';
}

/** Saat: "14:30" (İstanbul). @param {string | null | undefined} value */
export function formatTime(value) {
  return value ? dayjs(value).tz(ZONE).format('HH:mm') : '';
}

/**
 * Mülakat zamanı: "21 Ekim Salı, 14:00–15:00" (İstanbul).
 * @param {string} startsAt @param {string} endsAt
 */
export function formatTimeRange(startsAt, endsAt) {
  const start = dayjs(startsAt).tz(ZONE);
  return `${start.format('D MMMM dddd, HH:mm')}–${dayjs(endsAt).tz(ZONE).format('HH:mm')}`;
}

/**
 * Zaman veya saat içermeyen tarih ("2026-12-01") → "01.12.2026".
 * @param {string | null | undefined} value
 */
export function formatDate(value) {
  if (!value) return '';
  // Yalnızca tarih taşıyan değer saat dilimine çevrilmez; yoksa bir gün kayabilir.
  const isDateOnly = /^\d{4}-\d{2}-\d{2}$/.test(value);
  return (isDateOnly ? dayjs(value) : dayjs(value).tz(ZONE)).format('DD.MM.YYYY');
}

/**
 * Bir tarihten bugüne geçen süre, gün olarak: "Bugün", "1 gündür", "12 gündür".
 * @param {string} value
 */
export function formatDaysSince(value) {
  const days = dayjs().startOf('day').diff(dayjs(value).startOf('day'), 'day');
  return days <= 0 ? 'Bugün' : `${days} gündür`;
}

const numberFormat = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 2 });
const CURRENCY_SYMBOLS = { TRY: '₺', USD: '$', EUR: '€' };

/**
 * Para tutarı: `{ amount: 185000, currency: 'TRY', isNet: true }` → "185.000 ₺ net".
 * @param {{ amount: number, currency: string, isNet: boolean } | null | undefined} money
 */
export function formatMoney(money) {
  if (!money) return '';
  const symbol = CURRENCY_SYMBOLS[money.currency] ?? money.currency;
  return `${numberFormat.format(money.amount)} ${symbol} ${money.isNet ? 'net' : 'brüt'}`;
}

/**
 * Aralıklı tutar: 100000–130000 net → "100.000 – 130.000 ₺ net"; üst değer yoksa tek tutar.
 * @param {{ amount: number, currency: string, isNet: boolean } | null | undefined} money alt değer
 * @param {number | null | undefined} max üst değer
 */
export function formatMoneyRange(money, max) {
  if (!money) return '';
  if (max === null || max === undefined) return formatMoney(money);
  const symbol = CURRENCY_SYMBOLS[money.currency] ?? money.currency;
  return `${numberFormat.format(money.amount)} – ${numberFormat.format(max)} ${symbol} ${money.isNet ? 'net' : 'brüt'}`;
}

/**
 * Toplam ay → "3 yıl 4 ay", "6 ay", "3 yıl". Boşsa boş metin.
 * @param {number | null | undefined} totalMonths
 */
export function formatMonths(totalMonths) {
  if (totalMonths === null || totalMonths === undefined) return '';
  const years = Math.floor(totalMonths / 12);
  const months = totalMonths % 12;
  return [years > 0 && `${years} yıl`, (months > 0 || years === 0) && `${months} ay`]
    .filter(Boolean)
    .join(' ');
}

/** Dosya boyutu: 1536 → "1,5 KB". @param {number} bytes */
export function formatFileSize(bytes) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${numberFormat.format(Math.round(bytes / 102.4) / 10)} KB`;
  return `${numberFormat.format(Math.round(bytes / (1024 * 102.4)) / 10)} MB`;
}

/**
 * Takvim bileşeninin verdiği `Date` → API'nin beklediği "YYYY-MM-DD".
 * @param {Date | null | undefined} date
 */
export function toDateOnly(date) {
  return date ? dayjs(date).format('YYYY-MM-DD') : null;
}

/** @param {string | null | undefined} value "YYYY-MM-DD" */
export function fromDateOnly(value) {
  return value ? dayjs(value).toDate() : null;
}
