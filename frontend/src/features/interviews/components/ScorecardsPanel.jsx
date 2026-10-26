import { Button } from 'primereact/button';
import { Dialog } from 'primereact/dialog';
import { InputTextarea } from 'primereact/inputtextarea';
import { SelectButton } from 'primereact/selectbutton';
import { Tag } from 'primereact/tag';
import { useState } from 'react';
import { Link } from 'react-router';
import { Role } from '@/shared/auth/roles';
import { useAuth } from '@/shared/auth/useAuth';
import FormField from '@/shared/components/FormField';
import StatusTag from '@/shared/components/StatusTag';
import { useToast } from '@/shared/components/toast';
import {
  DEBRIEF_DECISION,
  RECOMMENDATION,
  SCORECARD_PROGRESS,
  toOptions,
} from '@/shared/constants/labels';
import { ROUTES } from '@/shared/constants/routes';
import {
  useInterviewScorecards,
  useRecordDebrief,
  useReopenScorecard,
  useScorecard,
} from '@/shared/recruitment/queries';
import ScorecardView, { formatScore } from '@/shared/recruitment/ScorecardView';
import { getErrorMessage } from '@/shared/utils/errors';
import { formatDateTime } from '@/shared/utils/format';

const DECISION_OPTIONS = toOptions(DEBRIEF_DECISION);

/**
 * Mülakatın değerlendirmeleri: her mülakatçının durumu, görülebilen puanlar ve İK'nın ortak
 * kararı. Mülakatçı kendi değerlendirmesini göndermeden diğerlerinin puanını göremez.
 * @param {{ interview: import('@/shared/api/interviews').Interview }} props
 */
export default function ScorecardsPanel({ interview }) {
  const { user, hasAnyRole } = useAuth();
  const { data, isLoading } = useInterviewScorecards(interview.id);
  const [viewing, setViewing] = useState(null);

  if (isLoading || !data) return null;
  if (interview.status === 'Cancelled' || interview.status === 'NoShow') return null;

  const myProgress = data.interviewers.find((item) => item.userId === user.id)?.progress;

  return (
    <section className="card card--padded" aria-labelledby="interview-scorecards">
      <h2 id="interview-scorecards" className="section-title">
        Değerlendirmeler
      </h2>
      <div className="detail-sections">
        {interview.isMine && (
          <Link
            to={ROUTES.scorecard(interview.id)}
            className={`p-button ${myProgress === 'Submitted' ? 'p-button-outlined' : ''} link-button`}
          >
            <i className="pi pi-star" aria-hidden="true" />
            &nbsp;
            {myProgress === 'Submitted'
              ? 'Değerlendirmemi gör'
              : myProgress === 'Draft'
                ? 'Değerlendirmeye devam et'
                : 'Değerlendir'}
          </Link>
        )}

        {data.othersHidden && (
          <p className="muted">
            Diğer mülakatçıların puanlarını kendi değerlendirmenizi gönderdikten sonra görürsünüz.
          </p>
        )}

        <ul className="plain-list">
          {data.interviewers.map((item) => (
            <li key={item.userId} className="dialog-title">
              <span>{item.displayName}</span>
              <StatusTag value={item.progress} map={SCORECARD_PROGRESS} />
              {item.recommendation && (
                <StatusTag value={item.recommendation} map={RECOMMENDATION} />
              )}
              {item.weightedScore !== null && (
                <span className="cell-sub">Puan {formatScore(item.weightedScore)}</span>
              )}
              {item.canView && item.scorecardId && item.progress === 'Submitted' && (
                <Button label="Gör" text onClick={() => setViewing(item)} />
              )}
            </li>
          ))}
        </ul>

        {data.averageScore !== null && (
          <div className="detail-sections">
            <strong>Ortalama puan: {formatScore(data.averageScore)}</strong>
            {data.competencyScores.length > 0 && (
              <div className="tag-list">
                {data.competencyScores.map((competency) => (
                  <Tag
                    key={competency.competencyId}
                    value={`${competency.name}: ${formatScore(competency.score)}`}
                    severity="info"
                  />
                ))}
              </div>
            )}
          </div>
        )}

        {data.debrief && (
          <div>
            <h3 className="section-title">Ortak karar</h3>
            <p className="dialog-title">
              <StatusTag value={data.debrief.decision} map={DEBRIEF_DECISION} />
              <span className="cell-sub">
                {data.debrief.decidedByName} · {formatDateTime(data.debrief.decidedAt)}
              </span>
            </p>
            <p className="multiline">{data.debrief.reason}</p>
          </div>
        )}

        {data.canDebrief && (
          <DebriefForm interviewId={interview.id} hasDecision={Boolean(data.debrief)} />
        )}
        {hasAnyRole([Role.HrSpecialist, Role.HrManager]) &&
          !data.canDebrief &&
          interview.status !== 'Completed' && (
            <p className="muted">
              Ortak karar, mülakat “Yapıldı” olarak işaretlenip tüm değerlendirmeler gönderilince
              verilir.
            </p>
          )}
      </div>

      {viewing && <ScorecardDialog interviewer={viewing} onClose={() => setViewing(null)} />}
    </section>
  );
}

