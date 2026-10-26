import dayjs from 'dayjs';
import { Button } from 'primereact/button';
import { Calendar } from 'primereact/calendar';
import { Checkbox } from 'primereact/checkbox';
import { confirmDialog } from 'primereact/confirmdialog';
import { Column } from 'primereact/column';
import { DataTable } from 'primereact/datatable';
import { InputText } from 'primereact/inputtext';
import { useState } from 'react';
import EmptyState from '@/shared/components/EmptyState';
import FormField from '@/shared/components/FormField';
import PageHeader from '@/shared/components/PageHeader';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { useToast } from '@/shared/components/toast';
import { getErrorMessage } from '@/shared/utils/errors';
import { toDateOnly } from '@/shared/utils/format';
import AdminNav from '../components/AdminNav';
import { useCreateHoliday, useDeleteHoliday, useHolidays } from '../queries';
import '../admin.css';

/**
 * Resmi tatiller: CV değerlendirme son tarihi gibi iş günü hesapları bu günleri saymaz. Her yıl aynı
 * güne gelenler (29 Ekim) bir kez eklenir; tarihi değişen bayramlar o yıla özel eklenir.
 */
export default function HolidaysPage() {
  const toast = useToast();
  const [year, setYear] = useState(() => dayjs().year());
  const { data: holidays = [], isLoading } = useHolidays(year);
  const create = useCreateHoliday();
  const remove = useDeleteHoliday();
  const [name, setName] = useState('');
  const [date, setDate] = useState(null);
  const [recurring, setRecurring] = useState(false);
  const [errors, setErrors] = useState({});

  const add = (event) => {
    event.preventDefault();
    const found = {};
    if (!name.trim()) found.name = 'Tatilin adını yazmalısınız.';
    if (!date) found.date = 'Tarih seçmelisiniz.';
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    create.mutate(
      { name: name.trim(), date: toDateOnly(date), recurring },
      {
        onSuccess: () => {
          setYear(date.getFullYear());
          setName('');
          setDate(null);
          setRecurring(false);
          toast.success(
            'Resmi tatil eklendi. Bundan sonra hesaplanan son tarihler bu günü saymaz.',
          );
        },
        onError: (error) => {
          const fieldErrors = error.response?.data?.errors;
          if (fieldErrors)
            setErrors(
              Object.fromEntries(
                Object.entries(fieldErrors).map(([field, messages]) => [field, messages[0]]),
              ),
            );
          else toast.error(getErrorMessage(error));
        },
      },
    );
  };

  const confirmDelete = (holiday) =>
    confirmDialog({
      header: 'Resmi tatil silinsin mi?',
      message: `“${holiday.name}” tatil listesinden çıkarılacak. Bu gün tekrar iş günü sayılır.`,
      acceptLabel: 'Sil',
      rejectLabel: 'Vazgeç',
      acceptClassName: 'p-button-danger',
      accept: () =>
        remove.mutate(holiday.id, {
          onSuccess: () => toast.success('Resmi tatil silindi.'),
          onError: (error) => toast.error(getErrorMessage(error)),
        }),
    });

  return (
    <>
      <AdminNav />
      <PageHeader
        title="Resmi tatiller"
        subtitle="Son tarih hesaplarında iş günü sayılmayan günler. Hafta sonları zaten sayılmaz."
      />
      <div className="detail-sections">
        <section className="card card--padded settings-card" aria-labelledby="holiday-add">
          <h2 id="holiday-add" className="section-title">
            Tatil ekle
          </h2>
          <form className="inline-form" onSubmit={add} noValidate>
            <FormField label="Ad" htmlFor="holiday-name" error={errors.name}>
              <InputText
                id="holiday-name"
                value={name}
                maxLength={100}
                placeholder="Ör. Kurban Bayramı 1. gün"
                invalid={Boolean(errors.name)}
                onChange={(event) => {
                  setName(event.target.value);
                  setErrors((current) => ({ ...current, name: undefined }));
                }}
              />
            </FormField>
            <FormField label="Tarih" htmlFor="holiday-date" error={errors.date}>
              <Calendar
                inputId="holiday-date"
                value={date}
                dateFormat="dd.mm.yy"
                showIcon
                invalid={Boolean(errors.date)}
                onChange={(event) => {
                  setDate(event.value);
                  setErrors((current) => ({ ...current, date: undefined }));
                }}
              />
            </FormField>
            <label className="toolbar__check" htmlFor="holiday-recurring">
              <Checkbox
                inputId="holiday-recurring"
                checked={recurring}
                onChange={(event) => setRecurring(event.checked)}
              />
              Her yıl tekrarlansın
            </label>
            <Button type="submit" label="Ekle" icon="pi pi-plus" loading={create.isPending} />
          </form>
          <p className="muted">
            Dini bayramların tarihi her yıl değişir; bunları “her yıl tekrarlansın” işaretlemeden,
            ilgili yıl için ekleyin.
          </p>
        </section>

        <section className="card" aria-label={`${year} resmi tatilleri`}>
          <div className="toolbar">
            <Button
              icon="pi pi-chevron-left"
              text
              severity="secondary"
              aria-label="Önceki yıl"
              onClick={() => setYear((current) => current - 1)}
            />
            <strong className="toolbar__title">{year}</strong>
            <Button
              icon="pi pi-chevron-right"
              text
              severity="secondary"
              aria-label="Sonraki yıl"
              onClick={() => setYear((current) => current + 1)}
            />
          </div>
          {isLoading ? (
            <TableSkeleton rows={4} />
          ) : (
            <DataTable
              value={holidays}
              dataKey="id"
              emptyMessage={
                <EmptyState
                  icon="pi pi-calendar"
                  title={`${year} için resmi tatil tanımlı değil`}
                />
              }
            >
              <Column
                header="Tarih"
                body={(holiday) => dayjs(holiday.date).format('DD.MM.YYYY dddd')}
              />
              <Column header="Ad" field="name" />
              <Column
                header="Tekrar"
                body={(holiday) => (holiday.isRecurring ? 'Her yıl' : `Yalnızca ${year}`)}
                className="col-desktop"
                headerClassName="col-desktop"
              />
              <Column
                header=""
                className="col-actions"
                body={(holiday) => (
                  <Button
                    icon="pi pi-trash"
                    text
                    severity="secondary"
                    aria-label={`${holiday.name} tatilini sil`}
                    onClick={() => confirmDelete(holiday)}
                  />
                )}
              />
            </DataTable>
          )}
        </section>
      </div>
    </>
  );
}
