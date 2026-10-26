import { Button } from 'primereact/button';
import { InputSwitch } from 'primereact/inputswitch';
import { InputText } from 'primereact/inputtext';
import { useState } from 'react';
import PageHeader from '@/shared/components/PageHeader';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { useToast } from '@/shared/components/toast';
import { STAGE_KIND_LABELS } from '@/shared/constants/labels';
import { getErrorMessage } from '@/shared/utils/errors';
import AdminNav from '../components/AdminNav';
import { usePipelineStages, useUpdatePipelineStages } from '../queries';
import '../admin.css';

/**
 * Süreç aşamaları: adlarını değiştirebilir, atlanabilir olanları belirleyebilirsiniz. Sıra ve
 * türler sabittir çünkü geçiş kuralları (CV kararı, mülakat değerlendirmeleri, işe alım) aşamanın
 * türüne bağlıdır.
 */
export default function PipelineStagesPage() {
  const { data, isLoading } = usePipelineStages();
  if (isLoading || !data) return <TableSkeleton rows={6} />;

  return (
    <>
      <AdminNav />
      <PageHeader
        title="Süreç aşamaları"
        subtitle="Adayların pozisyonlarda izlediği aşamalar. Adları değiştirebilirsiniz; sıra ve türler sabittir."
      />
      <StageEditor
        key={data.stages.map((stage) => `${stage.id}${stage.name}${stage.isOptional}`).join()}
        settings={data}
      />
    </>
  );
}

function StageEditor({ settings }) {
  const toast = useToast();
  const update = useUpdatePipelineStages();
  const [stages, setStages] = useState(settings.stages);
  const dirty = JSON.stringify(stages) !== JSON.stringify(settings.stages);
  const names = stages.map((stage) => stage.name.trim().toLocaleLowerCase('tr'));
  const hasProblem = names.some((name) => !name) || new Set(names).size !== names.length;

  const change = (id, changes) =>
    setStages((current) =>
      current.map((stage) => (stage.id === id ? { ...stage, ...changes } : stage)),
    );

  const save = () =>
    update.mutate(
      stages.map(({ id, name, isOptional }) => ({ id, name: name.trim(), isOptional })),
      {
        onSuccess: () => toast.success('Süreç aşamaları kaydedildi.'),
        onError: (error) => toast.error(getErrorMessage(error)),
      },
    );

  return (
    <section className="card card--padded settings-card" aria-labelledby="stages-title">
      <h2 id="stages-title" className="section-title">
        {settings.name}
      </h2>
      <div className="table-scroll">
        <table className="stage-table">
          <thead>
            <tr>
              <th scope="col">Sıra</th>
              <th scope="col">Aşama adı</th>
              <th scope="col">Davranışı</th>
              <th scope="col">Atlanabilir</th>
              <th scope="col">Şu an</th>
            </tr>
          </thead>
          <tbody>
            {stages.map((stage) => (
              <tr key={stage.id}>
                <td>{stage.order}</td>
                <td>
                  <InputText
                    aria-label={`${stage.order}. aşamanın adı`}
                    value={stage.name}
                    maxLength={100}
                    invalid={!stage.name.trim()}
                    onChange={(event) => change(stage.id, { name: event.target.value })}
                  />
                </td>
                <td className="cell-sub">{STAGE_KIND_LABELS[stage.kind]}</td>
                <td>
                  <InputSwitch
                    inputId={`optional-${stage.id}`}
                    aria-label={`${stage.name} aşaması atlanabilir`}
                    checked={stage.isOptional}
                    disabled={!stage.canBeOptional}
                    title={
                      stage.canBeOptional
                        ? undefined
                        : 'Bu aşamanın çıkışında uygulanan bir kural var; atlanamaz.'
                    }
                    onChange={(event) => change(stage.id, { isOptional: event.value })}
                  />
                </td>
                <td>{stage.activeCount > 0 ? `${stage.activeCount} aday` : '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {hasProblem && (
        <small className="form-field__error" role="alert">
          Her aşamanın adı dolu ve diğerlerinden farklı olmalı.
        </small>
      )}
      <div className="dialog-actions">
        <Button
          type="button"
          label="Kaydet"
          disabled={!dirty || hasProblem}
          loading={update.isPending}
          onClick={save}
        />
      </div>
    </section>
  );
}
