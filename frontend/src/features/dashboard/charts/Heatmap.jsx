export const DAY_NAMES = [
  'Pazartesi',
  'Salı',
  'Çarşamba',
  'Perşembe',
  'Cuma',
  'Cumartesi',
  'Pazar',
];
const DAY_SHORT = ['Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cmt', 'Paz'];
const DEFAULT_FROM = 8;
const DEFAULT_TO = 19;

/**
 * Gün × saat yoğunluk haritası. Büyüklük tek renkle (açıktan koyuya) gösterilir; boş hücre nötr kalır.
 * Saat aralığı veriye göre genişler (varsayılan 08-19).
 *
 * @param {{ cells: { day: number, hour: number, count: number }[], unit: string }} props
 */
export default function Heatmap({ cells, unit }) {
  const hours = cells.map((cell) => cell.hour);
  const from = Math.min(DEFAULT_FROM, ...hours);
  const to = Math.max(DEFAULT_TO, ...hours);
  const range = Array.from({ length: to - from + 1 }, (_, i) => from + i);
  const max = Math.max(...cells.map((cell) => cell.count), 1);
  const counts = new Map(cells.map((cell) => [`${cell.day}-${cell.hour}`, cell.count]));

  return (
    <div className="heatmap" role="img" aria-label={`Gün ve saate göre ${unit} yoğunluğu`}>
      <div
        className="heatmap__grid"
        style={{ gridTemplateColumns: `3rem repeat(${range.length}, minmax(0, 1fr))` }}
      >
        <span />
        {range.map((hour) => (
          <span key={hour} className="heatmap__hour">
            {String(hour).padStart(2, '0')}
          </span>
        ))}
        {DAY_SHORT.map((day, dayIndex) => (
          <HeatRow
            key={day}
            day={day}
            dayIndex={dayIndex}
            range={range}
            counts={counts}
            max={max}
            unit={unit}
          />
        ))}
      </div>
      <div className="heatmap__scale" aria-hidden="true">
        <span className="cell-sub">Az</span>
        <span className="heatmap__ramp" />
        <span className="cell-sub">Çok</span>
      </div>
    </div>
  );
}

function HeatRow({ day, dayIndex, range, counts, max, unit }) {
  return (
    <>
      <span className="heatmap__day">{day}</span>
      {range.map((hour) => {
        const count = counts.get(`${dayIndex}-${hour}`) ?? 0;
        const level = count === 0 ? null : Math.round(15 + (count / max) * 85);
        return (
          <span
            key={hour}
            className="heatmap__cell"
            title={`${DAY_NAMES[dayIndex]} ${String(hour).padStart(2, '0')}:00 · ${count} ${unit}`}
            style={
              level === null
                ? undefined
                : {
                    background: `color-mix(in srgb, var(--chart-seq-100), var(--chart-seq-700) ${level}%)`,
                  }
            }
          />
        );
      })}
    </>
  );
}
