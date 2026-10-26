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
import { HR_ROLES } from '@/shared/auth/roles';
import { useAuth } from '@/shared/auth/useAuth';
import DictionarySelect from '@/shared/components/DictionarySelect';
import EmptyState from '@/shared/components/EmptyState';
import PageHeader from '@/shared/components/PageHeader';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { DIFFICULTY_LABELS, QUESTION_TYPE_LABELS, toOptions } from '@/shared/constants/labels';
import { useUrlFilters } from '@/shared/hooks/useUrlFilters';
import { getErrorMessage } from '@/shared/utils/errors';
import QuestionFormDialog from '../components/QuestionFormDialog';
import { useQuestions } from '../queries';
import '../questions.css';

const PAGE_SIZE = 20;
const TYPE_OPTIONS = toOptions(QUESTION_TYPE_LABELS);

/**
 * Soru bankası. Takım lideri kendi biriminin sorularını, İK tüm birimlerin sorularını yönetir.
 * Sorular mülakat şablonlarında kullanılır.
 */
export default function QuestionBankPage() {
  const { hasAnyRole } = useAuth();
  const isHr = hasAnyRole(HR_ROLES);
  const { params, search, searchText, setSearchText, page, updateParams } = useUrlFilters();
  const departmentId = params.get('department');
  const competencyId = params.get('competency');
  const type = params.get('type');
  const showInactive = params.get('inactive') === '1';
  // Açık pencere: null | 'new' | soru kimliği
  const selected = params.get('selected');

  const filtered = Boolean(search || departmentId || competencyId || type);
  const { data, isLoading, isFetching, isError, error } = useQuestions({
    search: search || undefined,
    departmentId: departmentId || undefined,
    competencyId: competencyId || undefined,
    type: type || undefined,
    isActive: showInactive ? undefined : true,
    page,
    pageSize: PAGE_SIZE,
  });

  const questionCell = (question) => (
    <>
      <span className="question-text">{question.text}</span>
      <div className="cell-sub">
        {[question.positionTitle, question.levelName, isHr && question.departmentName]
          .filter(Boolean)
          .join(' · ')}
        {!question.isActive && (
          <>
            {' '}
            <Tag value="Pasif" severity="secondary" />
          </>
        )}
      </div>
    </>
  );

  const emptyState = filtered ? (
    <EmptyState
      icon="pi pi-search"
      title="Bu filtrelere uyan soru yok"
      description="Arama metnini veya filtreleri değiştirip tekrar deneyin."
    />
  ) : (
    <EmptyState
      icon="pi pi-question-circle"
      title="Soru bankası boş"
      description="Mülakatlarda sorulacak soruları ekleyin; sonra bunları mülakat şablonlarında toplayın."
      action={
        <Button
          label="Yeni soru"
          icon="pi pi-plus"
          onClick={() => updateParams({ selected: 'new' })}
        />
      }
    />
  );

  return (
    <>
      <PageHeader
        title="Soru bankası"
        subtitle={
          isHr
            ? 'Tüm birimlerin mülakat soruları. Takım liderleri kendi birimlerinin sorularını yönetir.'
            : 'Biriminizin mülakat soruları.'
        }
        actions={
          <Button
            label="Yeni soru"
            icon="pi pi-plus"
            onClick={() => updateParams({ selected: 'new' })}
          />
        }
      />

      <section className="card">
        <div className="toolbar">
          <IconField iconPosition="left" className="toolbar__search">
            <InputIcon className="pi pi-search" />
            <InputText
              value={searchText}
              onChange={(event) => setSearchText(event.target.value)}
              placeholder="Soru metninde ara"
              aria-label="Soru metninde ara"
            />
          </IconField>
          {isHr && (
            <DictionarySelect
              type="Department"
              inputId="filter-department"
              value={departmentId}
              onChange={(value) => updateParams({ department: value, page: null })}
              placeholder="Tüm birimler"
              showClear
            />
          )}
          <DictionarySelect
            type="Competency"
            inputId="filter-competency"
            value={competencyId}
            onChange={(value) => updateParams({ competency: value, page: null })}
            placeholder="Tüm yetkinlikler"
            showClear
          />
          <Dropdown
            value={type}
            options={TYPE_OPTIONS}
            onChange={(event) => updateParams({ type: event.value, page: null })}
            placeholder="Tüm tipler"
            aria-label="Tipe göre filtrele"
            showClear
          />
          <label className="toolbar__check" htmlFor="filter-inactive">
            <Checkbox
              inputId="filter-inactive"
              checked={showInactive}
              onChange={(event) =>
                updateParams({ inactive: event.checked ? '1' : null, page: null })
              }
            />
            Pasifleri de göster
          </label>
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
            onRowClick={(event) => updateParams({ selected: event.data.id })}
            rowHover
            emptyMessage={emptyState}
            className={isFetching ? 'is-refreshing' : undefined}
            aria-busy={isFetching}
          >
            <Column header="Soru" body={questionCell} />
            <Column header="Yetkinlik" field="competencyName" />
            <Column
              header="Tip"
              body={(question) => QUESTION_TYPE_LABELS[question.type]}
              className="col-desktop"
              headerClassName="col-desktop"
            />
            <Column
              header="Zorluk"
              body={(question) => DIFFICULTY_LABELS[question.difficulty]}
              className="col-desktop"
              headerClassName="col-desktop"
            />
            <Column
              header="Ağırlık"
              body={(question) => question.weight.toLocaleString('tr-TR')}
              className="col-desktop"
              headerClassName="col-desktop"
            />
            <Column
              header="Şablon"
              body={(question) => question.templateCount || '—'}
              className="col-desktop"
              headerClassName="col-desktop"
            />
          </DataTable>
        )}
      </section>

      {selected && (
        <QuestionFormDialog
          key={selected}
          questionId={selected === 'new' ? null : selected}
          onClose={() => updateParams({ selected: null })}
        />
      )}
    </>
  );
}
