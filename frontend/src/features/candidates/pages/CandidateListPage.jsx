import { Button } from 'primereact/button';
import { Checkbox } from 'primereact/checkbox';
import { Column } from 'primereact/column';
import { DataTable } from 'primereact/datatable';
import { Dropdown } from 'primereact/dropdown';
import { IconField } from 'primereact/iconfield';
import { InputIcon } from 'primereact/inputicon';
import { InputText } from 'primereact/inputtext';
import { Message } from 'primereact/message';
import { Tag } from 'primereact/tag';
import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router';
import DictionarySelect from '@/shared/components/DictionarySelect';
import EmptyState from '@/shared/components/EmptyState';
import PageHeader from '@/shared/components/PageHeader';
import { useToast } from '@/shared/components/toast';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { ROUTES } from '@/shared/constants/routes';
import { useDebounce } from '@/shared/hooks/useDebounce';
import { useUrlFilters } from '@/shared/hooks/useUrlFilters';
import { getErrorMessage } from '@/shared/utils/errors';
import { formatDate, formatDateTime } from '@/shared/utils/format';
import { canPreview, openOrDownload } from '@/shared/utils/openDocument';
import CandidateFormDialog from '../components/CandidateFormDialog';
import { getDocumentDownloadUrl } from '../api';
import { useCandidates } from '../queries';
import '../candidates.css';

const PAGE_SIZE = 20;
const MAX_TAGS_SHOWN = 3;
const MAX_PROCESSES_SHOWN = 2;

const PROCESS_OPTIONS = [
  { label: 'Süreci devam edenler', value: 'Active' },
  { label: 'Süreci olmayanlar', value: 'None' },
  { label: 'İşe alınanlar', value: 'Hired' },
  { label: 'Reddedilen / çekilenler', value: 'Closed' },
];

const BLACKLIST_OPTIONS = [
  { label: 'Kara listedekiler', value: 'yes' },
  { label: 'Kara listede olmayanlar', value: 'no' },
];

/** Sonlanmış sürecin rozeti: işe alındı yeşil, red/çekilme sönük. */
const CLOSED_PROCESS = {
  Hired: { label: 'İşe alındı', tone: 'hired' },
  Rejected: { label: 'Reddedildi', tone: 'closed' },
  Withdrawn: { label: 'Çekildi', tone: 'closed' },
};

const rowClassName = (candidate) => {
  if (candidate.blacklist === 'Active') return 'candidate-row candidate-row--blacklisted';
  if (candidate.processes.some((process) => process.status === 'Active')) {
    return 'candidate-row candidate-row--in-process';
  }
  return 'candidate-row';
};

const cvIcon = (contentType) => {
  if (contentType === 'application/pdf') return 'pi pi-file-pdf';
  if (contentType?.startsWith('image/')) return 'pi pi-image';
  return 'pi pi-file-word';
};

/**
 * Aday listesi: son işlem yapılan aday en üstte. Kara listedeki adayın satırı kırmızı, süreci
 * devam edenin sarı görünür; "Süreç" sütunu adayın nerede olduğunu gösterir. Arama; ad, iletişim,
 * etiket, ön görüşme notları ve CV içeriğinde yapılır.
 */
