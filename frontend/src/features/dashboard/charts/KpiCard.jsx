import { Skeleton } from 'primereact/skeleton';
import { Link } from 'react-router';
import { formatNumber } from './chartTheme';
import './charts.css';

/**
 * Tek sayılık gösterge kartı. `previous` verilirse bir önceki dönemle karşılaştırma oku çıkar;
 * `lowerIsBetter` süre gibi azalması iyi olan göstergelerde okun rengini tersler.
 *
 * @param {{ label: string, value: number | string | null | undefined, previous?: number | null,
 *   note?: string, tone?: 'default' | 'danger', lowerIsBetter?: boolean, isPending?: boolean,
 *   to?: string, format?: (value: number) => string }} props
 */
export default function KpiCard({
  label,
  value,
  previous,
  note,
  tone = 'default',
  lowerIsBetter = false,
  isPending = false,
  to,
  format = formatNumber,
}) {
  const content = (
    <>
      <span className="kpi-card__label">{label}</span>
      {isPending ? (
        <Skeleton width="4rem" height="2rem" />
      ) : (
        <span className="kpi-card__value" data-tone={tone}>
          {typeof value === 'number' ? format(value) : (value ?? '—')}
        </span>
      )}
      {!isPending && <Trend value={value} previous={previous} lowerIsBetter={lowerIsBetter} />}
      {!isPending && note && <span className="cell-sub">{note}</span>}
    </>
  );

  return to ? (
    <Link to={to} className="card kpi-card kpi-card--link">
      {content}
    </Link>
  ) : (
    <div className="card kpi-card">{content}</div>
  );
}

function Trend({ value, previous, lowerIsBetter }) {
  if (typeof value !== 'number' || typeof previous !== 'number') return null;
  if (previous === 0 && value === 0) return <span className="cell-sub">Önceki dönemle aynı</span>;

  const delta = value - previous;
  if (delta === 0) return <span className="cell-sub">Önceki dönemle aynı</span>;

  const percent = previous === 0 ? null : Math.round((Math.abs(delta) / previous) * 100);
  const good = lowerIsBetter ? delta < 0 : delta > 0;

  return (
    <span className="kpi-card__trend" data-good={good}>
      <i className={delta > 0 ? 'pi pi-arrow-up' : 'pi pi-arrow-down'} aria-hidden="true" />
      {percent === null ? `+${formatNumber(delta)}` : `%${percent}`}
      <span className="visually-hidden">{delta > 0 ? ' arttı' : ' azaldı'}</span>
      <span className="cell-sub"> önceki döneme göre</span>
    </span>
  );
}
