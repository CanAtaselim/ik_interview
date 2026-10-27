import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button } from 'primereact/button';
import { Column } from 'primereact/column';
import { DataTable } from 'primereact/datatable';
import { confirmDialog } from 'primereact/confirmdialog';
import { Dialog } from 'primereact/dialog';
import { Dropdown } from 'primereact/dropdown';
import { IconField } from 'primereact/iconfield';
import { InputIcon } from 'primereact/inputicon';
import { InputNumber } from 'primereact/inputnumber';
import { InputText } from 'primereact/inputtext';
import { Message } from 'primereact/message';
import { Tag } from 'primereact/tag';
import { useState } from 'react';
import {
  createDictionaryItem,
  deleteDictionaryItem,
  updateDictionaryItem,
} from '@/shared/api/dictionaries';
import EmptyState from '@/shared/components/EmptyState';
import FormField from '@/shared/components/FormField';
import PageHeader from '@/shared/components/PageHeader';
import StatusTag from '@/shared/components/StatusTag';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { useToast } from '@/shared/components/toast';
import { DICTIONARY_TYPES } from '@/shared/constants/labels';
import { useUrlFilters } from '@/shared/hooks/useUrlFilters';
import { dictionaryKeys, useDictionaryAdminItems } from '@/shared/hooks/useDictionary';
import { getErrorMessage } from '@/shared/utils/errors';
import AdminNav from '../components/AdminNav';
import '../admin.css';

const PAGE_SIZE = 20;

const STATUS_OPTIONS = [
  { label: 'Aktif', value: 'active' },
  { label: 'Pasif', value: 'inactive' },
];

const ACTIVE_STATUS = {
  true: { label: 'Aktif', severity: 'success' },
  false: { label: 'Pasif', severity: 'secondary' },
};

/** Kullanım dökümünü "3 kadro talebinde, 2 pozisyonda" biçiminde yazar. */
const describeUsage = (usages) => usages.map((usage) => `${usage.count} ${usage.area}`).join(', ');

/**
 * Seçim listelerinin (birim, seviye, red nedeni…) yönetimi. Liste sunucuda sayfalanır, aranır ve
 * süzülür. Kullanılan öğenin adı değişmez ve öğe silinmez: önce o kayıtlar silinmeli veya başka
 * değere çevrilmeli. Listelerde görünmesini istemeyen öğeyi pasifleştirir.
 */
