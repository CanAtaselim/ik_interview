import { Button } from 'primereact/button';
import { Checkbox } from 'primereact/checkbox';
import { Column } from 'primereact/column';
import { DataTable } from 'primereact/datatable';
import { Dropdown } from 'primereact/dropdown';
import { IconField } from 'primereact/iconfield';
import { InputIcon } from 'primereact/inputicon';
import { InputText } from 'primereact/inputtext';
import { Message } from 'primereact/message';
import { useState } from 'react';
import { Role } from '@/shared/auth/roles';
import { useAuth } from '@/shared/auth/useAuth';
import EmptyState from '@/shared/components/EmptyState';
import PageHeader from '@/shared/components/PageHeader';
import StatusTag from '@/shared/components/StatusTag';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { HIRING_REQUEST_STATUS, ROLE_LABELS, toOptions, URGENCY } from '@/shared/constants/labels';
import { useUrlFilters } from '@/shared/hooks/useUrlFilters';
import { getErrorMessage } from '@/shared/utils/errors';
import { formatDate } from '@/shared/utils/format';
import HiringRequestDetailDialog from '../components/HiringRequestDetailDialog';
import HiringRequestFormDialog from '../components/HiringRequestFormDialog';
import { useHiringRequests } from '../queries';
import '../hiring-requests.css';

const PAGE_SIZE = 20;
const STATUS_OPTIONS = toOptions(HIRING_REQUEST_STATUS);
const REQUESTER_ROLES = [Role.TeamLead, Role.DepartmentManager, Role.HrManager];
const APPROVER_ROLES = [Role.DepartmentManager, Role.HrManager];
const NEW_REQUEST = 'new';

/**
 * Kadro talepleri listesi. Filtreler, sayfa ve açık olan talep adres çubuğunda tutulur;
 * böylece bağlantı paylaşılabilir ve geri tuşu beklendiği gibi çalışır.
 */
export default function HiringRequestListPage() {
  const { hasAnyRole } = useAuth();
  const { params, search, searchText, setSearchText, page, updateParams } = useUrlFilters();
  // Açık form: null (kapalı), NEW_REQUEST veya düzenlenen talebin kimliği.
  const [editing, setEditing] = useState(null);

  const status = params.get('status');
  const awaitingMyDecision = params.get('awaiting') === '1';
  const selectedId = params.get('selected');

  const { data, isLoading, isFetching, isError, error } = useHiringRequests({
    search: search || undefined,
    status: status || undefined,
    awaitingMyDecision: awaitingMyDecision || undefined,
    page,
    pageSize: PAGE_SIZE,
  });

  const hasFilters = Boolean(search || status || awaitingMyDecision);
  const canCreate = hasAnyRole(REQUESTER_ROLES);
  const openRequest = (id) => updateParams({ selected: id });

  const titleCell = (request) => (
    <div>
      <button type="button" className="table-link" onClick={() => openRequest(request.id)}>
        {request.title}
      </button>
      <div className="cell-sub">
        {request.departmentName} · {request.levelName}
      </div>
    </div>
  );

  const statusCell = (request) => (
    <div>
      <StatusTag value={request.status} map={HIRING_REQUEST_STATUS} />
      {request.awaitingRole && (
        <div className="cell-sub">
          Sırada: {ROLE_LABELS[request.awaitingRole] ?? request.awaitingRole}
        </div>
      )}
    </div>
  );

  const emptyState = hasFilters ? (
    <EmptyState
      icon="pi pi-search"
      title="Bu filtrelere uyan talep yok"
      description="Arama metnini veya filtreleri değiştirip tekrar deneyin."
    />
  ) : (
    <EmptyState
      icon="pi pi-file-edit"
      title="Henüz kadro talebi yok"
      description={
        canCreate ? 'İlk talebi oluşturarak başlayın.' : 'Talep açıldığında burada göreceksiniz.'
      }
      action={
        canCreate && (
          <Button label="Yeni talep" icon="pi pi-plus" onClick={() => setEditing(NEW_REQUEST)} />
        )
      }
    />
  );

  return (
    <>
      <PageHeader
        title="Kadro talepleri"
        subtitle="Yeni kadro ihtiyaçlarını buradan açar, onay sürecini buradan izlersiniz."
        actions={
          canCreate && (
            <Button label="Yeni talep" icon="pi pi-plus" onClick={() => setEditing(NEW_REQUEST)} />
          )
        }
      />

      <section className="card">
        <div className="toolbar">
          <IconField iconPosition="left" className="toolbar__search">
            <InputIcon className="pi pi-search" />
            <InputText
              value={searchText}
              onChange={(event) => setSearchText(event.target.value)}
              placeholder="Pozisyon adıyla ara"
              aria-label="Pozisyon adıyla ara"
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
          {hasAnyRole(APPROVER_ROLES) && (
            <label className="toolbar__check" htmlFor="awaiting-me">
              <Checkbox
                inputId="awaiting-me"
                checked={awaitingMyDecision}
                onChange={(event) =>
                  updateParams({ awaiting: event.checked ? '1' : null, page: null })
                }
              />
              Yalnızca onayımı bekleyenler
            </label>
          )}
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
            onRowClick={(event) => openRequest(event.data.id)}
            rowHover
            emptyMessage={emptyState}
            className={isFetching ? 'is-refreshing' : undefined}
            aria-busy={isFetching}
          >
            <Column header="Pozisyon" body={titleCell} />
            <Column
              header="Kişi"
              field="headcount"
              className="col-narrow col-desktop"
              headerClassName="col-desktop"
            />
            <Column
              header="Aciliyet"
              body={(request) => <StatusTag value={request.urgency} map={URGENCY} />}
              className="col-desktop"
              headerClassName="col-desktop"
            />
            <Column header="Durum" body={statusCell} />
            <Column
              header="Talep eden"
              field="requestedByName"
              className="col-desktop"
              headerClassName="col-desktop"
            />
            <Column
              header="Tarih"
              body={(request) => formatDate(request.createdAt)}
              className="col-desktop"
              headerClassName="col-desktop"
            />
          </DataTable>
        )}
      </section>

      {selectedId && !editing && (
        <HiringRequestDetailDialog
          requestId={selectedId}
          onClose={() => updateParams({ selected: null })}
          onEdit={setEditing}
        />
      )}

      {editing && (
        <HiringRequestFormDialog
          requestId={editing === NEW_REQUEST ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={(id) => {
            setEditing(null);
            openRequest(id);
          }}
        />
      )}
    </>
  );
}
