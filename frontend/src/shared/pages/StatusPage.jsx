import { Link } from 'react-router';
import EmptyState from '@/shared/components/EmptyState';
import { ROUTES } from '@/shared/constants/routes';
import './StatusPage.css';

/**
 * 403 / 404 / hata sayfalarının ortak görünümü: ne olduğunu söyler, ana sayfaya döndürür.
 * @param {{ icon: string, title: string, description: string }} props
 */
export default function StatusPage({ icon, title, description }) {
  return (
    <main className="status-page">
      <EmptyState
        icon={icon}
        title={title}
        description={description}
        action={
          // Gezinme olduğu için buton görünümlü bir bağlantı.
          <Link to={ROUTES.home} className="p-button p-component status-page__link">
            <span className="p-button-icon p-button-icon-left pi pi-home" aria-hidden="true" />
            <span className="p-button-label">Ana sayfaya dön</span>
          </Link>
        }
      />
    </main>
  );
}
