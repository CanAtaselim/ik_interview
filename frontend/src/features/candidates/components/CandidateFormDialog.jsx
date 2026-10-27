import { zodResolver } from '@hookform/resolvers/zod';
import { Button } from 'primereact/button';
import { Chips } from 'primereact/chips';
import { Dialog } from 'primereact/dialog';
import { InputText } from 'primereact/inputtext';
import { InputTextarea } from 'primereact/inputtextarea';
import { useRef, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { findBlacklistMatches } from '@/shared/api/blacklist';
import DictionarySelect from '@/shared/components/DictionarySelect';
import FileDropzone from '@/shared/components/FileDropzone';
import FormField from '@/shared/components/FormField';
import { useToast } from '@/shared/components/toast';
import BlacklistWarning from '@/shared/recruitment/BlacklistWarning';
import { applyFieldErrors, getErrorMessage } from '@/shared/utils/errors';
import { formatFileSize } from '@/shared/utils/format';
import { findDuplicates, prefillFromCv } from '../api';
import { useDocumentType } from '../documentTypes';
import { useCreateCandidate, useUpdateCandidate, useUploadDocument } from '../queries';
import { candidateSchema, cvPrefillPatch, toCandidateForm, toCandidateInput } from '../schemas';
import DuplicateWarning from './DuplicateWarning';

const DUPLICATE_CODE = 'Candidate.PossibleDuplicate';
const BLACKLIST_CODE = 'Candidate.BlacklistMatch';

/**
 * Aday ekler veya düzenler. Eklemede yalnızca en gerekli alanlar ve CV sorulur (hızlı ekleme);
 * diğer bilgiler aday kartından sonra girilir. Ad, e-posta veya telefon alanından çıkınca mükerrer
 * kayıt ve kara liste eşleşmesi aranır; varsa formun üstünde uyarı gösterilir.
 * @param {{ candidate?: object | null, onClose: () => void, onSaved: (id: string) => void }} props
 */
export default function CandidateFormDialog({ candidate = null, onClose, onSaved }) {
  const toast = useToast();
  const isEdit = Boolean(candidate);
  const createCandidate = useCreateCandidate();
  const updateCandidate = useUpdateCandidate();
  const uploadDocument = useUploadDocument();
  const [duplicates, setDuplicates] = useState([]);
  const [blacklistMatches, setBlacklistMatches] = useState([]);
  const [file, setFile] = useState(null);
  const { typeId: cvTypeId } = useDocumentType(null);
  // CV'den yazılan alanlar ve yazılan değerler; CV kaldırılınca yalnızca elle değiştirilmemiş olanlar temizlenir.
  const prefilled = useRef({});
  const prefillRequest = useRef(0);

  const {
    register,
    control,
    handleSubmit,
    getValues,
    setValue,
    setError,
    formState: { errors, isSubmitting },
  } = useForm({
    resolver: zodResolver(candidateSchema),
    defaultValues: toCandidateForm(candidate),
  });

  // Alan boşaltıldıysa veya değer değiştiyse uyarılar da güncellenir. Kontrol yapılamazsa kayıt
  // engellenmez; sunucu kaydederken yeniden kontrol eder.
  const checkWarnings = async () => {
    const { firstName, lastName, email, phone } = getValues();
    const [foundDuplicates, foundMatches] = await Promise.allSettled([
      findDuplicates({ email, phone, excludeId: candidate?.id }),
      findBlacklistMatches({
        firstName,
        lastName,
        email,
        phone,
        excludeCandidateId: candidate?.id,
      }),
    ]);
    if (foundDuplicates.status === 'fulfilled') setDuplicates(foundDuplicates.value);
    if (foundMatches.status === 'fulfilled') setBlacklistMatches(foundMatches.value);
  };

  // CV'den yazılan alanları geri alır; kullanıcı sonradan elle değiştirdiyse dokunmaz.
  const clearPrefilled = () => {
    prefillRequest.current += 1;
    const { tags: addedTags = [], ...fields } = prefilled.current;
    const cleared = Object.entries(fields).filter(([field, value]) => getValues(field) === value);
    cleared.forEach(([field]) => setValue(field, '', { shouldDirty: true }));
    // Etiketlerden yalnızca CV'den eklenenler çıkar; kullanıcının yazdıkları kalır.
    if (addedTags.length > 0) {
      const remaining = getValues('tags').filter((tag) => !addedTags.includes(tag));
      setValue('tags', remaining, { shouldDirty: true });
    }
    prefilled.current = {};
    if (cleared.length > 0) checkWarnings();
  };

  const removeFile = () => {
    setFile(null);
    clearPrefilled();
  };

  // Seçilen CV'den okunabilen bilgiler formun boş alanlarına yazılır. Okunamazsa hiçbir şey
  // söylenmez; kullanıcı alanları elle doldurur.
  const selectFile = async (selected) => {
    setFile(selected);
    clearPrefilled();
    if (!/\.(pdf|docx?)$/i.test(selected.name)) return;
    const request = prefillRequest.current;
    try {
      const profile = await prefillFromCv(selected);
      // Yanıt beklenirken CV kaldırıldıysa veya değiştirildiyse sonuç yok sayılır.
      if (request !== prefillRequest.current) return;
      const { tags: addedTags, ...fields } = cvPrefillPatch(getValues(), profile);
      Object.entries(fields).forEach(([field, value]) =>
        setValue(field, value, { shouldDirty: true }),
      );
      if (addedTags) setValue('tags', [...getValues('tags'), ...addedTags], { shouldDirty: true });
      prefilled.current = addedTags ? { ...fields, tags: addedTags } : fields;
      if (Object.keys(fields).length > 0) checkWarnings();
    } catch {
      // Bilgi okunamadı; önemli değil.
    }
  };

  const hasWarnings = duplicates.length > 0 || blacklistMatches.length > 0;

  const save = async (values) => {
    let id = candidate?.id;
    try {
      // Uyarı ekrandaysa kullanıcı görüp devam ediyor demektir.
      const input = toCandidateInput(values, {
        confirmDuplicate: duplicates.length > 0,
        confirmBlacklist: blacklistMatches.length > 0,
      });
      if (isEdit) await updateCandidate.mutateAsync({ id, input });
      else id = await createCandidate.mutateAsync(input);
    } catch (error) {
      const problem = error.response?.data;
      if (problem?.code === DUPLICATE_CODE) setDuplicates(problem.duplicates);
      else if (problem?.code === BLACKLIST_CODE) setBlacklistMatches(problem.blacklistMatches);
      else if (!applyFieldErrors(error, setError)) toast.error(getErrorMessage(error));
      return;
    }

    if (file) {
      try {
        await uploadDocument.mutateAsync({ id, file, typeId: cvTypeId });
      } catch (error) {
        toast.error(`Aday eklendi ama CV yüklenemedi. ${getErrorMessage(error)}`);
        onSaved(id);
        return;
      }
    }

    toast.success(isEdit ? 'Aday bilgileri güncellendi.' : 'Aday eklendi.');
    onSaved(id);
  };

  const plainLabel = isEdit ? 'Kaydet' : 'Aday ekle';
  const confirmLabel = isEdit ? 'Yine de kaydet' : 'Yine de ekle';

  const footer = (
    <div className="dialog-actions">
      <Button type="button" label="Vazgeç" text severity="secondary" onClick={onClose} />
      <Button
        type="submit"
        form="candidate-form"
        label={hasWarnings ? confirmLabel : plainLabel}
        loading={isSubmitting}
      />
    </div>
  );

  return (
    <Dialog
      visible
      header={isEdit ? 'Aday bilgilerini düzenle' : 'Aday ekle'}
      footer={footer}
      onHide={onClose}
      className="form-dialog"
      modal
    >
      <form id="candidate-form" className="form-grid" onSubmit={handleSubmit(save)} noValidate>
        {hasWarnings && (
          <div className="form-grid__wide warning-stack">
            <BlacklistWarning matches={blacklistMatches} />
            <DuplicateWarning duplicates={duplicates} />
          </div>
        )}

        <FormField label="Ad" htmlFor="firstName" error={errors.firstName?.message}>
          <InputText
            id="firstName"
            invalid={Boolean(errors.firstName)}
            {...register('firstName', { onBlur: checkWarnings })}
          />
        </FormField>
        <FormField label="Soyad" htmlFor="lastName" error={errors.lastName?.message}>
          <InputText
            id="lastName"
            invalid={Boolean(errors.lastName)}
            {...register('lastName', { onBlur: checkWarnings })}
          />
        </FormField>

        <FormField label="E-posta" htmlFor="email" error={errors.email?.message}>
          <InputText
            id="email"
            type="email"
            autoComplete="off"
            invalid={Boolean(errors.email)}
            {...register('email', { onBlur: checkWarnings })}
          />
        </FormField>
        <FormField label="Telefon" htmlFor="phone" error={errors.phone?.message}>
          <InputText
            id="phone"
            type="tel"
            autoComplete="off"
            placeholder="05xx xxx xx xx"
            invalid={Boolean(errors.phone)}
            {...register('phone', { onBlur: checkWarnings })}
          />
        </FormField>

        <>
          <FormField label="Şehir" htmlFor="city" error={errors.city?.message}>
            <InputText id="city" invalid={Boolean(errors.city)} {...register('city')} />
          </FormField>
          <FormField label="Kaynak" htmlFor="sourceId" error={errors.sourceId?.message}>
            <Controller
              name="sourceId"
              control={control}
              render={({ field }) => (
                <DictionarySelect
                  type="CandidateSource"
                  inputId="sourceId"
                  value={field.value}
                  onChange={field.onChange}
                  placeholder="Adaya nereden ulaşıldı?"
                  invalid={Boolean(errors.sourceId)}
                  showClear
                />
              )}
            />
          </FormField>
          <FormField label="LinkedIn" htmlFor="linkedInUrl" error={errors.linkedInUrl?.message}>
            <InputText
              id="linkedInUrl"
              placeholder="https://www.linkedin.com/in/…"
              invalid={Boolean(errors.linkedInUrl)}
              {...register('linkedInUrl')}
            />
          </FormField>
          <FormField label="GitHub" htmlFor="gitHubUrl" error={errors.gitHubUrl?.message}>
            <InputText
              id="gitHubUrl"
              placeholder="https://github.com/…"
              invalid={Boolean(errors.gitHubUrl)}
              {...register('gitHubUrl')}
            />
          </FormField>
          <div className="form-grid__wide">
            <FormField label="Etiketler" htmlFor="tags" error={errors.tags?.message}>
              <Controller
                name="tags"
                control={control}
                render={({ field }) => (
                  <Chips
                    inputId="tags"
                    value={field.value}
                    onChange={(event) => field.onChange(event.value ?? [])}
                    separator=","
                    addOnBlur
                    placeholder={
                      field.value.length === 0 ? 'Yazıp Enter’a basın (örn. React, Senior)' : ''
                    }
                  />
                )}
              />
            </FormField>
          </div>
          <div className="form-grid__wide">
            <FormField label="Notlar" htmlFor="notes" error={errors.notes?.message}>
              <InputTextarea
                id="notes"
                rows={3}
                autoResize
                invalid={Boolean(errors.notes)}
                {...register('notes')}
              />
            </FormField>
          </div>
        </>

        {!isEdit && (
          <div className="form-grid__wide">
            <span className="form-field__label">CV</span>
            {file ? (
              <div className="selected-file">
                <i className="pi pi-file" aria-hidden="true" />
                <span>
                  {file.name} <span className="muted">· {formatFileSize(file.size)}</span>
                </span>
                <Button
                  type="button"
                  icon="pi pi-times"
                  text
                  severity="secondary"
                  aria-label="Seçilen dosyayı kaldır"
                  onClick={removeFile}
                />
              </div>
            ) : (
              <FileDropzone
                onSelect={selectFile}
                label="CV eklemek için tıklayın veya dosyayı buraya sürükleyin"
              />
            )}
            <p className="cell-sub">
              Ad ve soyad zorunlu; e-posta veya telefondan biri yeterli. Ön görüşme notlarını aday
              kartından ekleyebilirsiniz.
            </p>
          </div>
        )}
      </form>
    </Dialog>
  );
}
