import { Button } from 'primereact/button';
import { Skeleton } from 'primereact/skeleton';
import { useId, useState } from 'react';
import { Link } from 'react-router';
import './charts.css';

/**
 * Dashboard kartı: başlık, grafik, isteğe bağlı tablo görünümü ve "detaya git" bağlantısı.
 * Yükleme, hata ve boş durumu kart kendisi gösterir; çocuk bileşen yalnızca dolu veriyi çizer.
 *
 * @param {{
 *   title: string, hint?: string, query: { isPending: boolean, isError: boolean, refetch: () => void },
 *   isEmpty?: boolean, emptyText?: string, height?: number,
 *   table?: { columns: string[], rows: (string | number | null)[][] },
 *   to?: string, toLabel?: string, children: import('react').ReactNode }} props
 */
export default function ChartCard({
  title,
  hint,
  query,
  isEmpty = false,
  emptyText = 'Bu dönemde veri yok.',
  height = 240,
  table,
  to,
  toLabel = 'Detaya git',
  children,
}) {
  const [showTable, setShowTable] = useState(false);
  const titleId = useId();

  let body;
  if (query.isPending) body = <Skeleton height={`${height}px`} />;
  else if (query.isError)
    body = (
      <div className="chart-card__state" role="alert" style={{ minHeight: height }}>
        <p>Veri alınamadı.</p>
        <Button
          type="button"
          label="Yeniden dene"
          size="small"
          text
          onClick={() => query.refetch()}
        />
      </div>
    );
  else if (isEmpty)
    body = (
      <div className="chart-card__state" style={{ minHeight: height }}>
        <i className="pi pi-chart-bar" aria-hidden="true" />
        <p>{emptyText}</p>
      </div>
    );
  else if (showTable && table) body = <DataTable table={table} height={height} caption={title} />;
  else body = <div style={{ minHeight: height }}>{children}</div>;

  return (
    <section className="card chart-card" aria-labelledby={titleId}>
      <header className="chart-card__header">
        <div>
          <h2 id={titleId} className="chart-card__title">
            {title}
          </h2>
          {hint && <p className="cell-sub">{hint}</p>}
        </div>
        {table && !query.isPending && !query.isError && !isEmpty && (
          <Button
            type="button"
            icon={showTable ? 'pi pi-chart-bar' : 'pi pi-table'}
            text
            severity="secondary"
            size="small"
            aria-pressed={showTable}
            aria-label={showTable ? 'Grafiği göster' : 'Tablo olarak göster'}
            tooltip={showTable ? 'Grafiği göster' : 'Tablo olarak göster'}
            tooltipOptions={{ position: 'left' }}
            onClick={() => setShowTable((value) => !value)}
          />
        )}
      </header>
      {body}
      {to && (
        <Link to={to} className="chart-card__more">
          {toLabel} <i className="pi pi-angle-right" aria-hidden="true" />
        </Link>
      )}
    </section>
  );
}

function DataTable({ table, height, caption }) {
  return (
    <div className="chart-card__table" style={{ maxHeight: height }}>
      <table>
        <caption className="visually-hidden">{caption}</caption>
        <thead>
          <tr>
            {table.columns.map((column) => (
              <th key={column} scope="col">
                {column}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {table.rows.map((row) => (
            <tr key={row.join('|')}>
              {row.map((cell, index) => (
                <td key={table.columns[index]}>{cell ?? '—'}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
