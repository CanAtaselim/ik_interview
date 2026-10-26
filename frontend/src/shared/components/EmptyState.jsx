import './EmptyState.css';

/**
 * Gösterilecek veri olmadığında: ikon + bir cümle + (varsa) birincil buton.
 * @param {{ icon: string, title: string, description?: string, action?: import('react').ReactNode }} props
 */
export default function EmptyState({ icon, title, description, action }) {
  return (
    <div className="empty-state">
      <span className="empty-state__icon" aria-hidden="true">
        <i className={icon} />
      </span>
      <p className="empty-state__title">{title}</p>
      {description && <p className="empty-state__description">{description}</p>}
      {action && <div className="empty-state__action">{action}</div>}
    </div>
  );
}
