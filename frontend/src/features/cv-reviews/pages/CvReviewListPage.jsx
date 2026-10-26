import { Checkbox } from 'primereact/checkbox';
import { Column } from 'primereact/column';
import { DataTable } from 'primereact/datatable';
import { IconField } from 'primereact/iconfield';
import { InputIcon } from 'primereact/inputicon';
import { InputText } from 'primereact/inputtext';
import { Message } from 'primereact/message';
import { SelectButton } from 'primereact/selectbutton';
import { Link, useNavigate } from 'react-router';
import { Role } from '@/shared/auth/roles';
import { useAuth } from '@/shared/auth/useAuth';
import EmptyState from '@/shared/components/EmptyState';
import PageHeader from '@/shared/components/PageHeader';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { ROUTES } from '@/shared/constants/routes';
import { useUrlFilters } from '@/shared/hooks/useUrlFilters';
import { useCvReviews } from '@/shared/recruitment/queries';
import { getErrorMessage } from '@/shared/utils/errors';
import { formatDate } from '@/shared/utils/format';
import ReviewStatus from '../components/ReviewStatus';

const PAGE_SIZE = 20;
const DEFAULT_STATUS = 'Waiting';
const STATUS_OPTIONS = [
  { label: 'Karar bekleyen', value: 'Waiting' },
  { label: 'Sonuçlanan', value: 'Decided' },
  { label: 'Geciken', value: 'Overdue' },
  { label: 'Tümü', value: 'All' },
];

/**
 * CV değerlendirmeleri. Takım lideri kendisine (istersen birimine) önerilen adayları görür ve
 * karar verir; İK tüm önerilerin durumunu izler.
 */
export default function CvReviewListPage() {
  const navigate = useNavigate();
  const { hasAnyRole } = useAuth();
  const { params, search, searchText, setSearchText, page, updateParams } = useUrlFilters();
  const isLead = hasAnyRole([Role.TeamLead]);
  const status = params.get('status') ?? DEFAULT_STATUS;
  // Takım lideri önce kendisine önerilenleri görür; "scope=department" biriminin tamamını açar.
  const onlyMine = isLead && params.get('scope') !== 'department';

  const { data, isLoading, isFetching, isError, error } = useCvReviews({
    status: status === 'All' ? undefined : status,
    mine: onlyMine || undefined,
    search: search || undefined,
    page,
    pageSize: PAGE_SIZE,
  });

  const open = (review) => navigate(ROUTES.cvReview(review.id));

  const candidateCell = (review) => (
    <>
      <Link to={ROUTES.cvReview(review.id)} className="table-link">
        {review.candidateName}
      </Link>
      <div className="cell-sub">
        {review.positionTitle ?? 'Pozisyon belirtilmeden'}
        {review.departmentName && ` · ${review.departmentName}`}
      </div>
    </>
  );

  const dateCell = (review) =>
    review.decision === 'Positive' || review.decision === 'Negative' ? (
      <>
        {formatDate(review.decidedAt)}
        <div className="cell-sub">Karar tarihi</div>
      </>
    ) : (
      <>
        {formatDate(review.dueAt)}
        <div className="cell-sub">Son tarih</div>
      </>
    );

  const emptyState =
    search || status !== DEFAULT_STATUS ? (
      <EmptyState
        icon="pi pi-search"
        title="Bu filtrelere uyan değerlendirme yok"
        description="Arama metnini veya durumu değiştirip tekrar deneyin."
      />
    ) : (
      <EmptyState
        icon="pi pi-check-circle"
        title={isLead ? 'Karar bekleyen CV yok' : 'Karar bekleyen değerlendirme yok'}
        description={
          isLead
            ? 'İK size bir aday önerdiğinde burada göreceksiniz.'
            : 'Bir adayı takım liderine önermek için aday kartındaki “Öner”i kullanın.'
        }
      />
    );

  return (
    <>
      <PageHeader
        title="CV değerlendirmeleri"
        subtitle={
          isLead
            ? 'Size önerilen adayların CV’lerini inceleyip kararınızı verin.'
            : 'Takım liderlerine önerilen adaylar ve kararları.'
        }
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
          <SelectButton
            value={status}
            options={STATUS_OPTIONS}
            allowEmpty={false}
            aria-label="Duruma göre filtrele"
            onChange={(event) =>
              updateParams({
                status: event.value === DEFAULT_STATUS ? null : event.value,
                page: null,
              })
            }
          />
          {isLead && (
            <label className="toolbar__check" htmlFor="reviews-department">
              <Checkbox
                inputId="reviews-department"
                checked={!onlyMine}
                onChange={(event) =>
                  updateParams({ scope: event.checked ? 'department' : null, page: null })
                }
              />
              Birimime önerilenleri de göster
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
            onRowClick={(event) => open(event.data)}
            rowHover
            emptyMessage={emptyState}
            className={isFetching ? 'is-refreshing' : undefined}
            aria-busy={isFetching}
          >
            <Column header="Aday" body={candidateCell} />
            <Column
              header="Takım lideri"
              field="reviewerName"
              className="col-desktop"
              headerClassName="col-desktop"
            />
            <Column
              header="Öneren"
              field="suggestedByName"
              className="col-desktop"
              headerClassName="col-desktop"
            />
            <Column header="Durum" body={(review) => <ReviewStatus review={review} />} />
            <Column
              header="Tarih"
              body={dateCell}
              className="col-desktop"
              headerClassName="col-desktop"
            />
          </DataTable>
        )}
      </section>
    </>
  );
}
