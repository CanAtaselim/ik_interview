import { Button } from 'primereact/button';
import { confirmDialog } from 'primereact/confirmdialog';
import { Dialog } from 'primereact/dialog';
import { InputNumber } from 'primereact/inputnumber';
import { InputText } from 'primereact/inputtext';
import { InputTextarea } from 'primereact/inputtextarea';
import { SelectButton } from 'primereact/selectbutton';
import { useState } from 'react';
import { HR_ROLES } from '@/shared/auth/roles';
import { useAuth } from '@/shared/auth/useAuth';
import DictionarySelect from '@/shared/components/DictionarySelect';
import FormField from '@/shared/components/FormField';
import { useToast } from '@/shared/components/toast';
import { DIFFICULTY_LABELS, QUESTION_TYPE_LABELS, toOptions } from '@/shared/constants/labels';
import { getErrorMessage } from '@/shared/utils/errors';
import { formatDateTime } from '@/shared/utils/format';
import {
  useCreateQuestion,
  useDeleteQuestion,
  useQuestion,
  useSetQuestionActive,
  useUpdateQuestion,
} from '../queries';

const TYPE_OPTIONS = toOptions(QUESTION_TYPE_LABELS);
const DIFFICULTY_OPTIONS = Object.entries(DIFFICULTY_LABELS).map(([value, label]) => ({
  value: Number(value),
  label,
}));

const empty = (departmentId) => ({
  departmentId,
  competencyId: null,
  type: 'Technical',
  difficulty: 2,
  weight: 1,
  levelId: null,
  positionTitle: '',
  text: '',
  guidance: '',
});

/**
 * Soru ekleme ve düzenleme. Düzenlemede içerik değişirse yeni sürüm oluşur; eski sürümler pencerenin
 * altında görünür. Şablonda kullanılan soru silinemez, pasif yapılır.
 * @param {{ questionId?: string | null, onClose: () => void }} props
 */
