import { Chart } from 'primereact/chart';
import { useMemo } from 'react';
import { baseOptions, cssVar, formatNumber } from './chartTheme';

const DAY = new Intl.DateTimeFormat('tr-TR', { day: '2-digit', month: 'short' });
const MONTH = new Intl.DateTimeFormat('tr-TR', { month: 'short', year: '2-digit' });

/** @param {string} date "yyyy-MM-dd" @param {'day' | 'week' | 'month'} granularity */
export function formatBucket(date, granularity) {
  const value = new Date(`${date}T12:00:00`);
  return granularity === 'month' ? MONTH.format(value) : DAY.format(value);
}

/**
 * Zaman serisi çizgisi (tek seri). Haftalık adımda etiket haftanın başlangıç gününü gösterir.
 *
 * @param {{ points: { date: string, count: number }[], granularity: 'day' | 'week' | 'month',
 *   seriesLabel: string }} props
 */
export default function LineChart({ points, granularity, seriesLabel }) {
  const { data, options } = useMemo(() => {
    const base = baseOptions();
    const line = cssVar('--color-primary');
    return {
      data: {
        labels: points.map((point) => formatBucket(point.date, granularity)),
        datasets: [
          {
            label: seriesLabel,
            data: points.map((point) => point.count),
            borderColor: line,
            backgroundColor: `color-mix(in srgb, ${line} 14%, transparent)`,
            borderWidth: 2,
            fill: true,
            tension: 0.25,
            pointRadius: points.length > 40 ? 0 : 3,
            pointHoverRadius: 5,
            pointBackgroundColor: line,
            pointBorderColor: cssVar('--color-surface'),
            pointBorderWidth: 2,
          },
        ],
      },
      options: {
        ...base,
        interaction: { mode: 'index', intersect: false },
        plugins: {
          ...base.plugins,
          tooltip: {
            ...base.plugins.tooltip,
            callbacks: {
              title: ([point]) => (granularity === 'week' ? `${point.label} haftası` : point.label),
              label: (point) => ` ${seriesLabel}: ${formatNumber(point.parsed.y)}`,
            },
          },
        },
        scales: {
          ...base.scales,
          x: {
            ...base.scales.x,
            ticks: { ...base.scales.x.ticks, maxTicksLimit: 8, autoSkip: true },
          },
        },
      },
    };
  }, [points, granularity, seriesLabel]);

  return (
    <div style={{ height: 240 }}>
      <Chart type="line" data={data} options={options} style={{ height: '100%' }} />
    </div>
  );
}
