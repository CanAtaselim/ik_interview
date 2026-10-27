import { Link } from 'react-router';

/**
 * Dolu / hedef ilerleme listesi (açık pozisyonlar). Her satır pozisyonun süreç hattına gider.
 *
 * @param {{ items: { id: string, title: string, subtitle: string, value: number, target: number }[],
 *   toItem: (id: string) => string }} props
 */
export default function ProgressList({ items, toItem }) {
  return (
    <ul className="progress-list">
      {items.map((item) => {
        const share =
          item.target === 0 ? 0 : Math.min(100, Math.round((item.value / item.target) * 100));
        return (
          <li key={item.id}>
            <Link to={toItem(item.id)} className="progress-list__item">
              <span className="progress-list__main">
                <span className="task-list__title">{item.title}</span>
                <span className="cell-sub">{item.subtitle}</span>
              </span>
              <span className="progress-list__meter">
                <span
                  className="progress-list__bar"
                  role="progressbar"
                  aria-valuenow={item.value}
                  aria-valuemin={0}
                  aria-valuemax={item.target}
                  aria-label={`${item.title}: ${item.value} / ${item.target}`}
                >
                  <span style={{ width: `${share}%` }} />
                </span>
                <span className="progress-list__count">
                  {item.value} / {item.target}
                </span>
              </span>
            </Link>
          </li>
        );
      })}
    </ul>
  );
}
