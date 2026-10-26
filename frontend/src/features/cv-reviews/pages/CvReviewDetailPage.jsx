import { Button } from 'primereact/button';
import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { getReviewDocumentUrl } from '@/shared/api/cvReviews';
import { HR_ROLES } from '@/shared/auth/roles';
import { useAuth } from '@/shared/auth/useAuth';
import DetailList from '@/shared/components/DetailList';
import PageHeader from '@/shared/components/PageHeader';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { ROUTES } from '@/shared/constants/routes';
import NotFoundPage from '@/shared/pages/NotFoundPage';
import { useCvReview } from '@/shared/recruitment/queries';
import RestrictedCandidateSections from '@/shared/recruitment/RestrictedCandidateSections';
import { getErrorMessage } from '@/shared/utils/errors';
import { formatDate, formatDateTime } from '@/shared/utils/format';
import DecisionDialog from '../components/DecisionDialog';
import ReviewStatus from '../components/ReviewStatus';
import '../cv-reviews.css';

/**
 * Değerlendirme sayfası: adayın takım lideri görünümü (CV, ön görüşme özeti) ve karar. Ücret,
 * İK yorumu, iletişim ve kara liste bilgisi sunucudan hiç gelmez.
 */
export default function CvReviewDetailPage() {
  const { id } = useParams();
  const { hasAnyRole } = useAuth();
  const { data, isLoading, isError, error } = useCvReview(id);
  // Karar penceresi hangi kararla açıldı: null | 'Positive' | 'Negative' | 'OnHold'
  const [deciding, setDeciding] = useState(null);

  if (isLoading) return <TableSkeleton rows={6} />;
  if (isError) {
    return error.response?.status === 404 ? (
      <NotFoundPage />
    ) : (
      <p role="alert">{getErrorMessage(error)}</p>
    );
  }

  const { review, candidate } = data;
  const isFinal = review.decision === 'Positive' || review.decision === 'Negative';

  const decisionButtons = review.canDecide && (
    <div className="review-decision__actions">
      <Button
        label="Olumlu"
        icon="pi pi-check"
        severity="success"
        onClick={() => setDeciding('Positive')}
      />
      <Button
        label="Olumsuz"
        icon="pi pi-times"
        severity="danger"
        outlined
        onClick={() => setDeciding('Negative')}
      />
      {review.decision !== 'OnHold' && (
        <Button
          label="Beklet"
          icon="pi pi-clock"
          severity="secondary"
          outlined
          onClick={() => setDeciding('OnHold')}
        />
      )}
    </div>
  );

  return (
    <>
      <Link to={ROUTES.cvReviews} className="back-link">
        <i className="pi pi-arrow-left" aria-hidden="true" /> CV değerlendirmeleri
      </Link>
      <PageHeader
        title={
          <span className="dialog-title">
            {review.candidateName}
            <ReviewStatus review={review} />
          </span>
        }
        subtitle={[review.positionTitle ?? 'Pozisyon belirtilmeden', review.departmentName]
          .filter(Boolean)
          .join(' · ')}
        actions={
          hasAnyRole(HR_ROLES) && (
            <Link
              to={ROUTES.candidate(review.candidateId)}
              className="p-button p-button-outlined link-button"
            >
              Aday kartını aç
            </Link>
          )
        }
      />

      <div className="record-layout">
        <div className="record-layout__column">
          <RestrictedCandidateSections
            candidate={candidate}
            getDownloadUrl={(documentId) => getReviewDocumentUrl(review.id, documentId)}
          />
        </div>

        <aside className="record-layout__column record-layout__column--side">
          <section className="card card--padded review-decision" aria-labelledby="review-decision">
            <h2 id="review-decision" className="section-title">
              Karar
            </h2>
            {review.decision === 'Pending' && !review.canDecide && (
              <p className="muted">{review.reviewerName} adlı takım liderinin kararı bekleniyor.</p>
            )}
            {review.decision === 'Pending' && review.canDecide && (
              <p>
                CV’yi ve ön görüşme özetini inceleyip kararınızı verin. Son tarih:{' '}
                <strong>{formatDate(review.dueAt)}</strong>
              </p>
            )}
            {review.decision !== 'Pending' && (
              <>
                <DetailList
                  items={[
                    { label: 'Karar veren', value: review.reviewerName },
                    {
                      label: isFinal ? 'Karar tarihi' : 'Bekletilme tarihi',
                      value: formatDateTime(review.decidedAt),
                    },
                    ...(review.rejectionReasonName
                      ? [{ label: 'Ret nedeni', value: review.rejectionReasonName }]
                      : []),
                  ]}
                />
                <p className="multiline">{review.decisionReason}</p>
              </>
            )}
            {decisionButtons}
          </section>

          <section className="card card--padded" aria-labelledby="review-suggestion">
            <h2 id="review-suggestion" className="section-title">
              Öneri
            </h2>
            <div className="detail-sections">
              <DetailList
                items={[
                  { label: 'Öneren', value: review.suggestedByName },
                  { label: 'Takım lideri', value: review.reviewerName },
                  { label: 'Öneri tarihi', value: formatDateTime(review.suggestedAt) },
                  { label: 'Son tarih', value: formatDate(review.dueAt) },
                ]}
              />
              {review.suggestionNote && (
                <div>
                  <h3 className="section-title">İK’nın notu</h3>
                  <p className="multiline">{review.suggestionNote}</p>
                </div>
              )}
            </div>
          </section>
        </aside>
      </div>

      {deciding && (
        <DecisionDialog
          review={review}
          initialDecision={deciding}
          onClose={() => setDeciding(null)}
        />
      )}
    </>
  );
}
