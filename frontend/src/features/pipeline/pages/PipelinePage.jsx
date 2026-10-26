import { Button } from 'primereact/button';
import { useState } from 'react';
import { Link, useParams } from 'react-router';
import EmptyState from '@/shared/components/EmptyState';
import PageHeader from '@/shared/components/PageHeader';
import StatusTag from '@/shared/components/StatusTag';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { useToast } from '@/shared/components/toast';
import { APPLICATION_STATUS, POSITION_STATUS } from '@/shared/constants/labels';
import { ROUTES } from '@/shared/constants/routes';
import NotFoundPage from '@/shared/pages/NotFoundPage';
import BlacklistRequestDialog from '@/shared/recruitment/BlacklistRequestDialog';
import CloseApplicationDialog from '@/shared/recruitment/CloseApplicationDialog';
import {
  useMoveApplication,
  usePositionPipeline,
  useUndoApplicationChange,
} from '@/shared/recruitment/queries';
import ScheduleInterviewDialog from '@/shared/recruitment/ScheduleInterviewDialog';
import SuggestCandidateDialog from '@/shared/recruitment/SuggestCandidateDialog';
import { getErrorMessage } from '@/shared/utils/errors';
import AddCandidateDialog from '../components/AddCandidateDialog';
import BypassDialog from '../components/BypassDialog';
import KanbanBoard from '../components/KanbanBoard';
import '../pipeline.css';

