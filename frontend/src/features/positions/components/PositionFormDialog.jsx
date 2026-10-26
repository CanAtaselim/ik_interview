import { zodResolver } from '@hookform/resolvers/zod';
import { Button } from 'primereact/button';
import { Dialog } from 'primereact/dialog';
import { InputNumber } from 'primereact/inputnumber';
import { InputText } from 'primereact/inputtext';
import { InputTextarea } from 'primereact/inputtextarea';
import { Controller, useForm } from 'react-hook-form';
import { HR_ROLES } from '@/shared/auth/roles';
import DictionarySelect from '@/shared/components/DictionarySelect';
import FormField from '@/shared/components/FormField';
import { useToast } from '@/shared/components/toast';
import UserSelect from '@/shared/components/UserSelect';
import { applyFieldErrors, getErrorMessage } from '@/shared/utils/errors';
import { useCreatePosition, useUpdatePosition } from '../queries';
import { createPositionSchema, updatePositionSchema } from '../schemas';

/**
 * Pozisyon açar (talepsiz) veya mevcut pozisyonu düzenler.
 * @param {{ position?: object | null, onClose: () => void, onSaved: (id: string) => void }} props
 */
export default function PositionFormDialog({ position = null, onClose, onSaved }) {
  const toast = useToast();
  const createPosition = useCreatePosition();
  const updatePosition = useUpdatePosition();
  const isEdit = Boolean(position);

  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm({
    resolver: zodResolver(isEdit ? updatePositionSchema : createPositionSchema),
    defaultValues: {
      title: position?.title ?? '',
      departmentId: position?.departmentId ?? '',
      levelId: position?.levelId ?? '',
      headcount: position?.headcount ?? 1,
      jobDescription: position?.jobDescription ?? '',
      responsibleHrUserId: position?.responsibleHrUserId ?? null,
    },
  });

  const save = async (values) => {
    const input = { ...values, jobDescription: values.jobDescription || null };
    try {
      if (isEdit) {
        await updatePosition.mutateAsync({ id: position.id, input });
        toast.success('Pozisyon güncellendi.');
        onSaved(position.id);
      } else {
        const id = await createPosition.mutateAsync(input);
        toast.success('Pozisyon açıldı.');
        onSaved(id);
      }
    } catch (error) {
      if (!applyFieldErrors(error, setError)) toast.error(getErrorMessage(error));
    }
  };

  const footer = (
    <div className="dialog-actions">
      <Button type="button" label="Vazgeç" text severity="secondary" onClick={onClose} />
      <Button
        type="button"
        label={isEdit ? 'Kaydet' : 'Pozisyonu aç'}
        loading={isSubmitting}
        onClick={handleSubmit(save)}
      />
    </div>
  );

  return (
    <Dialog
      visible
      header={isEdit ? 'Pozisyonu düzenle' : 'Yeni pozisyon'}
      footer={footer}
      onHide={onClose}
      className="form-dialog"
      modal
    >
      <form className="form-grid" onSubmit={(event) => event.preventDefault()} noValidate>
        <div className="form-grid__wide">
          <FormField label="Pozisyon adı" htmlFor="position-title" error={errors.title?.message}>
            <InputText id="position-title" invalid={Boolean(errors.title)} {...register('title')} />
          </FormField>
        </div>

        {!isEdit && (
          <>
            <FormField
              label="Birim"
              htmlFor="position-department"
              error={errors.departmentId?.message}
            >
              <Controller
                name="departmentId"
                control={control}
                render={({ field }) => (
                  <DictionarySelect
                    type="Department"
                    inputId="position-department"
                    value={field.value}
                    onChange={(id) => field.onChange(id ?? '')}
                    invalid={Boolean(errors.departmentId)}
                  />
                )}
              />
            </FormField>

            <FormField label="Seviye" htmlFor="position-level" error={errors.levelId?.message}>
              <Controller
                name="levelId"
                control={control}
                render={({ field }) => (
                  <DictionarySelect
                    type="Level"
                    inputId="position-level"
                    value={field.value}
                    onChange={(id) => field.onChange(id ?? '')}
                    invalid={Boolean(errors.levelId)}
                  />
                )}
              />
            </FormField>
          </>
        )}

        <FormField
          label="Kişi sayısı"
          htmlFor="position-headcount"
          error={errors.headcount?.message}
        >
          <Controller
            name="headcount"
            control={control}
            render={({ field }) => (
              <InputNumber
                inputId="position-headcount"
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
          label="Sorumlu İK"
          htmlFor="position-hr"
          error={errors.responsibleHrUserId?.message}
        >
          <Controller
            name="responsibleHrUserId"
            control={control}
            render={({ field }) => (
              <UserSelect
                roles={HR_ROLES}
                inputId="position-hr"
                value={field.value}
                onChange={field.onChange}
                placeholder="Henüz atanmadı"
                invalid={Boolean(errors.responsibleHrUserId)}
              />
            )}
          />
        </FormField>

        <div className="form-grid__wide">
          <FormField
            label="İş tanımı"
            htmlFor="position-description"
            error={errors.jobDescription?.message}
          >
            <InputTextarea
              id="position-description"
              rows={6}
              autoResize
              placeholder="Sorumluluklar, aranan nitelikler, çalışma şekli…"
              invalid={Boolean(errors.jobDescription)}
              {...register('jobDescription')}
            />
          </FormField>
        </div>
      </form>
    </Dialog>
  );
}
