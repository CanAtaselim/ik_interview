/**
 * Grafik renkleri CSS belirteçlerinden (tokens.css) okunur; renk değerleri bileşenlerde yazılmaz.
 * Chart.js canvas'a çizdiği için CSS değişkenini çalışma anında çözümlemek gerekir.
 */
const CATEGORICAL_COUNT = 8;

/** @param {string} name Örn. '--color-primary'. */
export function cssVar(name) {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}

/** Kategori renkleri sabit sırayla; 8'den fazla seri olmaz, fazlası "Diğer" olarak birleşir. */
export function categoricalColors(count) {
  return Array.from({ length: Math.min(count, CATEGORICAL_COUNT) }, (_, i) =>
    cssVar(`--chart-${i + 1}`),
  );
}

/** Durumu olan serilerin (karar, öneri) renkleri: başarı/uyarı/tehlike/nötr. */
export const STATUS_TOKEN = Object.freeze({
  good: '--color-success',
  warning: '--color-warning',
  bad: '--color-danger',
  neutral: '--color-border-strong',
});

export function statusColor(kind) {
  return cssVar(STATUS_TOKEN[kind]);
}

/** Tüm grafiklerde ortak: ince ızgara, ikincil metin rengi, Türkçe sayı biçimi. */
export function baseOptions() {
  const text = cssVar('--color-text-muted');
  return {
    responsive: true,
    maintainAspectRatio: false,
    animation: { duration: 250 },
    font: { family: cssVar('--font-sans') },
    color: text,
    plugins: {
      legend: { display: false },
      tooltip: {
        backgroundColor: cssVar('--color-text'),
        titleColor: cssVar('--color-on-primary'),
        bodyColor: cssVar('--color-on-primary'),
        padding: 10,
        cornerRadius: 10,
        displayColors: true,
        boxPadding: 4,
      },
    },
    scales: {
      x: {
        grid: { display: false },
        ticks: { color: text },
        border: { color: cssVar('--chart-grid') },
      },
      y: {
        beginAtZero: true,
        grid: { color: cssVar('--chart-grid') },
        border: { display: false },
        ticks: { color: text, precision: 0 },
      },
    },
  };
}

const NUMBER = new Intl.NumberFormat('tr-TR');

/** @param {number | null | undefined} value */
export function formatNumber(value) {
  return value === null || value === undefined ? '—' : NUMBER.format(value);
}

/** Süreyi gün olarak yazar: 4.5 → "4,5 gün". @param {number | null | undefined} days */
export function formatDays(days) {
  return days === null || days === undefined ? '—' : `${NUMBER.format(days)} gün`;
}
