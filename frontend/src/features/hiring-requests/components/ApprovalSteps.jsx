import StatusTag from '@/shared/components/StatusTag';
import { APPROVAL_DECISION, ROLE_LABELS } from '@/shared/constants/labels';
import { formatDateTime } from '@/shared/utils/format';

/**
 * Talebin onay adımları: kim, ne karar verdi, ne zaman ve hangi notla.
 * @param {{ steps: { order: number, role: string, decision: string, decidedByName: string | null,
 *   note: string | null, decidedAt: string | null }[] }} props
 */
export default function ApprovalSteps({ steps }) {
  return (
    <ol className="approval-steps">
      {steps.map((step) => (
        <li key={step.order} className="approval-steps__item" data-decision={step.decision}>
          <span className="approval-steps__order" aria-hidden="true">
            {step.order}
          </span>
          <div className="approval-steps__body">
            <div className="approval-steps__head">
              <span className="approval-steps__role">{ROLE_LABELS[step.role] ?? step.role}</span>
              <StatusTag value={step.decision} map={APPROVAL_DECISION} />
            </div>
            {step.decidedByName && (
              <p className="approval-steps__meta">
                {step.decidedByName} · {formatDateTime(step.decidedAt)}
              </p>
            )}
            {step.note && <p className="approval-steps__note">“{step.note}”</p>}
          </div>
        </li>
      ))}
    </ol>
  );
}