/** Pozisyonun süreç hattı (kanban). */
export default function PipelinePage() {
  const { id } = useParams();
  const toast = useToast();
  const { data: board, isLoading, isError, error } = usePositionPipeline(id);
  const move = useMoveApplication(id);
  const undo = useUndoApplicationChange();
  const [adding, setAdding] = useState(false);
  const [closing, setClosing] = useState(null);
  const [blacklisting, setBlacklisting] = useState(null);
  const [suggesting, setSuggesting] = useState(null);
  const [scheduling, setScheduling] = useState(null);
  // Bekleyen değerlendirmelere rağmen geçiş: { card, stage }
  const [bypassing, setBypassing] = useState(null);

  if (isLoading) return <TableSkeleton rows={6} />;
  if (isError) {
    return error.response?.status === 404 ? (
      <NotFoundPage />
    ) : (
      <p role="alert">{getErrorMessage(error)}</p>
    );
  }

  const undoChange = (applicationId) =>
    undo.mutate(applicationId, {
      onSuccess: () => toast.success('Son değişiklik geri alındı.'),
      onError: (undoError) => toast.error(getErrorMessage(undoError)),
    });

  // Kart hemen yeni kolonda görünür; sunucu reddederse geri döner ve nedeni bildirilir.
  const moveCard = (card, stage) =>
    move.mutate(
      { applicationId: card.applicationId, stageId: stage.id },
      {
        onSuccess: () =>
          toast.action(`${card.candidateName} “${stage.name}” aşamasına taşındı.`, {
            label: 'Geri al',
            onAction: () => undoChange(card.applicationId),
          }),
        onError: (moveError) => {
          const problem = moveError.response?.data;
          if (problem?.code === 'Application.ScorecardsPending' && problem.canBypass) {
            toast.action(getErrorMessage(moveError), {
              label: 'Gerekçeyle geçir',
              onAction: () => setBypassing({ card, stage }),
            });
          } else toast.error(getErrorMessage(moveError));
        },
      },
    );

  const bypassMove = (reason) =>
    move.mutate(
      {
        applicationId: bypassing.card.applicationId,
        stageId: bypassing.stage.id,
        bypassReason: reason,
      },
      {
        onSuccess: () => {
          toast.success(
            `${bypassing.card.candidateName} “${bypassing.stage.name}” aşamasına geçirildi; gerekçe kayda geçti.`,
          );
          setBypassing(null);
        },
        onError: (moveError) => toast.error(getErrorMessage(moveError)),
      },
    );

  const isOpen = board.positionStatus === 'Open';

  return (
    <>
      <Link to={ROUTES.position(board.positionId)} className="back-link">
        <i className="pi pi-arrow-left" aria-hidden="true" /> {board.positionTitle}
      </Link>
      <PageHeader
        title={
          <span className="dialog-title">
            Süreç hattı
            <StatusTag value={board.positionStatus} map={POSITION_STATUS} />
          </span>
        }
        subtitle={`${board.positionTitle} · doluluk ${board.filledCount} / ${board.headcount}`}
        actions={
          <>
            <Link
              to={ROUTES.comparison(board.positionId)}
              className="p-button p-component p-button-outlined link-button"
            >
              <span
                className="p-button-icon p-button-icon-left pi pi-chart-bar"
                aria-hidden="true"
              />
              <span className="p-button-label">Karşılaştır</span>
            </Link>
            <Button
              label="Aday ekle"
              icon="pi pi-plus"
              disabled={!isOpen}
              title={isOpen ? undefined : 'Yalnızca açık pozisyona aday eklenebilir.'}
              onClick={() => setAdding(true)}
            />
          </>
        }
      />

      {board.active.length === 0 && board.closed.length === 0 ? (
        <section className="card">
          <EmptyState
            icon="pi pi-users"
            title="Bu pozisyonda henüz aday yok"
            description="Havuzdan aday ekleyin; aday ilk aşamadan başlar ve kartı sürükleyerek ilerletirsiniz."
            action={
              isOpen && (
                <Button label="Aday ekle" icon="pi pi-plus" onClick={() => setAdding(true)} />
              )
            }
          />
        </section>
      ) : (
        <KanbanBoard
          stages={board.stages}
          cards={board.active}
          onMove={moveCard}
          onClose={setClosing}
          onSuggest={setSuggesting}
          onSchedule={setScheduling}
        />
      )}

      {board.closed.length > 0 && (
        <section className="card closed-applications" aria-labelledby="closed-title">
          <h2 id="closed-title" className="section-title">
            Sonlanan süreçler ({board.closed.length})
          </h2>
          <ul>
            {board.closed.map((card) => (
              <li key={card.applicationId}>
                <Link to={ROUTES.candidate(card.candidateId)} className="table-link">
                  {card.candidateName}
                </Link>
                <StatusTag value={card.status} map={APPLICATION_STATUS} />
                <span className="muted">{card.closingReason}</span>
                {card.canUndo && (
                  <Button
                    label="Geri al"
                    icon="pi pi-undo"
                    text
                    disabled={undo.isPending}
                    onClick={() => undoChange(card.applicationId)}
                  />
                )}
              </li>
            ))}
          </ul>
        </section>
      )}

      {adding && (
        <AddCandidateDialog
          positionId={board.positionId}
          candidateIdsInProcess={board.active
            .filter((card) => card.status === 'Active')
            .map((card) => card.candidateId)}
          onClose={() => setAdding(false)}
        />
      )}

      {closing && (
        <CloseApplicationDialog
          application={{
            id: closing.applicationId,
            candidateName: closing.candidateName,
            positionTitle: board.positionTitle,
          }}
          onClose={() => setClosing(null)}
          onClosed={({ openBlacklistRequest }) => {
            if (openBlacklistRequest)
              setBlacklisting({ id: closing.candidateId, name: closing.candidateName });
            setClosing(null);
          }}
        />
      )}

      {suggesting && (
        <SuggestCandidateDialog
          candidate={{ id: suggesting.candidateId, name: suggesting.candidateName }}
          applications={[{ id: suggesting.applicationId, positionTitle: board.positionTitle }]}
          onClose={() => setSuggesting(null)}
        />
      )}

      {scheduling && (
        <ScheduleInterviewDialog
          application={{
            id: scheduling.applicationId,
            candidateName: scheduling.candidateName,
            positionTitle: board.positionTitle,
          }}
          onClose={() => setScheduling(null)}
        />
      )}

      {bypassing && (
        <BypassDialog
          candidateName={bypassing.card.candidateName}
          stageName={bypassing.stage.name}
          loading={move.isPending}
          onConfirm={bypassMove}
          onClose={() => setBypassing(null)}
        />
      )}

      {blacklisting && (
        <BlacklistRequestDialog candidate={blacklisting} onClose={() => setBlacklisting(null)} />
      )}
    </>
  );
}