function DebriefForm({ interviewId, hasDecision }) {
  const toast = useToast();
  const record = useRecordDebrief();
  const [decision, setDecision] = useState(null);
  const [reason, setReason] = useState('');
  const [error, setError] = useState(null);

  const submit = () => {
    if (!decision || !reason.trim()) {
      setError('Kararı seçip gerekçesini yazmalısınız.');
      return;
    }
    record.mutate(
      { interviewId, decision, reason: reason.trim() },
      {
        onSuccess: () => {
          toast.success('Ortak karar kaydedildi.');
          setDecision(null);
          setReason('');
        },
        onError: (mutationError) => toast.error(getErrorMessage(mutationError)),
      },
    );
  };

  return (
    <div className="decision-box">
      <span className="section-title">{hasDecision ? 'Ortak kararı değiştir' : 'Ortak karar'}</span>
      <SelectButton
        value={decision}
        options={DECISION_OPTIONS}
        onChange={(event) => {
          setDecision(event.value);
          setError(null);
        }}
        aria-label="Ortak karar"
      />
      <InputTextarea
        id="debrief-reason"
        aria-label="Ortak karar gerekçesi"
        rows={2}
        autoResize
        maxLength={2000}
        placeholder="Mülakatçıların görüşleri ve kararın gerekçesi"
        value={reason}
        onChange={(event) => {
          setReason(event.target.value);
          setError(null);
        }}
      />
      {error && (
        <small className="form-field__error" role="alert">
          {error}
        </small>
      )}
      <Button label="Ortak kararı kaydet" loading={record.isPending} onClick={submit} />
    </div>
  );
}

/** Gönderilmiş bir değerlendirme; İK Yöneticisi buradan düzeltme için yeniden açabilir. */
function ScorecardDialog({ interviewer, onClose }) {
  const toast = useToast();
  const { hasAnyRole } = useAuth();
  const { data: scorecard } = useScorecard(interviewer.scorecardId);
  const reopen = useReopenScorecard();
  const [reason, setReason] = useState('');
  const [reasonError, setReasonError] = useState(null);
  const canReopen = hasAnyRole([Role.HrManager]) && scorecard?.status === 'Submitted';

  const submitReopen = () => {
    if (!reason.trim()) {
      setReasonError('Yeniden açma gerekçesini yazmalısınız.');
      return;
    }
    reopen.mutate(
      { id: interviewer.scorecardId, reason: reason.trim() },
      {
        onSuccess: () => {
          toast.success(
            `${interviewer.displayName} adlı mülakatçının değerlendirmesi düzeltme için açıldı.`,
          );
          onClose();
        },
        onError: (error) => toast.error(getErrorMessage(error)),
      },
    );
  };

  const footer = canReopen && (
    <div className="dialog-actions">
      <Button
        type="button"
        label="Düzeltme için aç"
        severity="danger"
        outlined
        loading={reopen.isPending}
        onClick={submitReopen}
      />
    </div>
  );

  return (
    <Dialog
      visible
      header={`${interviewer.displayName} · değerlendirme`}
      footer={footer}
      onHide={onClose}
      className="form-dialog form-dialog--wide"
      modal
    >
      {!scorecard ? (
        <p className="muted">Yükleniyor…</p>
      ) : (
        <div className="detail-sections">
          <ScorecardView scorecard={scorecard} />
          {canReopen && (
            <FormField label="Yeniden açma gerekçesi" htmlFor="reopen-reason" error={reasonError}>
              <InputTextarea
                id="reopen-reason"
                rows={2}
                autoResize
                maxLength={2000}
                value={reason}
                invalid={Boolean(reasonError)}
                onChange={(event) => {
                  setReason(event.target.value);
                  setReasonError(null);
                }}
              />
            </FormField>
          )}
        </div>
      )}
    </Dialog>
  );
}
