import { Button } from 'primereact/button';
import { confirmDialog } from 'primereact/confirmdialog';
import { Dialog } from 'primereact/dialog';
import { InputText } from 'primereact/inputtext';
import { InputTextarea } from 'primereact/inputtextarea';
import { Tag } from 'primereact/tag';
import { useRef, useState } from 'react';
import { useSearchParams } from 'react-router';
import FormField from '@/shared/components/FormField';
import PageHeader from '@/shared/components/PageHeader';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { useToast } from '@/shared/components/toast';
import { getErrorMessage } from '@/shared/utils/errors';
import AdminNav from '../components/AdminNav';
import {
  useEmailTemplates,
  usePreviewEmailTemplate,
  useResetEmailTemplate,
  useUpdateEmailTemplate,
} from '../queries';
import '../admin.css';

/**
 * E-posta şablonları: sistemin gönderdiği e-postaların konusu ve içeriği. Metindeki {Alan}'lar
 * gönderimde gerçek değerle dolar; yalnızca şablonun tanımlı alanları kullanılabilir.
 */
export default function EmailTemplatesPage() {
  const [params, setParams] = useSearchParams();
  const { data: templates = [], isLoading } = useEmailTemplates();

  if (isLoading) return <TableSkeleton rows={4} />;

  const selected =
    templates.find((template) => template.code === params.get('template')) ?? templates[0];

  return (
    <>
      <AdminNav />
      <PageHeader
        title="E-posta şablonları"
        subtitle="Sistemin gönderdiği e-postaların metinleri. Değişiklik bundan sonra gönderilen e-postalarda geçerli olur."
      />
      <div className="dictionaries">
        <nav className="card dictionaries__types" aria-label="Şablonlar">
          {templates.map((template) => (
            <button
              key={template.id}
              type="button"
              className="dictionaries__type"
              aria-current={template.id === selected?.id ? 'page' : undefined}
              onClick={() => setParams({ template: template.code })}
            >
              {template.name}
              {template.isModified && (
                <>
                  {' '}
                  <Tag value="Değiştirildi" severity="info" />
                </>
              )}
            </button>
          ))}
        </nav>
        {/* Kaydetme ve varsayılana dönme sonrası form sunucudaki metinle yeniden kurulur. */}
        {selected && (
          <TemplateEditor
            key={`${selected.id}|${selected.subject}|${selected.body}`}
            template={selected}
          />
        )}
      </div>
    </>
  );
}

function TemplateEditor({ template }) {
  const toast = useToast();
  const update = useUpdateEmailTemplate();
  const reset = useResetEmailTemplate();
  const preview = usePreviewEmailTemplate();
  const bodyRef = useRef(null);
  const [subject, setSubject] = useState(template.subject);
  const [body, setBody] = useState(template.body);
  const [errors, setErrors] = useState({});
  const [previewing, setPreviewing] = useState(null);
  const variables = template.variables.match(/\{\w+\}/g) ?? [];
  const dirty = subject !== template.subject || body !== template.body;

  // Alan, gövdede imlecin olduğu yere eklenir.
  const insert = (variable) => {
    const field = bodyRef.current;
    const start = field?.selectionStart ?? body.length;
    const end = field?.selectionEnd ?? body.length;
    setBody(`${body.slice(0, start)}${variable}${body.slice(end)}`);
    requestAnimationFrame(() => {
      field?.focus();
      field?.setSelectionRange(start + variable.length, start + variable.length);
    });
  };

  const save = () =>
    update.mutate(
      { id: template.id, subject, body },
      {
        onSuccess: () => {
          setErrors({});
          toast.success('Şablon kaydedildi.');
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

  const showPreview = () =>
    preview.mutate(
      { id: template.id, subject, body },
      { onSuccess: setPreviewing, onError: (error) => toast.error(getErrorMessage(error)) },
    );

  const confirmReset = () =>
    confirmDialog({
      header: 'Varsayılan metne dönülsün mü?',
      message: 'Bu şablondaki değişiklikleriniz silinir ve sistemin özgün metni geri gelir.',
      acceptLabel: 'Varsayılana dön',
      rejectLabel: 'Vazgeç',
      acceptClassName: 'p-button-danger',
      accept: () =>
        reset.mutate(template.id, {
          onSuccess: () => toast.success('Şablon varsayılan metne döndürüldü.'),
          onError: (error) => toast.error(getErrorMessage(error)),
        }),
    });

  return (
    <section className="card card--padded template-editor" aria-labelledby="template-title">
      <h2 id="template-title" className="section-title">
        {template.name}
      </h2>
      <FormField label="Konu" htmlFor="template-subject" error={errors.subject}>
        <InputText
          id="template-subject"
          value={subject}
          maxLength={300}
          invalid={Boolean(errors.subject)}
          onChange={(event) => {
            setSubject(event.target.value);
            setErrors((current) => ({ ...current, subject: undefined }));
          }}
        />
      </FormField>
      <FormField label="İçerik (HTML)" htmlFor="template-body" error={errors.body}>
        <InputTextarea
          id="template-body"
          ref={bodyRef}
          rows={12}
          autoResize
          className="template-editor__body"
          value={body}
          invalid={Boolean(errors.body)}
          onChange={(event) => {
            setBody(event.target.value);
            setErrors((current) => ({ ...current, body: undefined }));
          }}
        />
      </FormField>
      <div className="template-editor__variables">
        <span className="cell-sub">Eklenebilecek alanlar (içeriğe eklemek için tıklayın):</span>
        <div className="tag-list">
          {variables.map((variable) => (
            <Button
              key={variable}
              type="button"
              label={variable}
              size="small"
              outlined
              severity="secondary"
              onClick={() => insert(variable)}
            />
          ))}
        </div>
      </div>
      <div className="dialog-actions template-editor__actions">
        <div className="dialog-actions__start">
          {template.isModified && (
            <Button
              type="button"
              label="Varsayılana dön"
              text
              severity="secondary"
              onClick={confirmReset}
            />
          )}
        </div>
        <Button
          type="button"
          label="Önizle"
          icon="pi pi-eye"
          outlined
          loading={preview.isPending}
          onClick={showPreview}
        />
        <Button
          type="button"
          label="Kaydet"
          disabled={!dirty}
          loading={update.isPending}
          onClick={save}
        />
      </div>

      {previewing && (
        <Dialog
          visible
          header="Önizleme (örnek verilerle)"
          onHide={() => setPreviewing(null)}
          className="form-dialog form-dialog--wide"
          modal
        >
          <p>
            <strong>Konu:</strong> {previewing.subject}
          </p>
          {/* E-posta HTML'i uygulamanın stilini ve betiğini etkilemesin diye kum havuzlu çerçevede gösterilir. */}
          <iframe
            title="E-posta önizlemesi"
            className="email-preview"
            sandbox=""
            srcDoc={previewing.html}
          />
        </Dialog>
      )}
    </section>
  );
}
