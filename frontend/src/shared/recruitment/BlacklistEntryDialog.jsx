import { Button } from 'primereact/button';
import { Dialog } from 'primereact/dialog';
import { InputTextarea } from 'primereact/inputtextarea';
import { Message } from 'primereact/message';
import { useState } from 'react';
import DetailList from '@/shared/components/DetailList';
import StatusTag from '@/shared/components/StatusTag';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { useToast } from '@/shared/components/toast';
import { BLACKLIST_STATUS } from '@/shared/constants/labels';
import {
  useApproveBlacklistEntry,
  useBlacklistEntry,
  useRemoveBlacklistEntry,
} from '@/shared/recruitment/queries';
import { getErrorMessage } from '@/shared/utils/errors';
import { formatDate, formatDateTime } from '@/shared/utils/format';

/** Kaydın ayrıntısı; İK Yöneticisi buradan onaylar veya kara listeden çıkarır. */
export default function BlacklistEntryDialog({ entryId, onClose }) {
  const toast = useToast();
  const { data: entry, isLoading, isError, error } = useBlacklistEntry(entryId);
  const approve = useApproveBlacklistEntry();
  const remove = useRemoveBlacklistEntry();
  const [removalReason, setRemovalReason] = useState('');
  const [reasonError, setReasonError] = useState(null);

  const onError = (mutationError) => toast.error(getErrorMessage(mutationError));

  const submitRemoval = () => {
    if (!removalReason.trim()) {
      setReasonError('Çıkarma gerekçesini yazmalısınız.');
      return;
    }
    remove.mutate(
      { id: entryId, reason: removalReason.trim() },
      {
        onSuccess: () => {
          setRemovalReason('');
          toast.success(
            entry.status === 'PendingApproval'
              ? 'Talep reddedildi.'
              : 'Aday kara listeden çıkarıldı.',
          );
        },
        onError,
      },
    );
  };

  const footer = entry && (entry.canApprove || entry.canRemove) && (
    <div className="dialog-actions">
      {entry.canRemove && (
        <Button
          type="button"
          label={entry.status === 'PendingApproval' ? 'Talebi reddet' : 'Kara listeden çıkar'}
          severity="danger"
          outlined
          loading={remove.isPending}
          onClick={submitRemoval}
        />
      )}
      {entry.canApprove && (
        <Button
          type="button"
          label="Onayla"
          icon="pi pi-check"
          loading={approve.isPending}
          onClick={() =>
            approve.mutate(entryId, {
              onSuccess: () => toast.success('Talep onaylandı; kayıt yürürlükte.'),
              onError,
            })
          }
        />
      )}
    </div>
  );

  const header = entry ? (
    <span className="dialog-title">
      {entry.candidateName}
      <StatusTag value={entry.status} map={BLACKLIST_STATUS} />
    </span>
  ) : (
    'Kara liste kaydı'
  );

  return (
    <Dialog visible header={header} footer={footer} onHide={onClose} className="form-dialog" modal>
      {isLoading && <TableSkeleton rows={4} />}
      {isError && <Message severity="error" text={getErrorMessage(error)} />}
      {entry && (
        <div className="detail-sections">
          <DetailList
            items={[
              { label: 'Neden', value: entry.reasonName },
              { label: 'Bitiş', value: entry.expiresAt ? formatDate(entry.expiresAt) : 'Kalıcı' },
              { label: 'Talep eden', value: entry.requestedByName },
              { label: 'Talep tarihi', value: formatDateTime(entry.createdAt) },
              ...(entry.approvedAt
                ? [
                    {
                      label: 'Onaylayan',
                      value: `${entry.approvedByName} · ${formatDateTime(entry.approvedAt)}`,
                    },
                  ]
                : []),
              ...(entry.removedAt
                ? [
                    {
                      label: 'Çıkaran',
                      value: `${entry.removedByName} · ${formatDateTime(entry.removedAt)}`,
                    },
                  ]
                : []),
            ]}
          />

          <section>
            <h2 className="section-title">Gerekçe</h2>
            <p className="multiline">{entry.description}</p>
          </section>

          {entry.removalReason && (
            <section>
              <h2 className="section-title">Çıkarma gerekçesi</h2>
              <p className="multiline">{entry.removalReason}</p>
            </section>
          )}

          {entry.canRemove && (
            <section className="decision-box">
              <label className="section-title" htmlFor="removal-reason">
                {entry.status === 'PendingApproval'
                  ? 'Talebi reddetmek için gerekçe'
                  : 'Kara listeden çıkarmak için gerekçe'}
              </label>
              <InputTextarea
                id="removal-reason"
                rows={3}
                autoResize
                maxLength={2000}
                value={removalReason}
                invalid={Boolean(reasonError)}
                onChange={(event) => {
                  setRemovalReason(event.target.value);
                  setReasonError(null);
                }}
              />
              {reasonError && (
                <small className="form-field__error" role="alert">
                  {reasonError}
                </small>
              )}
            </section>
          )}
        </div>
      )}
    </Dialog>
  );
}
