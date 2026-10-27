import { Button } from 'primereact/button';
import { useMemo, useState } from 'react';
import { useSearchParams } from 'react-router';
import PageHeader from '@/shared/components/PageHeader';
import BlacklistCards from '../cards/BlacklistCards';
import CandidateCards from '../cards/CandidateCards';
import CvReviewCards from '../cards/CvReviewCards';
import InterviewCards from '../cards/InterviewCards';
import PipelineCards from '../cards/PipelineCards';
import RequestCards from '../cards/RequestCards';
import ScorecardCards from '../cards/ScorecardCards';
import SummaryStrip from '../cards/SummaryStrip';
import DashboardFilters, { resolveRange } from '../components/DashboardFilters';
import { DashboardLayoutContext } from '../components/DashboardLayoutContext';
import { applyOrder, resolveWidth } from '../layout';
import { useDashboardLayout } from '../queries';
import '../charts/charts.css';

/**
 * İşe alım dashboard'u: kart kart özet. Filtreler adres çubuğunda durur; her tema kendi isteğiyle yüklenir,
 * bir kart yavaşsa ya da hata verirse diğerleri etkilenmez.
 */
export default function DashboardPage() {
  const [editing, setEditing] = useState(false);
  const { layout, save, reset } = useDashboardLayout();
  const layoutContext = useMemo(
    () => ({
      editing,
      orderFor: (gridId, ids) => applyOrder(ids, layout?.grids?.[gridId]),
      setOrder: (gridId, ids) => save({ ...layout, grids: { ...layout?.grids, [gridId]: ids } }),
      widthFor: (cardId, defaultWidth) => resolveWidth(defaultWidth, layout?.widths?.[cardId]),
      setWidth: (cardId, width) =>
        save({ ...layout, widths: { ...layout?.widths, [cardId]: width } }),
    }),
    [editing, layout, save],
  );
  const [params, setParams] = useSearchParams();
  const range = params.get('range') ?? '90';
  const from = params.get('from');
  const to = params.get('to');
  const departmentId = params.get('departmentId');
  const positionId = params.get('positionId');

  const filters = useMemo(
    () => ({
      ...resolveRange(range, from, to),
      ...(departmentId ? { departmentId } : {}),
      ...(positionId ? { positionId } : {}),
    }),
    [range, from, to, departmentId, positionId],
  );

  const update = (changes) =>
    setParams(
      (current) => {
        const next = new URLSearchParams(current);
        Object.entries(changes).forEach(([key, value]) =>
          value ? next.set(key, value) : next.delete(key),
        );
        return next;
      },
      { replace: true },
    );

  return (
    <>
      <PageHeader
        title="Dashboard"
        subtitle={
          editing
            ? 'Kartları üstlerindeki tutamaçtan sürükleyerek aynı bölüm içinde taşıyabilir, genişliklerini ayarlayabilirsiniz. Düzeniniz size özel kaydedilir.'
            : 'İşe alım sürecinin genel görünümü. Kartlara tıklayarak ilgili listeye geçebilirsiniz.'
        }
        actions={
          <>
            {editing && (
              <Button
                type="button"
                label="Varsayılana dön"
                icon="pi pi-refresh"
                outlined
                severity="secondary"
                disabled={!layout}
                onClick={() => reset()}
              />
            )}
            <Button
              type="button"
              label={editing ? 'Bitti' : 'Düzeni düzenle'}
              icon={editing ? 'pi pi-check' : 'pi pi-th-large'}
              outlined={!editing}
              aria-pressed={editing}
              onClick={() => setEditing((value) => !value)}
            />
          </>
        }
      />
      <DashboardFilters
        range={range}
        from={from}
        to={to}
        departmentId={departmentId}
        positionId={positionId}
        onChange={update}
      />
      <DashboardLayoutContext.Provider value={layoutContext}>
        <SummaryStrip filters={filters} />
        <PipelineCards filters={filters} />
        <RequestCards filters={filters} />
        <CandidateCards filters={filters} />
        <CvReviewCards filters={filters} />
        <InterviewCards filters={filters} />
        <ScorecardCards filters={filters} />
        <BlacklistCards filters={filters} />
      </DashboardLayoutContext.Provider>
    </>
  );
}
