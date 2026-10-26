import { Button } from 'primereact/button';
import { Column } from 'primereact/column';
import { DataTable } from 'primereact/datatable';
import { Dropdown } from 'primereact/dropdown';
import { IconField } from 'primereact/iconfield';
import { InputIcon } from 'primereact/inputicon';
import { InputText } from 'primereact/inputtext';
import { Message } from 'primereact/message';
import { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { HR_ROLES } from '@/shared/auth/roles';
import { useAuth } from '@/shared/auth/useAuth';
import EmptyState from '@/shared/components/EmptyState';
import PageHeader from '@/shared/components/PageHeader';
import StatusTag from '@/shared/components/StatusTag';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { POSITION_STATUS, toOptions } from '@/shared/constants/labels';
import { ROUTES } from '@/shared/constants/routes';
import { useUrlFilters } from '@/shared/hooks/useUrlFilters';
import { getErrorMessage } from '@/shared/utils/errors';
import PositionFormDialog from '../components/PositionFormDialog';
import { usePositions } from '../queries';

const PAGE_SIZE = 20;
const STATUS_OPTIONS = toOptions(POSITION_STATUS);

/** Pozisyon listesi. Filtreler ve sayfa adres çubuğunda tutulur. */
export default function PositionListPage() {
  const { hasAnyRole } = useAuth();
  const navigate = useNavigate();
  const { params, search, searchText, setSearchText, page, updateParams } = useUrlFilters();
  const [creating, setCreating] = useState(false);

  const status = params.get('status');

  const { data, isLoading, isFetching, isError, error } = usePositions({
    search: search || undefined,
    status: status || undefined,
    page,
    pageSize: PAGE_SIZE,
  });

  const canManage = hasAnyRole(HR_ROLES);
  const hasFilters = Boolean(search || status);

  const titleCell = (position) => (
    <div>
      <Link to={ROUTES.position(position.id)} className="table-link">
        {position.title}
      </Link>
      <div className="cell-sub">
        {position.departmentName} · {position.levelName}
      </div>
    </div>
  );

  const emptyState = hasFilters ? (
    <EmptyState
      icon="pi pi-search"
      title="Bu filtrelere uyan pozisyon yok"
      description="Arama metnini veya durumu değiştirip tekrar deneyin."
    />
  ) : (
    <EmptyState
      icon="pi pi-briefcase"
      title="Henüz pozisyon yok"
      description="Onaylanan kadro talepleri burada pozisyon olarak açılır."
      action={
        canManage && (
          <Button label="Yeni pozisyon" icon="pi pi-plus" onClick={() => setCreating(true)} />
        )
      }
    />
  );

  return (
    <>
      <PageHeader
        title="Pozisyonlar"
        subtitle="Aday aranan açık kadrolar."
        actions={
          canManage && (
            <Button label="Yeni pozisyon" icon="pi pi-plus" onClick={() => setCreating(true)} />
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
            onRowClick={(event) => navigate(ROUTES.position(event.data.id))}
            rowHover
            emptyMessage={emptyState}
            className={isFetching ? 'is-refreshing' : undefined}
            aria-busy={isFetching}
          >
            <Column header="Pozisyon" body={titleCell} />
            <Column
              header="Doluluk"
              body={(position) => `${position.filledCount} / ${position.headcount}`}
              className="col-narrow col-desktop"
              headerClassName="col-desktop"
            />
            <Column
              header="Sorumlu İK"
              body={(position) => position.responsibleHrName ?? 'Atanmadı'}
              className="col-desktop"
              headerClassName="col-desktop"
            />
            <Column
              header="Durum"
              body={(position) => <StatusTag value={position.status} map={POSITION_STATUS} />}
            />
          </DataTable>
        )}
      </section>

      {creating && (
        <PositionFormDialog
          onClose={() => setCreating(false)}
          onSaved={(id) => navigate(ROUTES.position(id))}
        />
      )}
    </>
  );
}
