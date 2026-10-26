import trLocale from '@fullcalendar/core/locales/tr';
import interactionPlugin from '@fullcalendar/interaction';
import FullCalendar from '@fullcalendar/react';
import timeGridPlugin from '@fullcalendar/timegrid';
import { useEffect, useState } from 'react';
import '../calendar.css';

const MOBILE_QUERY = '(max-width: 768px)';

function useIsMobile() {
  const [isMobile, setIsMobile] = useState(() => window.matchMedia(MOBILE_QUERY).matches);

  useEffect(() => {
    const media = window.matchMedia(MOBILE_QUERY);
    const update = () => setIsMobile(media.matches);
    media.addEventListener('change', update);
    return () => media.removeEventListener('change', update);
  }, []);

  return isMobile;
}

/**
 * Haftalık saat takvimi (mobilde günlük). Mülakat takvimi ve müsaitlik ekranı aynı görünümü
 * kullanır; saatler Türkiye saatiyle gösterilir.
 * @param {{ events: object[], onRangeChange: (range: { from: string, to: string }) => void,
 *   onSelect?: (range: { start: Date, end: Date }) => void, onEventClick?: (event: object) => void,
 *   renderEvent?: (event: object) => import('react').ReactNode }} props
 */
export default function WeekCalendar({
  events,
  onRangeChange,
  onSelect,
  onEventClick,
  renderEvent,
}) {
  const isMobile = useIsMobile();

  return (
    <div className="card interview-calendar">
      <FullCalendar
        key={isMobile ? 'day' : 'week'}
        plugins={[timeGridPlugin, interactionPlugin]}
        locale={trLocale}
        timeZone="local"
        initialView={isMobile ? 'timeGridDay' : 'timeGridWeek'}
        headerToolbar={{ left: 'prev,next today', center: 'title', right: '' }}
        firstDay={1}
        allDaySlot={false}
        slotMinTime="08:00:00"
        slotMaxTime="20:00:00"
        slotDuration="00:30:00"
        snapDuration="00:15:00"
        scrollTime="09:00:00"
        height="auto"
        expandRows
        nowIndicator
        businessHours={{ daysOfWeek: [1, 2, 3, 4, 5], startTime: '09:00', endTime: '18:00' }}
        selectable={Boolean(onSelect)}
        selectMirror
        selectAllow={(range) => range.start > new Date()}
        longPressDelay={300}
        events={events}
        datesSet={(range) =>
          onRangeChange({ from: range.start.toISOString(), to: range.end.toISOString() })
        }
        select={(range) => {
          range.view.calendar.unselect();
          onSelect?.({ start: range.start, end: range.end });
        }}
        eventClick={(info) => {
          info.jsEvent.preventDefault();
          onEventClick?.(info.event);
        }}
        eventContent={renderEvent && ((arg) => renderEvent(arg.event))}
      />
    </div>
  );
}