export default function QuestionFormDialog({ questionId = null, onClose }) {
  const toast = useToast();
  const { user, hasAnyRole } = useAuth();
  const isHr = hasAnyRole(HR_ROLES);
  const isEdit = Boolean(questionId);
  const { data: detail, isLoading } = useQuestion(questionId);
  const createQuestion = useCreateQuestion();
  const updateQuestion = useUpdateQuestion();
  const setActive = useSetQuestionActive();
  const deleteQuestion = useDeleteQuestion();
  const [form, setForm] = useState(isEdit ? null : empty(isHr ? null : user.departmentId));
  const [errors, setErrors] = useState({});

  // Düzenlenecek soru yüklenince form bir kez doldurulur.
  if (isEdit && !form && detail) {
    const question = detail.question;
    setForm({
      departmentId: question.departmentId,
      competencyId: question.competencyId,
      type: question.type,
      difficulty: question.difficulty,
      weight: question.weight,
      levelId: question.levelId,
      positionTitle: question.positionTitle ?? '',
      text: question.text,
      guidance: question.guidance ?? '',
    });
  }

  const question = detail?.question;
  const readOnly = isEdit && question && !question.canManage;
  const set = (field) => (value) => {
    setForm((current) => ({ ...current, [field]: value }));
    setErrors((current) => ({ ...current, [field]: undefined }));
  };

  const submit = () => {
    const found = {};
    if (!form.departmentId) found.departmentId = 'Sorunun ait olduğu birimi seçmelisiniz.';
    if (!form.competencyId) found.competencyId = 'Sorunun ölçtüğü yetkinliği seçmelisiniz.';
    if (!form.text.trim()) found.text = 'Soru metnini yazmalısınız.';
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    const input = {
      ...form,
      positionTitle: form.positionTitle.trim() || null,
      text: form.text.trim(),
      guidance: form.guidance.trim() || null,
    };
    const options = {
      onSuccess: () => {
        toast.success(isEdit ? 'Soru kaydedildi.' : 'Soru bankaya eklendi.');
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

    if (isEdit) updateQuestion.mutate({ id: questionId, ...input }, options);
    else createQuestion.mutate(input, options);
  };

  const toggleActive = () =>
    setActive.mutate(
      { id: questionId, isActive: !question.isActive },
      {
        onSuccess: () =>
          toast.success(question.isActive ? 'Soru pasif yapıldı.' : 'Soru yeniden aktif.'),
        onError: (error) => toast.error(getErrorMessage(error)),
      },
    );

  const confirmDelete = () =>
    confirmDialog({
      header: 'Soru silinsin mi?',
      message: 'Soru bankadan kaldırılacak. Bu soru hiçbir şablonda kullanılmıyor.',
      acceptLabel: 'Sil',
      rejectLabel: 'Vazgeç',
      acceptClassName: 'p-button-danger',
      accept: () =>
        deleteQuestion.mutate(questionId, {
          onSuccess: () => {
            toast.success('Soru silindi.');
            onClose();
          },
          onError: (error) => toast.error(getErrorMessage(error)),
        }),
    });

  const footer = !readOnly && (
    <div className="dialog-actions">
      {isEdit && question && (
        <div className="dialog-actions__start">
          {question.templateCount === 0 && (
            <Button type="button" label="Sil" severity="danger" text onClick={confirmDelete} />
          )}
          <Button
            type="button"
            label={question.isActive ? 'Pasif yap' : 'Aktif yap'}
            severity="secondary"
            text
            loading={setActive.isPending}
            onClick={toggleActive}
          />
        </div>
      )}
      <Button type="button" label="Vazgeç" text severity="secondary" onClick={onClose} />
      <Button
        type="button"
        label="Kaydet"
        loading={createQuestion.isPending || updateQuestion.isPending}
        disabled={!form}
        onClick={submit}
      />
    </div>
  );

  const header = isEdit
    ? `Soru${question ? ` · sürüm ${question.version}` : ''}${question && !question.isActive ? ' (pasif)' : ''}`
    : 'Yeni soru';

  return (
    <Dialog visible header={header} footer={footer} onHide={onClose} className="form-dialog" modal>
      {(isLoading || !form) && <p className="muted">Yükleniyor…</p>}
      {form && (
        <fieldset className="form-grid plain-fieldset" disabled={readOnly}>
          {isHr ? (
            <FormField label="Birim" htmlFor="question-department" error={errors.departmentId}>
              <DictionarySelect
                type="Department"
                inputId="question-department"
                value={form.departmentId}
                invalid={Boolean(errors.departmentId)}
                onChange={set('departmentId')}
              />
            </FormField>
          ) : (
            <FormField label="Birim" htmlFor="question-department-fixed">
              <InputText
                id="question-department-fixed"
                value={question?.departmentName ?? user.department ?? ''}
                disabled
              />
            </FormField>
          )}

          <FormField label="Yetkinlik" htmlFor="question-competency" error={errors.competencyId}>
            <DictionarySelect
              type="Competency"
              inputId="question-competency"
              value={form.competencyId}
              invalid={Boolean(errors.competencyId)}
              onChange={set('competencyId')}
            />
          </FormField>

          <FormField label="Tip" htmlFor="question-type">
            <SelectButton
              id="question-type"
              value={form.type}
              options={TYPE_OPTIONS}
              allowEmpty={false}
              onChange={(event) => set('type')(event.value)}
            />
          </FormField>

          <FormField label="Zorluk" htmlFor="question-difficulty">
            <SelectButton
              id="question-difficulty"
              value={form.difficulty}
              options={DIFFICULTY_OPTIONS}
              allowEmpty={false}
              onChange={(event) => set('difficulty')(event.value)}
            />
          </FormField>

          <FormField label="Ağırlık" htmlFor="question-weight" error={errors.weight}>
            <InputNumber
              inputId="question-weight"
              value={form.weight}
              onValueChange={(event) => set('weight')(event.value ?? 1)}
              min={0.5}
              max={5}
              step={0.5}
              minFractionDigits={0}
              maxFractionDigits={2}
              showButtons
              locale="tr-TR"
            />
          </FormField>

          <FormField label="Seviye (isteğe bağlı)" htmlFor="question-level">
            <DictionarySelect
              type="Level"
              inputId="question-level"
              value={form.levelId}
              showClear
              placeholder="Tüm seviyeler"
              onChange={set('levelId')}
            />
          </FormField>

          <div className="form-grid__wide">
            <FormField label="Pozisyon (isteğe bağlı)" htmlFor="question-position">
              <InputText
                id="question-position"
                value={form.positionTitle}
                maxLength={200}
                placeholder="Ör. Backend Geliştirici; boşsa birimin tüm pozisyonları için"
                onChange={(event) => set('positionTitle')(event.target.value)}
              />
            </FormField>
          </div>

          <div className="form-grid__wide">
            <FormField label="Soru" htmlFor="question-text" error={errors.text}>
              <InputTextarea
                id="question-text"
                rows={3}
                autoResize
                maxLength={2000}
                value={form.text}
                invalid={Boolean(errors.text)}
                onChange={(event) => set('text')(event.target.value)}
              />
            </FormField>
          </div>

          <div className="form-grid__wide">
            <FormField label="Beklenen cevap / ipuçları (isteğe bağlı)" htmlFor="question-guidance">
              <InputTextarea
                id="question-guidance"
                rows={3}
                autoResize
                maxLength={4000}
                value={form.guidance}
                placeholder="Mülakatçı mülakat sırasında görür; adaya gösterilmez."
                onChange={(event) => set('guidance')(event.target.value)}
              />
            </FormField>
          </div>

          {isEdit && question && (
            <p className="muted form-grid__wide">
              {question.templateCount > 0
                ? `Bu soru ${question.templateCount} şablonda kullanılıyor; silinemez, pasif yapılabilir. `
                : ''}
              Metni değiştirdiğinizde yeni sürüm oluşur; yapılmış mülakatlar sorunun o anki halini
              saklar.
            </p>
          )}

          {detail?.versions.length > 1 && (
            <details className="form-grid__wide">
              <summary>Sürüm geçmişi ({detail.versions.length})</summary>
              <ul className="version-list">
                {detail.versions.map((version) => (
                  <li key={version.number}>
                    <strong>Sürüm {version.number}</strong>
                    <span className="cell-sub">
                      {' '}
                      · {version.createdByName} · {formatDateTime(version.createdAt)}
                    </span>
                    <p className="multiline">{version.text}</p>
                  </li>
                ))}
              </ul>
            </details>
          )}
        </fieldset>
      )}
    </Dialog>
  );
}
