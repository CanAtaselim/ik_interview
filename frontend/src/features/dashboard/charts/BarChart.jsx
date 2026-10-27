import { Chart } from 'primereact/chart';
import { useMemo } from 'react';
import { baseOptions, cssVar, formatNumber } from './chartTheme';

const ROW_HEIGHT = 34;
const MIN_HEIGHT = 160;
const MAX_LABEL = 24;

const shorten = (label) => (label.length > MAX_LABEL ? `${label.slice(0, MAX_LABEL - 1)}…` : label);

/**
 * Yatay çubuk grafiği: kıyaslama (birim, kaynak, kişi). Tek seri olduğu için tek renk kullanılır;
 * uzun adlar kısaltılır, tam ad ipucunda görünür.
 *
 * @param {{ items: { label: string, count: number }[], unit?: string, color?: string, max?: number,
 *   decimals?: boolean }} props
 *   `color`: CSS belirteci (varsayılan marka rengi). `max`: sabit üst sınır (ör. 5 üzerinden puan).
 */
export default function BarChart({
  items,
  unit = '',
  color = '--color-primary',
  max,
  decimals = false,
}) {
  const { data, options } = useMemo(() => {
    const base = baseOptions();
    const bar = cssVar(color);
    return {
      data: {
        labels: items.map((item) => shorten(item.label)),
        datasets: [
          {
            data: items.map((item) => item.count),
            backgroundColor: bar,
            borderRadius: 4,
            borderSkipped: 'start',
            maxBarThickness: 18,
          },
        ],
      },
      options: {
        ...base,
        indexAxis: 'y',
        plugins: {
          ...base.plugins,
          tooltip: {
            ...base.plugins.tooltip,
            callbacks: {
              title: ([point]) => items[point.dataIndex].label,
              label: (point) => ` ${formatNumber(point.parsed.x)}${unit ? ` ${unit}` : ''}`,
            },
          },
        },
        scales: {
          x: {
            ...base.scales.y,
            max,
            ticks: { ...base.scales.y.ticks, precision: decimals ? 1 : 0 },
          },
          y: { ...base.scales.x, ticks: { ...base.scales.x.ticks, autoSkip: false } },
        },
      },
    };
  }, [items, unit, color, max, decimals]);

  return (
    <div style={{ height: Math.max(MIN_HEIGHT, items.length * ROW_HEIGHT + 32) }}>
      <Chart type="bar" data={data} options={options} style={{ height: '100%' }} />
    </div>
  );
}
