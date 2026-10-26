import './PageHeader.css';

/**
 * Sayfa başlığı; sağında sayfanın birincil aksiyonları durur.
 * @param {{ title: import('react').ReactNode, subtitle?: string, actions?: import('react').ReactNode }} props
 */
export default function PageHeader({ title, subtitle, actions }) {
  return (
    <header className="page-header">
      <div className="page-header__main">
        <h1 className="page-header__title">{title}</h1>
        {subtitle && <p className="page-header__subtitle">{subtitle}</p>}
      </div>
      {actions && <div className="page-header__actions">{actions}</div>}
    </header>
  );
}
