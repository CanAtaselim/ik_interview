import { zodResolver } from '@hookform/resolvers/zod';
import { Button } from 'primereact/button';
import { Calendar } from 'primereact/calendar';
import { Chips } from 'primereact/chips';
import { confirmDialog } from 'primereact/confirmdialog';
import { Dialog } from 'primereact/dialog';
import { InputNumber } from 'primereact/inputnumber';
import { InputText } from 'primereact/inputtext';
import { InputTextarea } from 'primereact/inputtextarea';
import { SelectButton } from 'primereact/selectbutton';
import { Controller, useForm, useWatch } from 'react-hook-form';
import { useAuth } from '@/shared/auth/useAuth';
import DictionarySelect from '@/shared/components/DictionarySelect';
import FormField from '@/shared/components/FormField';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { useToast } from '@/shared/components/toast';
import { HIRING_REASON_LABELS, toOptions, URGENCY } from '@/shared/constants/labels';
import { useDictionary } from '@/shared/hooks/useDictionary';
import { applyFieldErrors, getErrorMessage } from '@/shared/utils/errors';
import {
  useCreateHiringRequest,
  useHiringRequest,
  useSubmitHiringRequest,
  useUpdateHiringRequest,
} from '../queries';
import {
  emptyHiringRequestForm,
  hiringRequestSchema,
  toHiringRequestForm,
  toHiringRequestInput,
} from '../schemas';

const REASON_OPTIONS = toOptions(HIRING_REASON_LABELS);
const URGENCY_OPTIONS = toOptions(URGENCY);

/**
 * Yeni kadro talebi açar veya taslağı düzenler. Form, gerekli veriler (birimler ve
 * düzenlemede talebin kendisi) geldikten sonra kurulur; böylece ilk değerler baştan doğrudur.
 * @param {{ requestId?: string | null, onClose: () => void, onSaved: (id: string) => void }} props
 */
export default function HiringRequestFormDialog({ requestId = null, onClose, onSaved }) {
  const { user } = useAuth();
  const departments = useDictionary('Department');
  const existing = useHiringRequest(requestId);
  const isReady = departments.isSuccess && (!requestId || existing.isSuccess);

  if (!isReady) {
    return (
      <Dialog visible header="Kadro talebi" onHide={onClose} className="form-dialog" modal>
        <TableSkeleton rows={6} />
      </Dialog>
    );
  }

  // Yeni talepte birim, kullanıcının kendi birimiyle önceden seçilir.
  const ownDepartmentId = departments.data.find((item) => item.name === user.department)?.id;
  const initialValues = existing.data
    ? toHiringRequestForm(existing.data)
    : emptyHiringRequestForm(ownDepartmentId);

  return (
    <HiringRequestForm
      requestId={requestId}
      initialValues={initialValues}
      onClose={onClose}
      onSaved={onSaved}
    />
  );
}

