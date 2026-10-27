import { Chart } from 'primereact/chart';
import { useMemo } from 'react';
import { baseOptions, categoricalColors, cssVar, formatNumber } from './chartTheme';

/**
 * Halka grafiği: 6'dan az dilimli parça-bütün dağılımı. Kimlik yalnızca renkle verilmez; yanında
 * ad, sayı ve yüzde yazan bir açıklama listesi vardır. `colors` verilirse (ör. durum renkleri) sırayla
 * kullanılır, verilmezse sabit kategori sırası atanır.
 *
 * @param {{ items: { label: string, count: number }[], colors?: string[], centerLabel?: string }} props
 *   `colors`: CSS belirteci adları (ör. '--color-success').
 */
export default function DoughnutChart({ items, colors, centerLabel = 'Toplam' }) {
  const total = items.reduce((sum, item) => sum + item.count, 0);

  const { data, options, swatches } = useMemo(() => {
    const base = baseOptions();
    const palette = colors ? colors.map(cssVar) : categoricalColors(items.length);
    return {
      swatches: palette,
      data: {
        labels: items.map((item) => item.label),
        datasets: [
          {
            data: items.map((item) => item.count),
            backgroundColor: palette,
            borderColor: cssVar('--color-surface'),
            borderWidth: 2,
          },
        ],
      },
      options: {
        ...base,
        cutout: '68%',
        scales: {},
        plugins: {
          ...base.plugins,
          tooltip: {
            ...base.plugins.tooltip,
            callbacks: { label: (point) => ` ${point.label}: ${formatNumber(point.parsed)}` },
          },
        },
      },
    };
  }, [items, colors]);

  return (
    <div className="doughnut">
      <div className="doughnut__plot">
        <Chart type="doughnut" data={data} options={options} style={{ height: '100%' }} />
        <div className="doughnut__center" aria-hidden="true">
          <strong>{formatNumber(total)}</strong>
          <span className="cell-sub">{centerLabel}</span>
        </div>
      </div>
      <ul className="legend">
        {items.map((item, index) => (
          <li key={item.label} className="legend__item">
            <span
              className="legend__swatch"
              style={{ background: swatches[index] }}
              aria-hidden="true"
            />
            <span className="legend__label">{item.label}</span>
            <span className="legend__value">
              {formatNumber(item.count)}
              {total > 0 && (
                <span className="cell-sub"> · %{Math.round((item.count / total) * 100)}</span>
              )}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
