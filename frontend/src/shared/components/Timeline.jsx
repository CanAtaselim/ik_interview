import { Timeline as PrimeTimeline } from 'primereact/timeline';
import { TIMELINE_ICONS } from '@/shared/constants/labels';
import { formatDateTime } from '@/shared/utils/format';
import './Timeline.css';

/**
 * Olayların zaman sıralı listesi: ne oldu, kim yaptı, ne zaman.
 * @param {{ entries: { id: string, type: string, summary: string, actorName: string | null,
 *   occurredAt: string }[] }} props
 */
export default function Timeline({ entries }) {
  return (
    <PrimeTimeline
      className="timeline"
      value={entries}
      align="left"
      dataKey="id"
      marker={(entry) => (
        <span className="timeline__icon" aria-hidden="true">
          <i className={TIMELINE_ICONS[entry.type] ?? 'pi pi-circle'} />
        </span>
      )}
      opposite={(entry) => (
        <time className="timeline__date" dateTime={entry.occurredAt}>
          {formatDateTime(entry.occurredAt)}
        </time>
      )}
      content={(entry) => (
        <div className="timeline__content">
          <p className="timeline__summary">{entry.summary}</p>
          <p className="cell-sub">
            {entry.actorName ?? 'Sistem'}
            <span className="timeline__inline-date"> · {formatDateTime(entry.occurredAt)}</span>
          </p>
        </div>
      )}
    />
  );
}
