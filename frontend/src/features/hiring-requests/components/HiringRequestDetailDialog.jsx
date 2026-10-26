import { Button } from 'primereact/button';
import { confirmDialog } from 'primereact/confirmdialog';
import { Dialog } from 'primereact/dialog';
import { InputTextarea } from 'primereact/inputtextarea';
import { Message } from 'primereact/message';
import { Tag } from 'primereact/tag';
import { useState } from 'react';
import { Link } from 'react-router';
import DetailList from '@/shared/components/DetailList';
import StatusTag from '@/shared/components/StatusTag';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { useToast } from '@/shared/components/toast';
import { HIRING_REASON_LABELS, HIRING_REQUEST_STATUS, URGENCY } from '@/shared/constants/labels';
import { ROUTES } from '@/shared/constants/routes';
import { getErrorMessage } from '@/shared/utils/errors';
import { formatDate, formatDateTime } from '@/shared/utils/format';
import {
  useApproveHiringRequest,
  useCancelHiringRequest,
  useHiringRequest,
  useRejectHiringRequest,
  useSubmitHiringRequest,
} from '../queries';
import ApprovalSteps from './ApprovalSteps';

/**
 * Kadro talebinin detayı ve kullanıcının o talepte yapabildiği işlemler (onaya gönder,
 * onayla/reddet, düzenle, iptal et). Hangi butonların görüneceğine sunucu karar verir.
 * @param {{ requestId: string, onClose: () => void, onEdit: (id: string) => void }} props
 */
export default function HiringRequestDetailDialog({ requestId, onClose, onEdit }) {
  const toast = useToast();
  const { data: request, isLoading, isError, error } = useHiringRequest(requestId);
  const submitRequest = useSubmitHiringRequest();
  const approveRequest = useApproveHiringRequest();
  const rejectRequest = useRejectHiringRequest();
  const cancelRequest = useCancelHiringRequest();
  const [decisionNote, setDecisionNote] = useState('');
  const [noteError, setNoteError] = useState(null);

  const run = (mutation, variables, successMessage) =>
    mutation.mutate(variables, {
      onSuccess: () => {
        setDecisionNote('');
        toast.success(successMessage);
      },
      onError: (mutationError) => toast.error(getErrorMessage(mutationError)),
    });

  const reject = () => {
    if (!decisionNote.trim()) {
      setNoteError('Reddetmek için gerekçe yazmalısınız.');
      return;
    }
    run(rejectRequest, { id: requestId, reason: decisionNote.trim() }, 'Talep reddedildi.');
  };

  const confirmCancel = () =>
    confirmDialog({
      header: 'Talep iptal edilsin mi?',
      message:
        'İptal edilen talep yeniden açılamaz; gerekirse yeni bir talep oluşturmanız gerekir. Bir onaycı karar verdikten sonra talep iptal edilemez.',
      acceptLabel: 'Talebi iptal et',
      rejectLabel: 'Vazgeç',
      acceptClassName: 'p-button-danger',
      accept: () => run(cancelRequest, requestId, 'Talep iptal edildi.'),
    });

  const permissions = request?.permissions;
  const isDeciding = approveRequest.isPending || rejectRequest.isPending;

  const footer = request && (
    <div className="dialog-actions">
      {permissions.canCancel && (
        <Button
          type="button"
          label="Talebi iptal et"
          text
          severity="danger"
          className="dialog-actions__start"
          loading={cancelRequest.isPending}
          onClick={confirmCancel}
        />
      )}
      {permissions.canEdit && (
        <Button
          type="button"
          label="Düzenle"
          icon="pi pi-pencil"
          outlined
          onClick={() => onEdit(requestId)}
        />
      )}
      {permissions.canSubmit && (
        <Button
          type="button"
          label="Onaya gönder"
          icon="pi pi-send"
          loading={submitRequest.isPending}
          onClick={() => run(submitRequest, requestId, 'Talep onaya gönderildi.')}
        />
      )}
      {permissions.canDecide && (
        <>
          <Button
            type="button"
            label="Reddet"
            severity="danger"
            outlined
            disabled={isDeciding}
            onClick={reject}
          />
          <Button
            type="button"
            label="Onayla"
            icon="pi pi-check"
            loading={approveRequest.isPending}
            disabled={isDeciding}
            onClick={() =>
              run(
                approveRequest,
                { id: requestId, note: decisionNote.trim() },
                'Talebi onayladınız.',
              )
            }
          />
        </>
      )}
    </div>
  );

  const header = request ? (
    <span className="dialog-title">
      {request.title}
      <StatusTag value={request.status} map={HIRING_REQUEST_STATUS} />
    </span>
  ) : (
    'Kadro talebi'
  );

  return (
    <Dialog visible header={header} footer={footer} onHide={onClose} className="form-dialog" modal>
      {isLoading && <TableSkeleton rows={6} />}
      {isError && <Message severity="error" text={getErrorMessage(error)} />}
      {request && (
        <div className="detail-sections">
          <DetailList
            items={[
              { label: 'Birim', value: request.departmentName },
              { label: 'Seviye', value: request.levelName },
              { label: 'Kişi sayısı', value: request.headcount },
              { label: 'Aciliyet', value: <StatusTag value={request.urgency} map={URGENCY} /> },
              { label: 'Talep nedeni', value: HIRING_REASON_LABELS[request.reason] },
              ...(request.replacedEmployeeName
                ? [{ label: 'Yerine alım yapılacak çalışan', value: request.replacedEmployeeName }]
                : []),
              { label: 'Beklenen başlangıç', value: formatDate(request.expectedStartDate) },
              { label: 'Talep eden', value: request.requestedByName },
              { label: 'Oluşturulma', value: formatDateTime(request.createdAt) },
            ]}
          />

          {request.competencies.length > 0 && (
            <section>
              <h2 className="section-title">Aranan yetkinlikler</h2>
              <div className="tag-list">
                {request.competencies.map((competency) => (
                  <Tag key={competency} value={competency} severity="info" />
                ))}
              </div>
            </section>
          )}

          {request.description && (
            <section>
              <h2 className="section-title">Açıklama</h2>
              <p className="multiline">{request.description}</p>
            </section>
          )}

          {request.steps.length > 0 && (
            <section>
              <h2 className="section-title">Onay süreci</h2>
              <ApprovalSteps steps={request.steps} />
            </section>
          )}

          {request.status === 'Approved' &&
            (request.positionId ? (
              <Message
                severity="success"
                content={
                  <span>
                    Talep onaylandı ve pozisyon açıldı.{' '}
                    <Link to={ROUTES.position(request.positionId)}>Pozisyona git</Link>
                  </span>
                }
              />
            ) : (
              <Message
                severity="info"
                text="Talep onaylandı. Pozisyon birkaç saniye içinde açılacak."
              />
            ))}

          {permissions.canDecide && (
            <section className="decision-box">
              <label className="section-title" htmlFor="decision-note">
                Kararınız
              </label>
              <InputTextarea
                id="decision-note"
                rows={3}
                autoResize
                value={decisionNote}
                invalid={Boolean(noteError)}
                placeholder="Not (onay için isteğe bağlı, ret için zorunlu)"
                aria-describedby={noteError ? 'decision-note-error' : undefined}
                onChange={(event) => {
                  setDecisionNote(event.target.value);
                  setNoteError(null);
                }}
              />
              {noteError && (
                <small id="decision-note-error" className="form-field__error" role="alert">
                  {noteError}
                </small>
              )}
            </section>
          )}
        </div>
      )}
    </Dialog>
  );
}
