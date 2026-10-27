import { formatNumber } from './chartTheme';

/**
 * İşe alım hunisi: her aşamaya ulaşan süreç sayısı ve bir önceki aşamadan geçiş yüzdesi.
 * Aşamalar sıralı olduğu için tek renkli (açıktan koyuya) bir ölçek kullanılır; sayılar çubuğun
 * dışına, normal metin rengiyle yazılır.
 *
 * @param {{ stages: { stage: string, count: number }[] }} props
 */
export default function FunnelChart({ stages }) {
  const top = Math.max(...stages.map((stage) => stage.count), 1);

  return (
    <ol className="funnel">
      {stages.map((stage, index) => {
        const previous = index === 0 ? null : stages[index - 1].count;
        const share = previous ? Math.round((stage.count / previous) * 100) : null;
        const shade = stages.length === 1 ? 0 : Math.round((index / (stages.length - 1)) * 100);

        return (
          <li key={stage.stage} className="funnel__row">
            <span className="funnel__label">{stage.stage}</span>
            <span className="funnel__track">
              <span
                className="funnel__bar"
                style={{
                  width: `${Math.max((stage.count / top) * 100, stage.count > 0 ? 2 : 0)}%`,
                  background: `color-mix(in srgb, var(--chart-seq-300), var(--chart-seq-700) ${shade}%)`,
                }}
              />
            </span>
            <span className="funnel__value">
              <strong>{formatNumber(stage.count)}</strong>
              {share !== null && (
                <span className="cell-sub" title="Bir önceki aşamadan geçen oran">
                  %{share}
                </span>
              )}
            </span>
          </li>
        );
      })}
    </ol>
  );
}
