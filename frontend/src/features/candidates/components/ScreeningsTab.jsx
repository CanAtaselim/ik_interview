import { Button } from 'primereact/button';
import { confirmDialog } from 'primereact/confirmdialog';
import { useState } from 'react';
import EmptyState from '@/shared/components/EmptyState';
import { useToast } from '@/shared/components/toast';
import { SCREENING_CHANNEL_LABELS } from '@/shared/constants/labels';
import { getErrorMessage } from '@/shared/utils/errors';
import { formatDateTime } from '@/shared/utils/format';
import { useDeleteScreening, useStartScreening } from '../queries';
import ScreeningForm from './ScreeningForm';

/**
 * Adayın ön görüşme kayıtları. Bir adayın birden çok kaydı olabilir; aynı anda biri açıktır.
 * @param {{ candidate: object }} props
 */
export default function ScreeningsTab({ candidate }) {
  const toast = useToast();
  const startScreening = useStartScreening();
  const deleteScreening = useDeleteScreening();
  // Açılışta en son kayıt açık gelir.
  const [openId, setOpenId] = useState(candidate.screenings[0]?.id ?? null);

  const start = () =>
    startScreening.mutate(candidate.id, {
      onSuccess: setOpenId,
      onError: (error) => toast.error(getErrorMessage(error)),
    });

  const confirmDelete = (screening, summary) =>
    confirmDialog({
      header: 'Ön görüşme kaydı silinsin mi?',
      message: `${summary} tarihli kayıt aday kartından kaldırılacak.`,
      acceptLabel: 'Kaydı sil',
      rejectLabel: 'Vazgeç',
      acceptClassName: 'p-button-danger',
      accept: () =>
        deleteScreening.mutate(
          { id: candidate.id, screeningId: screening.id },
          {
            onSuccess: () => {
              if (openId === screening.id) setOpenId(null);
              toast.success('Ön görüşme kaydı silindi.');
            },
            onError: (error) => toast.error(getErrorMessage(error)),
          },
        ),
    });

  const startButton = (
    <Button
      label="Yeni ön görüşme"
      icon="pi pi-plus"
      loading={startScreening.isPending}
      onClick={start}
    />
  );

  if (candidate.screenings.length === 0) {
    return (
      <EmptyState
        icon="pi pi-comments"
        title="Henüz ön görüşme kaydı yok"
        description="Adayla görüştüğünüzde notlarınızı buraya yazın. Formu istediğiniz kadar, istediğiniz zaman doldurabilirsiniz."
        action={startButton}
      />
    );
  }

  return (
    <div className="screenings">
      <div className="screenings__actions">{startButton}</div>
      {candidate.screenings.map((screening) => {
        const isOpen = screening.id === openId;
        const summary = [
          formatDateTime(screening.interviewedAt ?? screening.createdAt),
          SCREENING_CHANNEL_LABELS[screening.channel],
          screening.interviewerName,
          screening.overallRating && `${screening.overallRating}/5`,
        ].filter(Boolean);

        return (
          <article key={screening.id} className="screening" data-open={isOpen}>
            <div className="screening__bar">
              <button
                type="button"
                className="screening__header"
                aria-expanded={isOpen}
                onClick={() => setOpenId(isOpen ? null : screening.id)}
              >
                <i
                  className={isOpen ? 'pi pi-chevron-down' : 'pi pi-chevron-right'}
                  aria-hidden="true"
                />
                <span>{summary.join(' · ')}</span>
              </button>
              <Button
                type="button"
                icon="pi pi-trash"
                text
                severity="danger"
                aria-label="Ön görüşme kaydını sil"
                tooltip="Kaydı sil"
                tooltipOptions={{ position: 'left' }}
                onClick={() =>
                  confirmDelete(
                    screening,
                    formatDateTime(screening.interviewedAt ?? screening.createdAt),
                  )
                }
              />
            </div>
            {isOpen && (
              <div className="screening__body">
                <ScreeningForm candidateId={candidate.id} screening={screening} />
              </div>
            )}
          </article>
        );
      })}
    </div>
  );
}
