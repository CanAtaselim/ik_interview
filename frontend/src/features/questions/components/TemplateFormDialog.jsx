import { Button } from 'primereact/button';
import { Dialog } from 'primereact/dialog';
import { IconField } from 'primereact/iconfield';
import { InputIcon } from 'primereact/inputicon';
import { InputText } from 'primereact/inputtext';
import { useState } from 'react';
import { HR_ROLES } from '@/shared/auth/roles';
import { useAuth } from '@/shared/auth/useAuth';
import DictionarySelect from '@/shared/components/DictionarySelect';
import FormField from '@/shared/components/FormField';
import { useToast } from '@/shared/components/toast';
import { DIFFICULTY_LABELS, QUESTION_TYPE_LABELS } from '@/shared/constants/labels';
import { useDebounce } from '@/shared/hooks/useDebounce';
import { getErrorMessage } from '@/shared/utils/errors';
import {
  useCreateInterviewTemplate,
  useInterviewTemplate,
  useQuestions,
  useSetInterviewTemplateActive,
  useUpdateInterviewTemplate,
} from '../queries';

const PICKER_SIZE = 50;

const questionMeta = (question) =>
  [
    question.competencyName,
    QUESTION_TYPE_LABELS[question.type],
    DIFFICULTY_LABELS[question.difficulty],
    `ağırlık ${question.weight.toLocaleString('tr-TR')}`,
  ].join(' · ');

/**
 * Şablon ekleme ve düzenleme: ad, birim, isteğe bağlı mülakat tipi ve seviye, sıralı sorular.
 * Sorular aynı pencerede bankadan aranıp eklenir; sırası oklarla değiştirilir.
 * @param {{ templateId?: string | null, onClose: () => void }} props
 */