export default function DictionariesPage() {
  const toast = useToast();
  const queryClient = useQueryClient();
  const { params, search, searchText, setSearchText, page, updateParams } = useUrlFilters();
  const [newName, setNewName] = useState('');
  const [editing, setEditing] = useState(null);
  const [blocked, setBlocked] = useState(null);

  const type =
    DICTIONARY_TYPES.find((entry) => entry.value === params.get('type')) ?? DICTIONARY_TYPES[0];
  const status = params.get('status');
  const { data, isLoading, isFetching, isError, error } = useDictionaryAdminItems(type.value, {
    search: search || undefined,
    isActive: status ? status === 'active' : undefined,
    page,
    pageSize: PAGE_SIZE,
  });
  const items = data?.items ?? [];
  const hasFilters = Boolean(search || status);

  // Başka bir listeye geçince arama, durum ve sayfa sıfırlanır.
  const selectType = (value) => {
    setSearchText('');
    updateParams({ type: value, q: null, status: null, page: null });
  };

  const onDone = (message) => () => {
    queryClient.invalidateQueries({ queryKey: dictionaryKeys.all });
    toast.success(message);
  };
  const onError = (error) => toast.error(getErrorMessage(error));

  const createItem = useMutation({
    mutationFn: (name) => createDictionaryItem(type.value, name),
    onSuccess: () => {
      setNewName('');
      onDone('Öğe eklendi.')();
    },
    onError,
  });
  const updateItem = useMutation({
    mutationFn: ({ id, ...item }) => updateDictionaryItem(id, item),
    onError,
  });
  const deleteItem = useMutation({
    mutationFn: (item) => deleteDictionaryItem(item.id),
    onSuccess: onDone('Öğe silindi.'),
    onError,
  });

  const addItem = (event) => {
    event.preventDefault();
    if (newName.trim()) createItem.mutate(newName.trim());
  };

  const toggleActive = (item) =>
    updateItem.mutate(
      { ...item, isActive: !item.isActive },
      { onSuccess: onDone(item.isActive ? 'Öğe pasifleştirildi.' : 'Öğe etkinleştirildi.') },
    );

  const saveEditing = (event) => {
    event.preventDefault();
    updateItem.mutate(editing, {
      onSuccess: () => {
        setEditing(null);
        onDone('Öğe güncellendi.')();
      },
    });
  };

  const isLocked = (item) => item.usageCount > 0 || item.isSystem;

  // Kullanılan öğe silinmez; kullanıcıya nedenini ve ne yapacağını gösterir.
  const askDelete = (item) => {
    if (isLocked(item)) {
      setBlocked(item);
      return;
    }
    confirmDialog({
      header: 'Öğe silinsin mi?',
      message: `“${item.name}” listeden kalıcı olarak kaldırılacak.`,
      acceptLabel: 'Sil',
      rejectLabel: 'Vazgeç',
      acceptClassName: 'p-button-danger',
      accept: () => deleteItem.mutate(item),
    });
  };

  const usageCell = (item) => {
    if (item.isSystem) return <Tag value="Sistem öğesi" severity="info" />;
    if (item.usageCount === 0) return <span className="muted">—</span>;
    return (
      <div>
        <Tag value="Kullanımda" severity="warning" />
        <div className="cell-sub">{describeUsage(item.usages)}</div>
      </div>
    );
  };

  const actionsCell = (item) => (
    <div className="row-actions">
      <Button
        icon="pi pi-pencil"
        text
        severity="secondary"
        aria-label={`${item.name} öğesini düzenle`}
        onClick={() => setEditing({ ...item })}
      />
      <Button
        label={item.isActive ? 'Pasifleştir' : 'Etkinleştir'}
        text
        severity="secondary"
        disabled={updateItem.isPending}
        onClick={() => toggleActive(item)}
      />
      <Button
        icon="pi pi-trash"
        text
        severity="secondary"
        aria-label={`${item.name} öğesini sil`}
        onClick={() => askDelete(item)}
      />
    </div>
  );

  return (
    <>
      <AdminNav />
      <PageHeader
        title="Sözlükler"
        subtitle="Formlarda çıkan seçim listelerini buradan yönetirsiniz."
      />

      <div className="dictionaries">
        <nav className="card dictionaries__types" aria-label="Sözlük türleri">
          {DICTIONARY_TYPES.map((entry) => (
            <button
              key={entry.value}
              type="button"
              className="dictionaries__type"
              aria-current={entry.value === type.value ? 'page' : undefined}
              onClick={() => selectType(entry.value)}
            >
              {entry.label}
            </button>
          ))}
        </nav>

        <section className="card" aria-labelledby="dictionary-title">
          <form className="toolbar" onSubmit={addItem}>
            <h2 id="dictionary-title" className="section-title toolbar__title">
              {type.label}
            </h2>
            <InputText
              value={newName}
              onChange={(event) => setNewName(event.target.value)}
              placeholder="Yeni öğe adı"
              aria-label="Yeni öğe adı"
              maxLength={200}
            />
            <Button
              type="submit"
              label="Ekle"
              icon="pi pi-plus"
              disabled={!newName.trim()}
              loading={createItem.isPending}
            />
          </form>

          <div className="toolbar">
            <IconField iconPosition="left" className="toolbar__search">
              <InputIcon className="pi pi-search" />
              <InputText
                value={searchText}
                onChange={(event) => setSearchText(event.target.value)}
                placeholder="Listede ara"
                aria-label="Listede ara"
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
              value={items}
              dataKey="id"
              lazy
              paginator={(data?.totalCount ?? 0) > PAGE_SIZE}
              rows={PAGE_SIZE}
              first={(page - 1) * PAGE_SIZE}
              totalRecords={data?.totalCount ?? 0}
              onPage={(event) => updateParams({ page: event.page === 0 ? null : event.page + 1 })}
              className={isFetching ? 'is-refreshing' : undefined}
              aria-busy={isFetching}
              emptyMessage={
                hasFilters ? (
                  <EmptyState
                    icon="pi pi-search"
                    title="Aramanıza uyan öğe bulunamadı"
                    description="Aramayı kısaltın veya durum süzgecini kaldırın."
                  />
                ) : (
                  <EmptyState
                    icon="pi pi-list"
                    title="Bu listede henüz öğe yok"
                    description="Yukarıdaki kutuya bir ad yazıp “Ekle”ye basın."
                  />
                )
              }
            >
              <Column header="Ad" field="name" />
              <Column header="Kullanım" body={usageCell} />
              <Column
                header="Durum"
                body={(item) => <StatusTag value={String(item.isActive)} map={ACTIVE_STATUS} />}
              />
              <Column header="" body={actionsCell} className="col-actions" />
            </DataTable>
          )}
        </section>
      </div>

      {blocked && (
        <Dialog
          visible
          header="Bu öğe silinemez"
          onHide={() => setBlocked(null)}
          modal
          footer={
            <div className="dialog-actions">
              {blocked.isActive && !blocked.isSystem && (
                <Button
                  type="button"
                  label="Pasifleştir"
                  text
                  severity="secondary"
                  onClick={() => {
                    toggleActive(blocked);
                    setBlocked(null);
                  }}
                />
              )}
              <Button type="button" label="Tamam" onClick={() => setBlocked(null)} />
            </div>
          }
        >
          {blocked.isSystem ? (
            <p>“{blocked.name}” öğesini sistem kullanıyor; silinemez ve adı değiştirilemez.</p>
          ) : (
            <>
              <p>
                “{blocked.name}” şu kayıtlarda kullanılıyor:{' '}
                <strong>{describeUsage(blocked.usages)}</strong>.
              </p>
              <p className="muted">
                Önce bu kayıtları silin veya başka bir değerle değiştirin; sonra öğeyi silebilir ve
                adını değiştirebilirsiniz. Listelerde görünmesini istemiyorsanız
                pasifleştirebilirsiniz.
              </p>
            </>
          )}
        </Dialog>
      )}

      {editing && (
        <Dialog
          visible
          header="Öğeyi düzenle"
          onHide={() => setEditing(null)}
          modal
          footer={
            <div className="dialog-actions">
              <Button
                type="button"
                label="Vazgeç"
                text
                severity="secondary"
                onClick={() => setEditing(null)}
              />
              <Button
                type="submit"
                form="dictionary-item-form"
                label="Kaydet"
                disabled={!editing.name.trim()}
                loading={updateItem.isPending}
              />
            </div>
          }
        >
          <form id="dictionary-item-form" className="form-grid" onSubmit={saveEditing}>
            <div className="form-grid__wide">
              <FormField label="Ad" htmlFor="dictionary-item-name">
                <InputText
                  id="dictionary-item-name"
                  value={editing.name}
                  maxLength={200}
                  disabled={isLocked(editing)}
                  onChange={(event) => setEditing({ ...editing, name: event.target.value })}
                />
                {isLocked(editing) && (
                  <small className="form-field__hint">
                    {editing.isSystem
                      ? 'Bu öğeyi sistem kullanıyor; adı değiştirilemez.'
                      : `Bu öğe ${describeUsage(editing.usages)} kullanılıyor; adı değiştirilemez. Önce o kayıtları silin veya başka bir değerle değiştirin.`}
                  </small>
                )}
              </FormField>
            </div>
            <FormField label="Sıra" htmlFor="dictionary-item-order">
              <InputNumber
                inputId="dictionary-item-order"
                value={editing.sortOrder}
                min={0}
                showButtons
                onValueChange={(event) => setEditing({ ...editing, sortOrder: event.value ?? 0 })}
              />
            </FormField>
          </form>
        </Dialog>
      )}
    </>
  );
}
