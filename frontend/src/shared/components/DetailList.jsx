import './DetailList.css';

/**
 * Etiket–değer çiftlerini ızgara halinde gösterir. Boş değerler "—" olarak yazılır.
 * @param {{ items: { label: string, value: import('react').ReactNode }[] }} props
 */
export default function DetailList({ items }) {
  return (
    <dl className="detail-list">
      {items.map((item) => (
        <div key={item.label} className="detail-list__item">
          <dt>{item.label}</dt>
          <dd>{item.value || item.value === 0 ? item.value : '—'}</dd>
        </div>
      ))}
    </dl>
  );
}
