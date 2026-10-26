import {
  DndContext,
  DragOverlay,
  KeyboardSensor,
  PointerSensor,
  useDraggable,
  useDroppable,
  useSensor,
  useSensors,
} from '@dnd-kit/core';
import { Button } from 'primereact/button';
import { Tag } from 'primereact/tag';
import { useState } from 'react';
import { Link } from 'react-router';
import StatusTag from '@/shared/components/StatusTag';
import { CV_REVIEW_DECISION } from '@/shared/constants/labels';
import { ROUTES } from '@/shared/constants/routes';
import { formatDateTime, formatDaysSince } from '@/shared/utils/format';

/**
 * Süreç hattı: her aşama bir kolon, her süreç bir kart. Kart başka kolona sürüklenerek veya
 * üzerindeki "sonraki aşama" düğmesiyle taşınır (klavye ve dokunmatik için). Geçişin geçerli
 * olup olmadığına sunucu karar verir.
 * @param {{ stages: object[], cards: import('@/shared/api/applications').PipelineCard[],
 *   onMove: (card: object, stage: object) => void, onClose: (card: object) => void,
 *   onSuggest: (card: object) => void, onSchedule: (card: object) => void }} props
 */
export default function KanbanBoard({ stages, cards, onMove, onClose, onSuggest, onSchedule }) {
  const [draggingId, setDraggingId] = useState(null);
  // Küçük hareketler tıklama sayılsın ki karttaki bağlantı ve düğmeler çalışsın.
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 8 } }),
    useSensor(KeyboardSensor),
  );

  const draggingCard = cards.find((card) => card.applicationId === draggingId);

  const onDragEnd = ({ active, over }) => {
    setDraggingId(null);
    const card = cards.find((item) => item.applicationId === active.id);
    const stage = stages.find((item) => item.id === over?.id);
    if (card && stage && stage.id !== card.stageId) onMove(card, stage);
  };

  return (
    <DndContext
      sensors={sensors}
      onDragStart={({ active }) => setDraggingId(active.id)}
      onDragEnd={onDragEnd}
      onDragCancel={() => setDraggingId(null)}
    >
      <div className="kanban" role="list" aria-label="Süreç aşamaları">
        {stages.map((stage, index) => (
          <KanbanColumn
            key={stage.id}
            stage={stage}
            nextStage={stages[index + 1]}
            cards={cards.filter((card) => card.stageId === stage.id)}
            onMove={onMove}
            onClose={onClose}
            onSuggest={onSuggest}
            onSchedule={onSchedule}
          />
        ))}
      </div>
      <DragOverlay>{draggingCard && <CardBody card={draggingCard} />}</DragOverlay>
    </DndContext>
  );
}

// CV kararı, aday teknik mülakata geçene kadar kartta görünür; sonrasında bilgi kalabalığı olur.
const CV_DECISION_STAGES = ['Applied', 'HrScreening', 'CvReview'];
// Bu aşamalardaki kartta "mülakat planla" kısayolu bulunur.
const INTERVIEW_STAGES = ['TechnicalInterview', 'CaseStudy', 'FinalInterview'];

function KanbanColumn({ stage, nextStage, cards, onMove, onClose, onSuggest, onSchedule }) {
  const { setNodeRef, isOver } = useDroppable({ id: stage.id });

  return (
    <section ref={setNodeRef} className="kanban-column" data-over={isOver} role="listitem">
      <header className="kanban-column__header">
        <h2>{stage.name}</h2>
        <span className="kanban-column__count" aria-label={`${cards.length} aday`}>
          {cards.length}
        </span>
      </header>
      {stage.isOptional && <p className="kanban-column__hint">Atlanabilir</p>}
      <div className="kanban-column__cards">
        {cards.map((card) => (
          <KanbanCard
            key={card.applicationId}
            card={card}
            stage={stage}
            nextStage={nextStage}
            onMove={onMove}
            onClose={onClose}
            onSuggest={onSuggest}
            onSchedule={onSchedule}
          />
        ))}
      </div>
    </section>
  );
}

function KanbanCard({ card, stage, nextStage, onMove, onClose, onSuggest, onSchedule }) {
  const isActive = card.status === 'Active';
  const showCvDecision = CV_DECISION_STAGES.includes(stage.kind);
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: card.applicationId,
    disabled: !isActive,
  });

  return (
    <article
      ref={setNodeRef}
      className="kanban-card"
      data-dragging={isDragging}
      data-draggable={isActive}
      {...attributes}
      {...listeners}
    >
      <CardBody card={card} showCvDecision={showCvDecision} />
      {isActive && (
        <div className="kanban-card__actions">
          {stage.kind === 'CvReview' && (
            <Button
              icon="pi pi-send"
              text
              severity="secondary"
              title="Takım liderine öner"
              aria-label={`${card.candidateName} adayını takım liderine öner`}
              onClick={() => onSuggest(card)}
            />
          )}
          {INTERVIEW_STAGES.includes(stage.kind) && (
            <Button
              icon="pi pi-calendar-plus"
              text
              severity="secondary"
              title="Mülakat planla"
              aria-label={`${card.candidateName} için mülakat planla`}
              onClick={() => onSchedule(card)}
            />
          )}
          {nextStage && (
            <Button
              icon="pi pi-arrow-right"
              text
              severity="secondary"
              title={`“${nextStage.name}” aşamasına taşı`}
              aria-label={`${card.candidateName} adayını “${nextStage.name}” aşamasına taşı`}
              onClick={() => onMove(card, nextStage)}
            />
          )}
          <Button
            icon="pi pi-times"
            text
            severity="secondary"
            title="Süreci sonlandır"
            aria-label={`${card.candidateName} adayının sürecini sonlandır`}
            onClick={() => onClose(card)}
          />
        </div>
      )}
    </article>
  );
}

function CardBody({ card, showCvDecision = false }) {
  return (
    <div className="kanban-card__body">
      <Link to={ROUTES.candidate(card.candidateId)} className="table-link">
        {card.candidateName}
      </Link>
      {card.status === 'Hired' ? (
        <Tag value="İşe alındı" severity="success" />
      ) : (
        <span className="cell-sub">
          Bu aşamada: {formatDaysSince(card.stageEnteredAt).toLocaleLowerCase('tr')}
        </span>
      )}
      {card.status === 'Active' && card.nextInterviewAt && (
        <span className="cell-sub">
          <i className="pi pi-calendar" aria-hidden="true" /> Mülakat:{' '}
          {formatDateTime(card.nextInterviewAt)}
        </span>
      )}
      {showCvDecision && card.cvDecision && (
        <span className="kanban-card__badge">
          <span className="cell-sub">CV:</span>
          <StatusTag value={card.cvDecision} map={CV_REVIEW_DECISION} />
        </span>
      )}
    </div>
  );
}