function HiringRequestForm({ requestId, initialValues, onClose, onSaved }) {
  const toast = useToast();
  const createRequest = useCreateHiringRequest();
  const updateRequest = useUpdateHiringRequest();
  const submitRequest = useSubmitHiringRequest();
  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isDirty, isSubmitting },
  } = useForm({ resolver: zodResolver(hiringRequestSchema), defaultValues: initialValues });

  const isReplacement = useWatch({ control, name: 'reason' }) === 'Replacement';

  const save = async (values, sendForApproval) => {
    let id = requestId;
    try {
      const input = toHiringRequestInput(values);
      if (id) await updateRequest.mutateAsync({ id, input });
      else id = await createRequest.mutateAsync(input);
    } catch (error) {
      if (!applyFieldErrors(error, setError)) toast.error(getErrorMessage(error));
      return;
    }

    if (!sendForApproval) {
      toast.success('Talep taslak olarak kaydedildi.');
      onSaved(id);
      return;
    }

    try {
      await submitRequest.mutateAsync(id);
      toast.success('Talep onaya gönderildi.');
    } catch (error) {
      toast.error(
        `Talep taslak olarak kaydedildi ama onaya gönderilemedi. ${getErrorMessage(error)}`,
      );
    }
    onSaved(id);
  };

  const closeSafely = () => {
    if (!isDirty) {
      onClose();
      return;
    }

    confirmDialog({
      header: 'Pencere kapatılsın mı?',
      message: 'Kaydetmediğiniz değişiklikler silinecek.',
      acceptLabel: 'Kapat',
      rejectLabel: 'Düzenlemeye dön',
      accept: onClose,
    });
  };

  const footer = (
    <div className="dialog-actions">
      <Button type="button" label="Vazgeç" text severity="secondary" onClick={closeSafely} />
      <Button
        type="button"
        label={requestId ? 'Kaydet' : 'Taslak olarak kaydet'}
        outlined
        disabled={isSubmitting}
        onClick={handleSubmit((values) => save(values, false))}
      />
      <Button
        type="button"
        label="Kaydet ve onaya gönder"
        icon="pi pi-send"
        loading={isSubmitting}
        onClick={handleSubmit((values) => save(values, true))}
      />
    </div>
  );

  return (
    <Dialog
      visible
      header={requestId ? 'Kadro talebini düzenle' : 'Yeni kadro talebi'}
      footer={footer}
      onHide={closeSafely}
      className="form-dialog"
      modal
    >
      <form className="form-grid" onSubmit={(event) => event.preventDefault()} noValidate>
        <p className="form-grid__wide muted">
          Tüm alanlar zorunludur (<span className="form-field__required">*</span>). Yerine alınan
          çalışanın adı yalnızca “Yerine alım” seçilince istenir.
        </p>

        <FormField
          label="Birim"
          htmlFor="departmentId"
          error={errors.departmentId?.message}
          required
        >
          <Controller
            name="departmentId"
            control={control}
            render={({ field }) => (
              <DictionarySelect
                type="Department"
                inputId="departmentId"
                value={field.value}
                onChange={(id) => field.onChange(id ?? '')}
                invalid={Boolean(errors.departmentId)}
              />
            )}
          />
        </FormField>

        <FormField
          label="Pozisyon adı"
          htmlFor="title"
          error={errors.title?.message}
          hint="Aradığınız ad listede yoksa İK’dan eklemesini isteyin."
          required
        >
          <Controller
            name="title"
            control={control}
            render={({ field }) => (
              <DictionarySelect
                type="PositionTitle"
                valueBy="name"
                inputId="title"
                placeholder="Pozisyon adını seçin"
                value={field.value}
                onChange={(name) => field.onChange(name ?? '')}
                invalid={Boolean(errors.title)}
              />
            )}
          />
        </FormField>

        <FormField label="Seviye" htmlFor="levelId" error={errors.levelId?.message} required>
          <Controller
            name="levelId"
            control={control}
            render={({ field }) => (
              <DictionarySelect
                type="Level"
                inputId="levelId"
                value={field.value}
                onChange={(id) => field.onChange(id ?? '')}
                invalid={Boolean(errors.levelId)}
              />
            )}
          />
        </FormField>

        <FormField
          label="Kişi sayısı"
          htmlFor="headcount"
          error={errors.headcount?.message}
          required
        >
          <Controller
            name="headcount"
            control={control}
            render={({ field }) => (
              <InputNumber
                inputId="headcount"
                value={field.value}
                onValueChange={(event) => field.onChange(event.value)}
                min={1}
                max={1000}
                showButtons
                invalid={Boolean(errors.headcount)}
              />
            )}
          />
        </FormField>

        <FormField
          label="Beklenen başlangıç tarihi"
          htmlFor="expectedStartDate"
          error={errors.expectedStartDate?.message}
          required
        >
          <Controller
            name="expectedStartDate"
            control={control}
            render={({ field }) => (
              <Calendar
                inputId="expectedStartDate"
                value={field.value}
                onChange={(event) => field.onChange(event.value ?? null)}
                dateFormat="dd.mm.yy"
                placeholder="Tarih seçin"
                invalid={Boolean(errors.expectedStartDate)}
                showIcon
              />
            )}
          />
        </FormField>

        <FormField label="Talep nedeni" htmlFor="reason" error={errors.reason?.message} required>
          <Controller
            name="reason"
            control={control}
            render={({ field }) => (
              <SelectButton
                id="reason"
                value={field.value}
                options={REASON_OPTIONS}
                onChange={(event) => field.onChange(event.value)}
                allowEmpty={false}
              />
            )}
          />
        </FormField>

        <FormField label="Aciliyet" htmlFor="urgency" error={errors.urgency?.message} required>
          <Controller
            name="urgency"
            control={control}
            render={({ field }) => (
              <SelectButton
                id="urgency"
                value={field.value}
                options={URGENCY_OPTIONS}
                onChange={(event) => field.onChange(event.value)}
                allowEmpty={false}
              />
            )}
          />
        </FormField>

        {isReplacement && (
          <FormField
            label="Yerine alım yapılacak çalışan"
            htmlFor="replacedEmployeeName"
            error={errors.replacedEmployeeName?.message}
            required
          >
            <InputText
              id="replacedEmployeeName"
              placeholder="Ayrılan çalışanın adı soyadı"
              invalid={Boolean(errors.replacedEmployeeName)}
              {...register('replacedEmployeeName')}
            />
          </FormField>
        )}

        <div className="form-grid__wide">
          <FormField
            label="Aranan yetkinlikler"
            htmlFor="competencies"
            error={errors.competencies?.message}
            required
          >
            <Controller
              name="competencies"
              control={control}
              render={({ field }) => (
                <Chips
                  inputId="competencies"
                  value={field.value}
                  onChange={(event) => field.onChange(event.value ?? [])}
                  separator=","
                  addOnBlur
                  placeholder={
                    field.value.length === 0 ? 'Yazıp Enter’a basın (örn. C#, PostgreSQL)' : ''
                  }
                />
              )}
            />
          </FormField>
        </div>

        <div className="form-grid__wide">
          <FormField
            label="Açıklama"
            htmlFor="description"
            error={errors.description?.message}
            required
          >
            <InputTextarea
              id="description"
              rows={4}
              autoResize
              placeholder="Bu kadroya neden ihtiyaç var, hangi ekipte çalışacak?"
              invalid={Boolean(errors.description)}
              {...register('description')}
            />
          </FormField>
        </div>
      </form>
    </Dialog>
  );
}
