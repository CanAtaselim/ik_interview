import { BLACKLIST_MATCH_KIND_LABELS } from '@/shared/constants/labels';

const DEFAULT_MESSAGE =
  'Bu kişi kara listedeki bir kayıtla eşleşiyor. Yine de kaydederseniz bu kararınız kayda geçer.';

/**
 * Kişinin kara listedeki bir kayıtla eşleştiğini gösteren uyarı. Engellemez; İK onaylarsa işlem
 * yapılır ve bu onay kayda geçer.
 * @param {{ matches: import('@/shared/api/blacklist').BlacklistMatch[], message?: string }} props
 */
export default function BlacklistWarning({ matches, message = DEFAULT_MESSAGE }) {
  if (matches.length === 0) return null;

  return (
    <div className="notice notice--danger" role="alert">
      <i className="pi pi-ban" aria-hidden="true" />
      <div>
        <p>{message}</p>
        <ul>
          {matches.map((match) => (
            <li key={match.entryId}>
              <strong>{match.fullName}</strong> — {match.reasonName} (
              {BLACKLIST_MATCH_KIND_LABELS[match.kind]})
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}
