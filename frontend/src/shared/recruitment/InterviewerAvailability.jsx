import dayjs from 'dayjs';
import { Tag } from 'primereact/tag';
import { formatTime } from '@/shared/utils/format';
import { usePlanningSchedule } from './queries';
import './InterviewerAvailability.css';

const overlaps = (range, startsAt, endsAt) =>
  new Date(range.startsAt) < endsAt && startsAt < new Date(range.endsAt);

const covers = (range, startsAt, endsAt) =>
  new Date(range.startsAt) <= startsAt && endsAt <= new Date(range.endsAt);

const ranges = (items) =>
  items.map((item) => `${formatTime(item.startsAt)}–${formatTime(item.endsAt)}`).join(', ');

const OUTLOOK_NOTES = {
  NoMailbox: "Outlook'ta posta kutusu bulunamadı; Outlook takvimine bakılamadı.",
  Unavailable: 'Outlook takvimine ulaşılamadı; yalnızca sistemdeki bilgiye bakıldı.',
};

/**
 * Seçilen gün için mülakatçıların müsaitliği ve dolu saatleri; seçilen saatin her kişi için
 * uygun olup olmadığı tek bakışta görünür. Outlook bağlıysa oradaki meşgul saatler de gösterilir
 * (konu ve katılımcı görünmez). Müsaitlik dışı veya Outlook'ta meşgul olsa da planlama engellenmez.
 * @param {{ startsAt: Date, endsAt: Date, userIds: string[], roomId: string | null,
 *   excludeInterviewId: string | null }} props
 */
export default function InterviewerAvailability({
  startsAt,
  endsAt,
  userIds,
  roomId,
  excludeInterviewId,
}) {
  const day = dayjs(startsAt).startOf('day');
  const { data, isLoading, isError } = usePlanningSchedule({
    from: day.toISOString(),
    to: day.add(1, 'day').toISOString(),
    userIds,
    roomId,
    excludeInterviewId,
  });

  if (isLoading) return <p className="muted">Takvimler yükleniyor…</p>;
  if (isError || !data) return null;

  const statusOf = (person) => {
    if (person.busy.some((item) => overlaps(item, startsAt, endsAt)))
      return { label: 'Dolu', severity: 'danger' };
    if (person.outlookBusy.some((item) => overlaps(item, startsAt, endsAt)))
      return { label: "Outlook'ta meşgul", severity: 'danger' };
    if (person.available.some((item) => covers(item, startsAt, endsAt)))
      return { label: 'Müsait', severity: 'success' };
    if (person.available.length > 0) return { label: 'Müsaitlik dışı', severity: 'warning' };
    return { label: 'Müsaitlik bildirmemiş', severity: 'secondary' };
  };
  const roomBusy = data.roomBusy.some((item) => overlaps(item, startsAt, endsAt));

  return (
    <section className="availability-panel" aria-label={`${day.format('D MMMM dddd')} takvimi`}>
      <h3 className="availability-panel__title">{day.format('D MMMM dddd')} için takvimler</h3>
      <ul className="availability-panel__list">
        {data.interviewers.map((person) => {
          const status = statusOf(person);
          return (
            <li key={person.userId} className="availability-panel__row">
              <div className="availability-panel__name">
                <strong>{person.displayName}</strong>
                <Tag value={status.label} severity={status.severity} />
              </div>
              <p className="cell-sub">
                {person.available.length > 0
                  ? `Müsait: ${ranges(person.available)}`
                  : 'Bu gün için müsaitlik girilmemiş'}
                {person.busy.length > 0 &&
                  ` · Mülakatları: ${person.busy
                    .map(
                      (item) =>
                        `${formatTime(item.startsAt)}–${formatTime(item.endsAt)} (${item.title})`,
                    )
                    .join(', ')}`}
                {person.outlook === 'Ok' &&
                  person.outlookBusy.length > 0 &&
                  ` · Outlook'ta meşgul: ${ranges(person.outlookBusy)}`}
              </p>
              {OUTLOOK_NOTES[person.outlook] && (
                <p className="cell-sub availability-panel__note">{OUTLOOK_NOTES[person.outlook]}</p>
              )}
            </li>
          );
        })}
        {roomId && (
          <li className="availability-panel__row">
            <div className="availability-panel__name">
              <strong>Oda</strong>
              <Tag value={roomBusy ? 'Dolu' : 'Boş'} severity={roomBusy ? 'danger' : 'success'} />
            </div>
            {data.roomBusy.length > 0 && (
              <p className="cell-sub">Ayrılmış saatler: {ranges(data.roomBusy)}</p>
            )}
          </li>
        )}
      </ul>
    </section>
  );
}
