import { Button } from 'primereact/button';
import { Column } from 'primereact/column';
import { DataTable } from 'primereact/datatable';
import { Dialog } from 'primereact/dialog';
import { Dropdown } from 'primereact/dropdown';
import { IconField } from 'primereact/iconfield';
import { InputIcon } from 'primereact/inputicon';
import { InputText } from 'primereact/inputtext';
import { InputTextarea } from 'primereact/inputtextarea';
import { Message } from 'primereact/message';
import { useState } from 'react';
import { Link } from 'react-router';
import { HR_ROLES } from '@/shared/auth/roles';
import { useAuth } from '@/shared/auth/useAuth';
import DetailList from '@/shared/components/DetailList';
import EmptyState from '@/shared/components/EmptyState';
import PageHeader from '@/shared/components/PageHeader';
import StatusTag from '@/shared/components/StatusTag';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { useToast } from '@/shared/components/toast';
import { BLACKLIST_STATUS, toOptions } from '@/shared/constants/labels';
import { ROUTES } from '@/shared/constants/routes';
import { useUrlFilters } from '@/shared/hooks/useUrlFilters';
import {
  useApproveBlacklistEntry,
  useBlacklistEntries,
  useBlacklistEntry,
  useRemoveBlacklistEntry,
} from '@/shared/recruitment/queries';
import { getErrorMessage } from '@/shared/utils/errors';
import { formatDate, formatDateTime } from '@/shared/utils/format';

const PAGE_SIZE = 20;
const STATUS_OPTIONS = toOptions(BLACKLIST_STATUS);

/**
 * Kara liste. Kayıtlar ve nedenleri yalnızca İK ve Sistem Admin tarafından görülür; listeyi ve
 * bir kaydın ayrıntısını açmak sunucuda erişim kaydına yazılır.
 */
export default function BlacklistPage() {
  const { params, search, searchText, setSearchText, page, updateParams } = useUrlFilters();
  const status = params.get('status');
  const selectedId = params.get('selected');

  const { data, isLoading, isFetching, isError, error } = useBlacklistEntries({
    search: search || undefined,
    status: status || undefined,
    page,
    pageSize: PAGE_SIZE,
  });

  const openEntry = (id) => updateParams({ selected: id });

  const nameCell = (entry) => (
    <button type="button" className="table-link" onClick={() => openEntry(entry.id)}>
      {entry.candidateName}
    </button>
  );

  const emptyState =
    search || status ? (
      <EmptyState
        icon="pi pi-search"
        title="Bu filtrelere uyan kayıt yok"
        description="Arama metnini veya durumu değiştirip tekrar deneyin."
      />
    ) : (
      <EmptyState
        icon="pi pi-ban"
        title="Kara listede kayıt yok"
        description="Bir adayı kara listeye almak için aday kartındaki “Kara listeye al”ı kullanın."
      />
    );

  return (
    <>
      <PageHeader
        title="Kara liste"
        subtitle="Yeniden değerlendirilmemesi gereken adaylar. Bu ekrandaki her görüntüleme kayda geçer."
      />

      <section className="card">
        <div className="toolbar">
          <IconField iconPosition="left" className="toolbar__search">
            <InputIcon className="pi pi-search" />
            <InputText
              value={searchText}
              onChange={(event) => setSearchText(event.target.value)}
              placeholder="Aday adıyla ara"
              aria-label="Aday adıyla ara"
            />
          </IconField>
          <Dropdown
            value={status}
            options={STATUS_OPTIONS}
            onChange={(event) => updateParams({ status: event.value, page: null })}
            placeholder="Tüm durumlar"
            aria-label="Duruma göre filtrele"
            showClear
            filter
            filterInputAutoFocus
          />
        </div>

        {isError && (
          <Message severity="error" text={getErrorMessage(error)} className="toolbar__message" />
        )}
        {isLoading ? (
          <TableSkeleton />
        ) : (
          <DataTable
            value={data?.items ?? []}
            dataKey="id"
            lazy
            paginator={(data?.totalCount ?? 0) > PAGE_SIZE}
            rows={PAGE_SIZE}
            first={(page - 1) * PAGE_SIZE}
            totalRecords={data?.totalCount ?? 0}
            onPage={(event) => updateParams({ page: event.page === 0 ? null : event.page + 1 })}
            onRowClick={(event) => openEntry(event.data.id)}
            rowHover
            emptyMessage={emptyState}
            className={isFetching ? 'is-refreshing' : undefined}
            aria-busy={isFetching}
          >
            <Column header="Aday" body={nameCell} />
            <Column
              header="Neden"
              field="reasonName"
              className="col-desktop"
              headerClassName="col-desktop"
            />
            <Column
              header="Durum"
              body={(entry) => <StatusTag value={entry.status} map={BLACKLIST_STATUS} />}
            />
            <Column
              header="Bitiş"
              body={(entry) => (entry.expiresAt ? formatDate(entry.expiresAt) : 'Kalıcı')}
              className="col-desktop"
              headerClassName="col-desktop"
            />
            <Column
              header="Talep eden"
              field="requestedByName"
              className="col-desktop"
              headerClassName="col-desktop"
            />
            <Column
              header="Tarih"
              body={(entry) => formatDate(entry.createdAt)}
              className="col-desktop"
              headerClassName="col-desktop"
            />
          </DataTable>
        )}
      </section>

      {selectedId && (
        <BlacklistEntryDialog
          entryId={selectedId}
          onClose={() => updateParams({ selected: null })}
        />
      )}
    </>
  );
}

/** Kaydın ayrıntısı; İK Yöneticisi buradan onaylar veya kara listeden çıkarır. */
function BlacklistEntryDialog({ entryId, onClose }) {
  const toast = useToast();
  const { hasAnyRole } = useAuth();
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

          {hasAnyRole(HR_ROLES) && (
            <Link to={ROUTES.candidate(entry.candidateId)}>Aday kartını aç</Link>
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
