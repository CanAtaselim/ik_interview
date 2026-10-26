import dayjs from 'dayjs';
import { Button } from 'primereact/button';
import { Checkbox } from 'primereact/checkbox';
import { Message } from 'primereact/message';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { HR_ROLES, INTERVIEWER_ROLES } from '@/shared/auth/roles';
import { useAuth } from '@/shared/auth/useAuth';
import DictionarySelect from '@/shared/components/DictionarySelect';
import PageHeader from '@/shared/components/PageHeader';
import UserSelect from '@/shared/components/UserSelect';
import { INTERVIEW_STATUS } from '@/shared/constants/labels';
import { ROUTES } from '@/shared/constants/routes';
import { useInterviews } from '@/shared/recruitment/queries';
import ScheduleInterviewDialog from '@/shared/recruitment/ScheduleInterviewDialog';
import { getErrorMessage } from '@/shared/utils/errors';
import WeekCalendar from '../components/WeekCalendar';

/**
 * Mülakat takvimi. İK tüm mülakatları görür, mülakatçı ve odaya göre süzer; boş bir saati seçerek
 * mülakat planlar. Mülakatçılar yalnızca kendi mülakatlarını görür.
 */
export default function InterviewCalendarPage() {
  const navigate = useNavigate();
  const { hasAnyRole } = useAuth();
  const isHr = hasAnyRole(HR_ROLES);
  const [range, setRange] = useState(null);
  const [interviewerId, setInterviewerId] = useState(null);
  const [roomId, setRoomId] = useState(null);
  const [showCancelled, setShowCancelled] = useState(false);
  // Planlama penceresi: null | { start?: Date, durationMinutes?: number }
  const [planning, setPlanning] = useState(null);

  const {
    data = [],
    isError,
    error,
  } = useInterviews(
    { ...range, interviewerUserId: interviewerId ?? undefined, roomId: roomId ?? undefined },
    { enabled: Boolean(range) },
  );

  const events = data
    .filter((interview) => showCancelled || interview.status !== 'Cancelled')
    .map((interview) => ({
      id: interview.id,
      start: interview.startsAt,
      end: interview.endsAt,
      title: interview.candidateName,
      classNames: [`event--${interview.status.toLowerCase()}`],
      extendedProps: interview,
    }));

  const renderEvent = (event) => {
    const interview = event.extendedProps;
    const status =
      interview.status === 'Scheduled' ? null : INTERVIEW_STATUS[interview.status].label;
    return (
      <div>
        <div className="calendar-event__title">{interview.candidateName}</div>
        <div className="calendar-event__sub">
          {[
            interview.typeName,
            interview.roomName ?? (interview.mode === 'Online' ? 'Online' : null),
            status,
          ]
            .filter(Boolean)
            .join(' · ')}
        </div>
      </div>
    );
  };

  return (
    <>
      <PageHeader
        title="Mülakatlar"
        subtitle={
          isHr
            ? 'Takvimde boş bir saati seçerek veya “Mülakat planla” ile yeni mülakat planlayın.'
            : 'Mülakatçı olarak atandığınız mülakatlar.'
        }
        actions={
          isHr && (
            <Button
              label="Mülakat planla"
              icon="pi pi-calendar-plus"
              onClick={() => setPlanning({})}
            />
          )
        }
      />

      {isHr && (
        <section className="card toolbar calendar-filters" aria-label="Takvim filtreleri">
          <UserSelect
            roles={[...INTERVIEWER_ROLES]}
            value={interviewerId}
            onChange={setInterviewerId}
            inputId="filter-interviewer"
            placeholder="Tüm mülakatçılar"
          />
          <DictionarySelect
            type="Room"
            inputId="filter-room"
            value={roomId}
            onChange={setRoomId}
            placeholder="Tüm odalar"
            showClear
          />
          <label className="toolbar__check" htmlFor="filter-cancelled">
            <Checkbox
              inputId="filter-cancelled"
              checked={showCancelled}
              onChange={(event) => setShowCancelled(event.checked)}
            />
            İptal edilenleri göster
          </label>
        </section>
      )}

      {isError && (
        <Message severity="error" text={getErrorMessage(error)} className="toolbar__message" />
      )}

      <WeekCalendar
        events={events}
        onRangeChange={setRange}
        onSelect={
          isHr
            ? ({ start, end }) =>
                setPlanning({
                  start,
                  // Tek tıklama 30 dakikalık dilim seçer; varsayılan süre bir saattir.
                  durationMinutes: Math.max(dayjs(end).diff(start, 'minute'), 60),
                })
            : undefined
        }
        onEventClick={(event) => navigate(ROUTES.interview(event.id))}
        renderEvent={renderEvent}
      />

      {planning && (
        <ScheduleInterviewDialog
          initialStart={planning.start ?? null}
          initialDurationMinutes={planning.durationMinutes ?? null}
          onClose={() => setPlanning(null)}
        />
      )}
    </>
  );
}
