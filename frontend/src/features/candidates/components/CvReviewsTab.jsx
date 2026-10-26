import { Button } from 'primereact/button';
import { Tag } from 'primereact/tag';
import EmptyState from '@/shared/components/EmptyState';
import StatusTag from '@/shared/components/StatusTag';
import { CV_REVIEW_DECISION } from '@/shared/constants/labels';
import { formatDate } from '@/shared/utils/format';

/**
 * Adayın takım liderlerine önerildiği değerlendirmeler ve kararları. Karar değiştirilemez;
 * gerekirse aynı lidere yeni öneri açılır.
 * @param {{ reviews: import('@/shared/api/cvReviews').CvReview[], onSuggest: () => void }} props
 */
export default function CvReviewsTab({ reviews, onSuggest }) {
  const suggestButton = (
    <Button label="Takım liderine öner" icon="pi pi-send" onClick={onSuggest} />
  );

  if (reviews.length === 0) {
    return (
      <EmptyState
        icon="pi pi-send"
        title="Aday henüz bir takım liderine önerilmedi"
        description="CV’yi ve ön görüşme özetini takım liderine gönderin; ücret bilgisi ve İK yorumu paylaşılmaz."
        action={suggestButton}
      />
    );
  }

  return (
    <div className="screenings">
      <div className="screenings__actions">{suggestButton}</div>
      <ul className="application-list">
        {reviews.map((review) => (
          <li key={review.id} className="application-list__item">
            <div className="application-list__main">
              <div className="dialog-title">
                <strong>{review.reviewerName}</strong>
                <StatusTag value={review.decision} map={CV_REVIEW_DECISION} />
                {review.isOverdue && <Tag value="Gecikti" severity="danger" icon="pi pi-clock" />}
              </div>
              <p className="cell-sub">
                {[
                  review.positionTitle ?? 'Pozisyon belirtilmeden',
                  review.departmentName,
                  `Öneren: ${review.suggestedByName}`,
                  formatDate(review.suggestedAt),
                  review.decidedAt
                    ? `Karar: ${formatDate(review.decidedAt)}`
                    : `Son tarih: ${formatDate(review.dueAt)}`,
                ]
                  .filter(Boolean)
                  .join(' · ')}
              </p>
              {review.decisionReason && (
                <p>
                  {review.rejectionReasonName && <strong>{review.rejectionReasonName}: </strong>}
                  {review.decisionReason}
                </p>
              )}
              {review.suggestionNote && <p className="muted">İK notu: {review.suggestionNote}</p>}
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}
