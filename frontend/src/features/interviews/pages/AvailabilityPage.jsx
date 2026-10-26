import { confirmDialog } from 'primereact/confirmdialog';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import PageHeader from '@/shared/components/PageHeader';
import { useToast } from '@/shared/components/toast';
import { ROUTES } from '@/shared/constants/routes';
import { useInterviews } from '@/shared/recruitment/queries';
import { getErrorMessage } from '@/shared/utils/errors';
import { formatTimeRange } from '@/shared/utils/format';
import WeekCalendar from '../components/WeekCalendar';
import { useAddAvailability, useDeleteAvailability, useMyAvailability } from '../queries';

/**
 * Mülakatçının müsaitliği: takvimde sürükleyerek aralık ekler, tıklayarak kaldırır. İK mülakat
 * planlarken bu aralıkları görür. Kendi mülakatları da takvimde görünür.
 */
export default function AvailabilityPage() {
  const toast = useToast();
  const navigate = useNavigate();
  const [range, setRange] = useState(null);
  const { data: slots = [] } = useMyAvailability(range);
  const { data: interviews = [] } = useInterviews(
    { ...range, mine: true, status: 'Scheduled' },
    { enabled: Boolean(range) },
  );
  const addAvailability = useAddAvailability();
  const deleteAvailability = useDeleteAvailability();

  const events = [
    ...slots.map((slot) => ({
      id: `slot-${slot.id}`,
      start: slot.startsAt,
      end: slot.endsAt,
      title: 'Müsait',
      classNames: ['event--availability'],
      extendedProps: { kind: 'slot', slot },
    })),
    ...interviews.map((interview) => ({
      id: interview.id,
      start: interview.startsAt,
      end: interview.endsAt,
      title: `${interview.typeName} · ${interview.candidateName}`,
      classNames: ['event--busy'],
      extendedProps: { kind: 'interview', interview },
    })),
  ];

  const add = ({ start, end }) =>
    addAvailability.mutate(
      { startsAt: start.toISOString(), endsAt: end.toISOString() },
      {
        onSuccess: () =>
          toast.success(
            `Müsaitlik eklendi: ${formatTimeRange(start.toISOString(), end.toISOString())}.`,
          ),
        onError: (error) => toast.error(getErrorMessage(error)),
      },
    );

  const onEventClick = (event) => {
    const { kind, slot, interview } = event.extendedProps;
    if (kind === 'interview') {
      navigate(ROUTES.interview(interview.id));
      return;
    }
    confirmDialog({
      header: 'Müsaitlik kaldırılsın mı?',
      message: `${formatTimeRange(slot.startsAt, slot.endsAt)} aralığı kaldırılacak. Planlanmış mülakatlar etkilenmez.`,
      acceptLabel: 'Kaldır',
      rejectLabel: 'Vazgeç',
      acceptClassName: 'p-button-danger',
      accept: () =>
        deleteAvailability.mutate(slot.id, {
          onSuccess: () => toast.success('Müsaitlik kaldırıldı.'),
          onError: (error) => toast.error(getErrorMessage(error)),
        }),
    });
  };

  return (
    <>
      <PageHeader
        title="Müsaitliğim"
        subtitle="Mülakat için uygun olduğunuz saatleri takvimde sürükleyerek işaretleyin; kaldırmak için üzerine tıklayın. İK planlarken bu saatleri görür."
      />
      <WeekCalendar
        events={events}
        onRangeChange={setRange}
        onSelect={add}
        onEventClick={onEventClick}
      />
    </>
  );
}