export default function TemplateFormDialog({ templateId = null, onClose }) {
  const toast = useToast();
  const { user, hasAnyRole } = useAuth();
  const isHr = hasAnyRole(HR_ROLES);
  const isEdit = Boolean(templateId);
  const { data: detail } = useInterviewTemplate(templateId);
  const createTemplate = useCreateInterviewTemplate();
  const updateTemplate = useUpdateInterviewTemplate();
  const setActive = useSetInterviewTemplateActive();
  const [form, setForm] = useState(
    isEdit
      ? null
      : {
          name: '',
          departmentId: isHr ? null : user.departmentId,
          levelId: null,
          interviewTypeId: null,
          questions: [],
        },
  );
  const [errors, setErrors] = useState({});
  const [searchText, setSearchText] = useState('');
  const search = useDebounce(searchText);

  if (isEdit && !form && detail) {
    setForm({
      name: detail.template.name,
      departmentId: detail.template.departmentId,
      levelId: detail.template.levelId,
      interviewTypeId: detail.template.interviewTypeId,
      questions: detail.questions,
    });
  }

  const template = detail?.template;
  const readOnly = isEdit && template && !template.canManage;
  const { data: available } = useQuestions({
    departmentId: form?.departmentId ?? undefined,
    isActive: true,
    search: search || undefined,
    pageSize: PICKER_SIZE,
  });
  const selectedIds = new Set(form?.questions.map((question) => question.id));
  const candidates = (available?.items ?? []).filter((question) => !selectedIds.has(question.id));

  const set = (field) => (value) => {
    setForm((current) => ({ ...current, [field]: value }));
    setErrors((current) => ({ ...current, [field]: undefined }));
  };
  const setQuestions = (update) => {
    setForm((current) => ({ ...current, questions: update(current.questions) }));
    setErrors((current) => ({ ...current, questionIds: undefined }));
  };
  const move = (index, offset) =>
    setQuestions((questions) => {
      const next = [...questions];
      [next[index], next[index + offset]] = [next[index + offset], next[index]];
      return next;
    });

  const submit = () => {
    const found = {};
    if (!form.name.trim()) found.name = 'Şablonun adını yazmalısınız.';
    if (!form.departmentId) found.departmentId = 'Şablonun ait olduğu birimi seçmelisiniz.';
    if (form.questions.length === 0) found.questionIds = 'Şablona en az bir soru eklemelisiniz.';
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    const input = {
      name: form.name.trim(),
      departmentId: form.departmentId,
      levelId: form.levelId,
      interviewTypeId: form.interviewTypeId,
      questionIds: form.questions.map((question) => question.id),
    };
    const options = {
      onSuccess: () => {
        toast.success(isEdit ? 'Şablon kaydedildi.' : 'Şablon oluşturuldu.');
        onClose();
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
    };

    if (isEdit) updateTemplate.mutate({ id: templateId, ...input }, options);
    else createTemplate.mutate(input, options);
  };

  const footer = !readOnly && (
    <div className="dialog-actions">
      {isEdit && template && (
        <div className="dialog-actions__start">
          <Button
            type="button"
            label={template.isActive ? 'Pasif yap' : 'Aktif yap'}
            severity="secondary"
            text
            loading={setActive.isPending}
            onClick={() =>
              setActive.mutate(
                { id: templateId, isActive: !template.isActive },
                {
                  onSuccess: () =>
                    toast.success(
                      template.isActive ? 'Şablon pasif yapıldı.' : 'Şablon yeniden aktif.',
                    ),
                  onError: (error) => toast.error(getErrorMessage(error)),
                },
              )
            }
          />
        </div>
      )}
      <Button type="button" label="Vazgeç" text severity="secondary" onClick={onClose} />
      <Button
        type="button"
        label="Kaydet"
        disabled={!form}
        loading={createTemplate.isPending || updateTemplate.isPending}
        onClick={submit}
      />
    </div>
  );

  return (
    <Dialog
      visible
      header={
        isEdit
          ? `Şablon${template && !template.isActive ? ' (pasif)' : ''}`
          : 'Yeni mülakat şablonu'
      }
      footer={footer}
      onHide={onClose}
      className="form-dialog form-dialog--wide"
      modal
    >
      {!form && <p className="muted">Yükleniyor…</p>}
      {form && (
        <fieldset className="form-grid plain-fieldset" disabled={readOnly}>
          <div className="form-grid__wide">
            <FormField label="Ad" htmlFor="template-name" error={errors.name}>
              <InputText
                id="template-name"
                value={form.name}
                maxLength={200}
                placeholder="Ör. Backend teknik mülakat"
                invalid={Boolean(errors.name)}
                onChange={(event) => set('name')(event.target.value)}
              />
            </FormField>
          </div>

          {isHr ? (
            <FormField label="Birim" htmlFor="template-department" error={errors.departmentId}>
              <DictionarySelect
                type="Department"
                inputId="template-department"
                value={form.departmentId}
                invalid={Boolean(errors.departmentId)}
                onChange={set('departmentId')}
              />
            </FormField>
          ) : (
            <FormField label="Birim" htmlFor="template-department-fixed">
              <InputText
                id="template-department-fixed"
                value={template?.departmentName ?? user.department ?? ''}
                disabled
              />
            </FormField>
          )}

          <FormField label="Mülakat tipi (isteğe bağlı)" htmlFor="template-type">
            <DictionarySelect
              type="InterviewType"
              inputId="template-type"
              value={form.interviewTypeId}
              showClear
              placeholder="Tüm tipler"
              onChange={set('interviewTypeId')}
            />
          </FormField>

          <FormField label="Seviye (isteğe bağlı)" htmlFor="template-level">
            <DictionarySelect
              type="Level"
              inputId="template-level"
              value={form.levelId}
              showClear
              placeholder="Tüm seviyeler"
              onChange={set('levelId')}
            />
          </FormField>

          <section className="form-grid__wide" aria-labelledby="template-selected">
            <h3 id="template-selected" className="section-title">
              Sorular ({form.questions.length})
            </h3>
            {errors.questionIds && (
              <small className="form-field__error" role="alert">
                {errors.questionIds}
              </small>
            )}
            {form.questions.length === 0 ? (
              <p className="muted">Aşağıdaki listeden soru ekleyin.</p>
            ) : (
              <ol className="template-questions">
                {form.questions.map((question, index) => (
                  <li key={question.id} className="template-questions__item">
                    <div className="template-questions__main">
                      {question.text}
                      <div className="cell-sub">
                        {questionMeta(question)}
                        {!question.isActive && ' · pasif'}
                      </div>
                    </div>
                    {!readOnly && (
                      <div className="template-questions__actions">
                        <Button
                          icon="pi pi-arrow-up"
                          text
                          severity="secondary"
                          aria-label="Yukarı taşı"
                          disabled={index === 0}
                          onClick={() => move(index, -1)}
                        />
                        <Button
                          icon="pi pi-arrow-down"
                          text
                          severity="secondary"
                          aria-label="Aşağı taşı"
                          disabled={index === form.questions.length - 1}
                          onClick={() => move(index, 1)}
                        />
                        <Button
                          icon="pi pi-times"
                          text
                          severity="secondary"
                          aria-label="Şablondan çıkar"
                          onClick={() =>
                            setQuestions((questions) =>
                              questions.filter((item) => item.id !== question.id),
                            )
                          }
                        />
                      </div>
                    )}
                  </li>
                ))}
              </ol>
            )}
          </section>

          {!readOnly && (
            <section className="form-grid__wide" aria-labelledby="template-bank">
              <h3 id="template-bank" className="section-title">
                Bankadan soru ekle
              </h3>
              {!form.departmentId ? (
                <p className="muted">Önce birimi seçin.</p>
              ) : (
                <>
                  <IconField iconPosition="left" className="template-search">
                    <InputIcon className="pi pi-search" />
                    <InputText
                      value={searchText}
                      onChange={(event) => setSearchText(event.target.value)}
                      placeholder="Soru metninde ara"
                      aria-label="Eklenecek soruyu ara"
                    />
                  </IconField>
                  <ul className="template-questions template-questions__picker">
                    {candidates.length === 0 && (
                      <li className="muted">
                        Eklenebilecek aktif soru yok. Soru bankasından yeni soru ekleyebilirsiniz.
                      </li>
                    )}
                    {candidates.map((question) => (
                      <li key={question.id} className="template-questions__item">
                        <div className="template-questions__main">
                          {question.text}
                          <div className="cell-sub">{questionMeta(question)}</div>
                        </div>
                        <Button
                          label="Ekle"
                          icon="pi pi-plus"
                          text
                          onClick={() => setQuestions((questions) => [...questions, question])}
                        />
                      </li>
                    ))}
                  </ul>
                </>
              )}
            </section>
          )}
        </fieldset>
      )}
    </Dialog>
  );
}
