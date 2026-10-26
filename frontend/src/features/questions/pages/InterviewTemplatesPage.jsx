import { Button } from 'primereact/button';
import { Checkbox } from 'primereact/checkbox';
import { Column } from 'primereact/column';
import { DataTable } from 'primereact/datatable';
import { IconField } from 'primereact/iconfield';
import { InputIcon } from 'primereact/inputicon';
import { InputText } from 'primereact/inputtext';
import { Message } from 'primereact/message';
import { Tag } from 'primereact/tag';
import { HR_ROLES } from '@/shared/auth/roles';
import { useAuth } from '@/shared/auth/useAuth';
import EmptyState from '@/shared/components/EmptyState';
import PageHeader from '@/shared/components/PageHeader';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { useUrlFilters } from '@/shared/hooks/useUrlFilters';
import { useInterviewTemplates } from '@/shared/recruitment/queries';
import { getErrorMessage } from '@/shared/utils/errors';
import TemplateFormDialog from '../components/TemplateFormDialog';
import '../questions.css';

/**
 * Mülakat şablonları: bir mülakatta sorulacak soruların sıralı listesi. İK şablonları pozisyonlara
 * bağlar; mülakat planlanınca mülakat tipine uyan şablon otomatik gelir.
 */
export default function InterviewTemplatesPage() {
  const { hasAnyRole } = useAuth();
  const isHr = hasAnyRole(HR_ROLES);
  const { params, search, searchText, setSearchText, updateParams } = useUrlFilters();
  const showInactive = params.get('inactive') === '1';
  const selected = params.get('selected');

  const {
    data = [],
    isLoading,
    isFetching,
    isError,
    error,
  } = useInterviewTemplates({
    search: search || undefined,
    isActive: showInactive ? undefined : true,
  });

  const newButton = (
    <Button
      label="Yeni şablon"
      icon="pi pi-plus"
      onClick={() => updateParams({ selected: 'new' })}
    />
  );

  return (
    <>
      <PageHeader
        title="Mülakat şablonları"
        subtitle="Mülakatta sorulacak soruların sıralı listesi. Pozisyon sayfasından pozisyona bağlanır."
        actions={newButton}
      />

      <section className="card">
        <div className="toolbar">
          <IconField iconPosition="left" className="toolbar__search">
            <InputIcon className="pi pi-search" />
            <InputText
              value={searchText}
              onChange={(event) => setSearchText(event.target.value)}
              placeholder="Şablon adıyla ara"
              aria-label="Şablon adıyla ara"
            />
          </IconField>
          <label className="toolbar__check" htmlFor="filter-inactive-templates">
            <Checkbox
              inputId="filter-inactive-templates"
              checked={showInactive}
              onChange={(event) => updateParams({ inactive: event.checked ? '1' : null })}
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
            value={data}
            dataKey="id"
            onRowClick={(event) => updateParams({ selected: event.data.id })}
            rowHover
            className={isFetching ? 'is-refreshing' : undefined}
            emptyMessage={
              search ? (
                <EmptyState icon="pi pi-search" title="Bu adla şablon yok" />
              ) : (
                <EmptyState
                  icon="pi pi-list"
                  title="Henüz şablon yok"
                  description="Soru bankasındaki soruları bir şablonda toplayın; mülakatçılar mülakatta bu soruları görür."
                  action={newButton}
                />
              )
            }
          >
            <Column
              header="Ad"
              body={(template) => (
                <>
                  <span className="table-link">{template.name}</span>
                  {!template.isActive && (
                    <>
                      {' '}
                      <Tag value="Pasif" severity="secondary" />
                    </>
                  )}
                </>
              )}
            />
            {isHr && (
              <Column
                header="Birim"
                field="departmentName"
                className="col-desktop"
                headerClassName="col-desktop"
              />
            )}
            <Column
              header="Mülakat tipi"
              body={(template) => template.interviewTypeName ?? 'Tümü'}
            />
            <Column
              header="Seviye"
              body={(template) => template.levelName ?? 'Tümü'}
              className="col-desktop"
              headerClassName="col-desktop"
            />
            <Column header="Soru" field="questionCount" />
          </DataTable>
        )}
      </section>

      {selected && (
        <TemplateFormDialog
          key={selected}
          templateId={selected === 'new' ? null : selected}
          onClose={() => updateParams({ selected: null })}
        />
      )}
    </>
  );
}
