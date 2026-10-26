import { Link } from 'react-router';
import { ROUTES } from '@/shared/constants/routes';

/**
 * Aynı e-posta veya telefonla kayıtlı adayları gösteren uyarı. Engellemez; İK karar verir.
 * @param {{ duplicates: import('../api').DuplicateCandidate[], message?: string }} props
 */
export default function DuplicateWarning({
  duplicates,
  message = 'Aynı e-posta veya telefonla kayıtlı aday var. Yeni kayıt açmadan önce mevcut kaydı kontrol edin:',
}) {
  if (duplicates.length === 0) return null;

  return (
    <div className="duplicate-warning" role="alert">
      <i className="pi pi-exclamation-triangle" aria-hidden="true" />
      <div>
        <p>{message}</p>
        <ul>
          {duplicates.map((duplicate) => (
            <li key={duplicate.id}>
              <Link to={ROUTES.candidate(duplicate.id)} target="_blank" rel="noreferrer">
                {duplicate.fullName}
              </Link>
              <span className="muted">
                {[duplicate.email, duplicate.phone]
                  .filter(Boolean)
                  .map((contact) => ` · ${contact}`)}
              </span>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}
