import {
  closestCenter,
  DndContext,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
} from '@dnd-kit/core';
import {
  arrayMove,
  rectSortingStrategy,
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { Children, isValidElement } from 'react';
import { CARD_WIDTHS } from '../layout';
import { useDashboardLayoutContext } from './DashboardLayoutContext';

/**
 * Kartları kendi içinde yer değiştirebilen ızgara. Her çocuk `cardId`, `title` ve varsayılan `width` (altı sütunlu ızgarada 2, 3, 4 ya da 6) alan bir karttır.
 * Düzenleme modu kapalıyken de aynı ağaç çizilir; mod değişince grafikler yeniden oluşmaz.
 *
 * @param {{ id: string, className?: string, children: import('react').ReactNode }} props
 */
export default function SortableGrid({ id, className = 'dashboard-grid', children }) {
  const { editing, orderFor, setOrder, widthFor, setWidth } = useDashboardLayoutContext();
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  const cards = new Map(
    Children.toArray(children)
      .filter(isValidElement)
      .map((child) => [child.props.cardId, child]),
  );
  const ids = orderFor(id, [...cards.keys()]);

  const handleDragEnd = ({ active, over }) => {
    if (!over || active.id === over.id) return;
    setOrder(id, arrayMove(ids, ids.indexOf(active.id), ids.indexOf(over.id)));
  };

  return (
    <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
      <SortableContext items={ids} strategy={rectSortingStrategy}>
        <div className={`${className}${editing ? ' dashboard-grid--editing' : ''}`}>
          {ids.map((cardId) => {
            const card = cards.get(cardId);
            return (
              <SortableCell
                key={cardId}
                id={cardId}
                width={widthFor(cardId, card.props.width ?? 2)}
                title={card.props.title}
                editing={editing}
                onWidthChange={(width) => setWidth(cardId, width)}
              >
                {card}
              </SortableCell>
            );
          })}
        </div>
      </SortableContext>
    </DndContext>
  );
}

function SortableCell({ id, width, title, editing, onWidthChange, children }) {
  const {
    attributes,
    listeners,
    setNodeRef,
    setActivatorNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({
    id,
    disabled: !editing,
  });

  return (
    <div
      ref={setNodeRef}
      className="dashboard-cell"
      data-width={width}
      data-dragging={isDragging}
      style={{ transform: CSS.Translate.toString(transform), transition }}
    >
      {children}
      {editing && (
        <div className="dashboard-cell__toolbar">
          <button
            type="button"
            ref={setActivatorNodeRef}
            className="dashboard-cell__handle"
            aria-label={`Kartı taşı: ${title}. Klavyede boşluk tuşuyla tutun, ok tuşlarıyla taşıyın.`}
            {...attributes}
            {...listeners}
          >
            <i className="pi pi-arrows-alt" aria-hidden="true" />
          </button>
          <div
            className="dashboard-cell__widths"
            role="group"
            aria-label={`Kart genişliği: ${title}`}
          >
            {CARD_WIDTHS.map((option) => (
              <button
                key={option.value}
                type="button"
                className="dashboard-cell__width"
                aria-pressed={width === option.value}
                title={option.title}
                aria-label={option.title}
                onClick={() => onWidthChange(option.value)}
              >
                {option.label}
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
