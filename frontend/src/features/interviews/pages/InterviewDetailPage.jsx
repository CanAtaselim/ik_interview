import { Button } from 'primereact/button';
import { Dialog } from 'primereact/dialog';
import { InputTextarea } from 'primereact/inputtextarea';
import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { getInterviewDocumentUrl } from '@/shared/api/interviews';
import { HR_ROLES } from '@/shared/auth/roles';
import { useAuth } from '@/shared/auth/useAuth';
import DetailList from '@/shared/components/DetailList';
import FormField from '@/shared/components/FormField';
import PageHeader from '@/shared/components/PageHeader';
import StatusTag from '@/shared/components/StatusTag';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { useToast } from '@/shared/components/toast';
import {
  DIFFICULTY_LABELS,
  INTERVIEW_MODE_LABELS,
  INTERVIEW_STATUS,
  QUESTION_TYPE_LABELS,
} from '@/shared/constants/labels';
import { ROUTES } from '@/shared/constants/routes';
import NotFoundPage from '@/shared/pages/NotFoundPage';
import BlacklistRequestDialog from '@/shared/recruitment/BlacklistRequestDialog';
import {
  useCancelInterview,
  useInterview,
  useRecordInterviewOutcome,
} from '@/shared/recruitment/queries';
import RestrictedCandidateSections from '@/shared/recruitment/RestrictedCandidateSections';
import ScheduleInterviewDialog from '@/shared/recruitment/ScheduleInterviewDialog';
import { getErrorMessage } from '@/shared/utils/errors';
import { formatDateTime, formatTimeRange } from '@/shared/utils/format';
import ScorecardsPanel from '../components/ScorecardsPanel';

/**
 * Mülakat sayfası: mülakat bilgileri ve adayın mülakatçıya açık görünümü (CV, ön görüşme özeti).
 * İK buradan mülakatı değiştirir, iptal eder ve sonucunu girer.
 */
export default function InterviewDetailPage() {
  const { id } = useParams();
  const toast = useToast();
  const { hasAnyRole } = useAuth();
  const isHr = hasAnyRole(HR_ROLES);
  const { data, isLoading, isError, error } = useInterview(id);
  const recordOutcome = useRecordInterviewOutcome();
  // Açık pencere: null | 'reschedule' | 'cancel' | 'blacklist'
  const [dialog, setDialog] = useState(null);

  if (isLoading) return <TableSkeleton rows={6} />;
  if (isError) {
    return error.response?.status === 404 ? (
      <NotFoundPage />
    ) : (
      <p role="alert">{getErrorMessage(error)}</p>
    );
  }

  const { interview, candidate } = data;
  const isScheduled = interview.status === 'Scheduled';

  const saveOutcome = (outcome) =>
    recordOutcome.mutate(
      { id: interview.id, outcome },
      {
        onSuccess: () =>
          outcome === 'NoShow'
            ? toast.action('Adayın mülakata gelmediği kaydedildi.', {
                label: 'Kara liste talebi aç',
                onAction: () => setDialog('blacklist'),
              })
            : toast.success('Mülakat yapıldı olarak kaydedildi.'),
        onError: (mutationError) => toast.error(getErrorMessage(mutationError)),
      },
    );

  const actions = isHr && (
    <>
      {interview.awaitingOutcome && (
        <>
          <Button
            label="Aday gelmedi"
            icon="pi pi-user-minus"
            severity="danger"
            outlined
            disabled={recordOutcome.isPending}
            onClick={() => saveOutcome('NoShow')}
          />
          <Button
            label="Yapıldı"
            icon="pi pi-check"
            loading={recordOutcome.isPending}
            onClick={() => saveOutcome('Completed')}
          />
        </>
      )}
      {isScheduled && !interview.awaitingOutcome && (
        <>
          <Button label="İptal et" severity="danger" outlined onClick={() => setDialog('cancel')} />
          <Button label="Değiştir" icon="pi pi-pencil" onClick={() => setDialog('reschedule')} />
        </>
      )}
      <Link
        to={ROUTES.candidate(interview.candidateId)}
        className="p-button p-button-outlined link-button"
      >
        Aday kartı
      </Link>
    </>
  );

  const location =
    interview.mode === 'Online' ? (
      interview.meetingUrl ? (
        <a href={interview.meetingUrl} target="_blank" rel="noreferrer">
          Toplantıya katıl
        </a>
      ) : (
        'Online (bağlantı eklenmedi)'
      )
    ) : (
      (interview.roomName ?? 'Yüz yüze (oda seçilmedi)')
    );

  return (
    <>
      <Link to={ROUTES.interviews} className="back-link">
        <i className="pi pi-arrow-left" aria-hidden="true" /> Mülakatlar
      </Link>
      <PageHeader
        title={
          <span className="dialog-title">
            {interview.candidateName}
            <StatusTag value={interview.status} map={INTERVIEW_STATUS} />
          </span>
        }
        subtitle={`${interview.typeName} mülakatı · ${interview.positionTitle}`}
        actions={actions}
      />

      {interview.awaitingOutcome && isHr && (
        <div className="notice page-notice" role="status">
          <i className="pi pi-info-circle" aria-hidden="true" />
          <div>
            Mülakatın saati geçti. Yapıldıysa “Yapıldı”yı, aday gelmediyse “Aday gelmedi”yi seçin.
          </div>
        </div>
      )}

      <div className="record-layout">
        <div className="record-layout__column">
          {data.template && <InterviewQuestions template={data.template} />}
          <RestrictedCandidateSections
            candidate={candidate}
            getDownloadUrl={(documentId) => getInterviewDocumentUrl(interview.id, documentId)}
          />
        </div>

        <aside className="record-layout__column record-layout__column--side">
          <section className="card card--padded" aria-labelledby="interview-info">
            <h2 id="interview-info" className="section-title">
              Mülakat
            </h2>
            <div className="detail-sections">
              <DetailList
                items={[
                  { label: 'Zaman', value: formatTimeRange(interview.startsAt, interview.endsAt) },
                  { label: 'Süre', value: `${interview.durationMinutes} dakika` },
                  { label: 'Şekli', value: INTERVIEW_MODE_LABELS[interview.mode] },
                  { label: 'Yer', value: location },
                  {
                    label: 'Mülakatçılar',
                    value: interview.interviewers.map((item) => item.displayName).join(', '),
                  },
                  { label: 'Planlayan', value: data.scheduledByName },
                ]}
              />
              {data.note && (
                <div>
                  <h3 className="section-title">İK’nın notu</h3>
                  <p className="multiline">{data.note}</p>
                </div>
              )}
              {data.cancellationReason && (
                <div>
                  <h3 className="section-title">İptal nedeni</h3>
                  <p className="multiline">{data.cancellationReason}</p>
                </div>
              )}
            </div>
          </section>
          <ScorecardsPanel interview={interview} />
        </aside>
      </div>

      {dialog === 'reschedule' && (
        <ScheduleInterviewDialog
          interview={{ ...interview, note: data.note }}
          onClose={() => setDialog(null)}
        />
      )}
      {dialog === 'cancel' && (
        <CancelInterviewDialog interview={interview} onClose={() => setDialog(null)} />
      )}
      {dialog === 'blacklist' && (
        <BlacklistRequestDialog
          candidate={{ id: interview.candidateId, name: interview.candidateName }}
          onClose={() => setDialog(null)}
        />
      )}
    </>
  );
}