export default function CandidateListPage() {
  const navigate = useNavigate();
  const toast = useToast();
  const { params, search, searchText, setSearchText, page, updateParams } = useUrlFilters();
  const [adding, setAdding] = useState(false);

  const sourceId = params.get('source');
  const onlyTalentPool = params.get('pool') === '1';
  const tag = params.get('tag');
  const process = params.get('process');
  const blacklist = params.get('blacklist');
  const city = params.get('city') ?? '';

  // Şehir kutusu yazmayı bırakınca adrese ve sorguya yansır.
  const [cityText, setCityText] = useState(city);
  const debouncedCity = useDebounce(cityText);
  useEffect(() => {
    if (debouncedCity.trim() !== city)
      updateParams({ city: debouncedCity.trim(), page: null }, { replace: true });
    // eslint-disable-next-line react-hooks/exhaustive-deps -- yalnızca geciktirilmiş şehir metni değişince
  }, [debouncedCity]);

  const { data, isLoading, isFetching, isError, error } = useCandidates({
    search: search || undefined,
    sourceId: sourceId || undefined,
    inTalentPool: onlyTalentPool || undefined,
    tag: tag || undefined,
    process: process || undefined,
    blacklisted: blacklist ? blacklist === 'yes' : undefined,
    city: city || undefined,
    page,
    pageSize: PAGE_SIZE,
  });

  const hasFilters = Boolean(
    search || sourceId || onlyTalentPool || tag || process || blacklist || city,
  );

  const nameCell = (candidate) => (
    <div>
      <Link to={ROUTES.candidate(candidate.id)} className="table-link">
        {candidate.firstName} {candidate.lastName}
      </Link>
      <div className="cell-sub">
        {[candidate.email, candidate.phone].filter(Boolean).join(' · ')}
      </div>
    </div>
  );

  // Etikete tıklamak listeyi o etikete göre süzer; satırın tıklanmasıyla karışmasın diye olay durdurulur.
  const tagsCell = (candidate) => (
    <div className="tag-list">
      {candidate.tags.slice(0, MAX_TAGS_SHOWN).map((item) => (
        <button
          key={item}
          type="button"
          className="tag-button"
          title={`“${item}” etiketli adayları göster`}
          onClick={(event) => {
            event.stopPropagation();
            updateParams({ tag: item, page: null });
          }}
        >
          {item}
        </button>
      ))}
      {candidate.tags.length > MAX_TAGS_SHOWN && (
        <span className="cell-sub">+{candidate.tags.length - MAX_TAGS_SHOWN}</span>
      )}
    </div>
  );

  // Tıklanınca CV açılır; satırın tıklanmasıyla (aday kartına gitme) karışmasın diye olay durdurulur.
  const openCv = async (event, candidate) => {
    event.stopPropagation();
    try {
      await openOrDownload(
        () => getDocumentDownloadUrl(candidate.id, candidate.cv.documentId),
        candidate.cv.contentType,
      );
    } catch (error) {
      toast.error(getErrorMessage(error));
    }
  };

  const cvCell = (candidate) =>
    candidate.cv ? (
      <Button
        type="button"
        icon={cvIcon(candidate.cv.contentType)}
        text
        rounded
        severity="secondary"
        className="cv-button"
        tooltip={
          canPreview(candidate.cv.contentType)
            ? `${candidate.cv.fileName} · yan sekmede aç`
            : `${candidate.cv.fileName} · indir`
        }
        tooltipOptions={{ position: 'left' }}
        aria-label={`${candidate.firstName} ${candidate.lastName} CV’sini ${
          canPreview(candidate.cv.contentType) ? 'aç' : 'indir'
        }`}
        onClick={(event) => openCv(event, candidate)}
      />
    ) : (
      <span
        className="muted"
        title={candidate.documentCount > 0 ? 'CV türünde dosya yok' : 'Dosya yok'}
      >
        —
      </span>
    );

  const processCell = (candidate) => {
    const shown = candidate.processes.slice(0, MAX_PROCESSES_SHOWN);
    const hidden = candidate.processes.length - shown.length;
    const hasActive = candidate.processes.some((process) => process.status === 'Active');

    return (
      <div className="process-cell">
        {candidate.blacklist === 'Active' && (
          <span className="status-pill status-pill--blacklist">Kara listede</span>
        )}
        {candidate.blacklist === 'PendingApproval' && (
          <span className="status-pill status-pill--pending">Kara liste onayı bekliyor</span>
        )}
        {shown.map((process) => {
          const closed = CLOSED_PROCESS[process.status];
          return (
            <div key={process.applicationId} className="process-cell__item">
              <span className={`status-pill status-pill--${closed?.tone ?? 'active'}`}>
                {closed?.label ?? process.stageName}
              </span>
              <span className="cell-sub">{process.positionTitle}</span>
            </div>
          );
        })}
        {hidden > 0 && <span className="cell-sub">+{hidden} süreç daha</span>}
        {!hasActive &&
          candidate.processes.length === 0 &&
          candidate.inTalentPool &&
          candidate.blacklist !== 'Active' && (
            <span className="status-pill status-pill--pool">Havuzda</span>
          )}
        {!hasActive && candidate.processes.length === 0 && !candidate.inTalentPool && (
          <span className="muted">Süreçte değil</span>
        )}
      </div>
    );
  };

  const emptyState = hasFilters ? (
    <EmptyState
      icon="pi pi-search"
      title="Aramanıza uyan aday bulunamadı"
      description="Daha az kelimeyle arayın veya filtreleri kaldırın."
    />
  ) : (
    <EmptyState
      icon="pi pi-users"
      title="Havuzda henüz aday yok"
      description="İlk adayı ekleyin; ad, soyad ve bir iletişim bilgisi yeterli."
      action={<Button label="Aday ekle" icon="pi pi-plus" onClick={() => setAdding(true)} />}
    />
  );

  return (
    <>
      <PageHeader
        title="Aday Listesi"
        subtitle="Son işlem yapılan aday en üstte. Yeni eklenen aday havuza girer; pozisyona bağlı olmadan da eklenebilir."
        actions={<Button label="Aday ekle" icon="pi pi-plus" onClick={() => setAdding(true)} />}
      />

      <section className="card">
        <div className="toolbar">
          <IconField iconPosition="left" className="toolbar__search toolbar__search--wide">
            <InputIcon className="pi pi-search" />
            <InputText
              value={searchText}
              onChange={(event) => setSearchText(event.target.value)}
              placeholder="Ad, e-posta, telefon, yetkinlik veya CV içeriği"
              aria-label="Aday ara"
            />
          </IconField>
          <DictionarySelect
            type="CandidateSource"
            value={sourceId}
            onChange={(id) => updateParams({ source: id, page: null })}
            placeholder="Tüm kaynaklar"
            showClear
          />
          <Dropdown
            value={process}
            options={PROCESS_OPTIONS}
            onChange={(event) => updateParams({ process: event.value, page: null })}
            placeholder="Tüm süreçler"
            aria-label="Sürece göre filtrele"
            showClear
            filter
            filterInputAutoFocus
          />
          <Dropdown
            value={blacklist}
            options={BLACKLIST_OPTIONS}
            onChange={(event) => updateParams({ blacklist: event.value, page: null })}
            placeholder="Kara liste: hepsi"
            aria-label="Kara listeye göre filtrele"
            showClear
            filter
            filterInputAutoFocus
          />
          <InputText
            value={cityText}
            onChange={(event) => setCityText(event.target.value)}
            placeholder="Şehir"
            aria-label="Şehre göre filtrele"
            className="toolbar__city"
          />
          <label className="toolbar__check" htmlFor="only-pool">
            <Checkbox
              inputId="only-pool"
              checked={onlyTalentPool}
              onChange={(event) => updateParams({ pool: event.checked ? '1' : null, page: null })}
            />
            Yalnızca havuzdakiler
          </label>
          {tag && (
            <Tag severity="info" className="filter-chip">
              Etiket: {tag}
              <button
                type="button"
                aria-label="Etiket filtresini kaldır"
                onClick={() => updateParams({ tag: null, page: null })}
              >
                <i className="pi pi-times" aria-hidden="true" />
              </button>
            </Tag>
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
            onRowClick={(event) => navigate(ROUTES.candidate(event.data.id))}
            rowHover
            rowClassName={rowClassName}
            emptyMessage={emptyState}
            className={isFetching ? 'is-refreshing' : undefined}
            aria-busy={isFetching}
          >
            <Column header="Aday" body={nameCell} />
            <Column header="Süreç" body={processCell} />
            <Column
              header="Şehir"
              body={(candidate) => candidate.city ?? <span className="muted">—</span>}
              className="col-desktop"
              headerClassName="col-desktop"
            />
            <Column
              header="Kaynak"
              body={(candidate) => candidate.sourceName ?? <span className="muted">—</span>}
              className="col-desktop"
              headerClassName="col-desktop"
            />
            <Column
              header="Etiketler"
              body={tagsCell}
              className="col-desktop"
              headerClassName="col-desktop"
            />
            <Column
              header="CV"
              body={cvCell}
              className="col-narrow col-desktop"
              headerClassName="col-desktop"
            />
            <Column
              header="Son işlem"
              body={(candidate) => formatDateTime(candidate.lastActivityAt)}
              className="col-desktop"
              headerClassName="col-desktop"
            />
            <Column
              header="Eklenme"
              body={(candidate) => formatDate(candidate.createdAt)}
              className="col-desktop"
              headerClassName="col-desktop"
            />
          </DataTable>
        )}
      </section>

      {adding && (
        <CandidateFormDialog
          onClose={() => setAdding(false)}
          onSaved={(id) => navigate(ROUTES.candidate(id))}
        />
      )}
    </>
  );
}
