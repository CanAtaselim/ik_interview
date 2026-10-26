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
    <ol className="timeline">
      {entries.map((entry) => (
        <li key={entry.id} className="timeline__item">
          <span className="timeline__icon" aria-hidden="true">
            <i className={TIMELINE_ICONS[entry.type] ?? 'pi pi-circle'} />
          </span>
          <div>
            <p className="timeline__summary">{entry.summary}</p>
            <p className="cell-sub">
              {entry.actorName ?? 'Sistem'} · {formatDateTime(entry.occurredAt)}
            </p>
          </div>
        </li>
      ))}
    </ol>
  );
}