/** İptal nedeni (zorunlu) ile mülakatı iptal eder. */
function CancelInterviewDialog({ interview, onClose }) {
  const toast = useToast();
  const cancel = useCancelInterview();
  const [reason, setReason] = useState('');
  const [reasonError, setReasonError] = useState(null);

  const submit = () => {
    if (!reason.trim()) {
      setReasonError('İptal nedenini yazmalısınız.');
      return;
    }
    cancel.mutate(
      { id: interview.id, reason: reason.trim() },
      {
        onSuccess: () => {
          toast.success('Mülakat iptal edildi.');
          onClose();
        },
        onError: (error) => toast.error(getErrorMessage(error)),
      },
    );
  };

  const footer = (
    <div className="dialog-actions">
      <Button type="button" label="Vazgeç" text severity="secondary" onClick={onClose} />
      <Button
        type="button"
        label="Mülakatı iptal et"
        severity="danger"
        loading={cancel.isPending}
        onClick={submit}
      />
    </div>
  );

  return (
    <Dialog
      visible
      header="Mülakatı iptal et"
      footer={footer}
      onHide={onClose}
      className="form-dialog"
      modal
    >
      <div className="form-grid">
        <p className="form-grid__wide">
          <strong>{interview.candidateName}</strong> ile {formatDateTime(interview.startsAt)}{' '}
          tarihli mülakat iptal edilecek.
        </p>
        <div className="form-grid__wide">
          <FormField label="İptal nedeni" htmlFor="cancel-reason" error={reasonError}>
            <InputTextarea
              id="cancel-reason"
              rows={3}
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
        </div>
      </div>
    </Dialog>
  );
}

/** Şablondaki sorular; beklenen cevap mülakatçı isterse açılır. */
function InterviewQuestions({ template }) {
  return (
    <section className="card card--padded" aria-labelledby="interview-questions">
      <h2 id="interview-questions" className="section-title">
        Sorular · {template.name}
      </h2>
      <ol className="interview-questions">
        {template.questions.map((question) => (
          <li key={question.questionId}>
            <p className="interview-questions__text">{question.text}</p>
            <p className="cell-sub">
              {[
                question.competencyName,
                QUESTION_TYPE_LABELS[question.type],
                DIFFICULTY_LABELS[question.difficulty],
                `ağırlık ${question.weight.toLocaleString('tr-TR')}`,
              ].join(' · ')}
            </p>
            {question.guidance && (
              <details>
                <summary>Beklenen cevap</summary>
                <p className="multiline">{question.guidance}</p>
              </details>
            )}
          </li>
        ))}
      </ol>
    </section>
  );
}
