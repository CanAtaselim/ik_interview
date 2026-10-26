import { Tag } from 'primereact/tag';
import StatusTag from '@/shared/components/StatusTag';
import { CV_REVIEW_DECISION } from '@/shared/constants/labels';
import '../cv-reviews.css';

/**
 * Değerlendirmenin kararı; süresi dolduysa yanında "Gecikti" etiketi.
 * @param {{ review: import('@/shared/api/cvReviews').CvReview }} props
 */
export default function ReviewStatus({ review }) {
  return (
    <span className="review-status">
      <StatusTag value={review.decision} map={CV_REVIEW_DECISION} />
      {review.isOverdue && <Tag value="Gecikti" severity="danger" icon="pi pi-clock" />}
    </span>
  );
}
