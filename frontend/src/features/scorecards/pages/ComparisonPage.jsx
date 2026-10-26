import { Tag } from 'primereact/tag';
import { Link, useParams } from 'react-router';
import { HR_ROLES } from '@/shared/auth/roles';
import { useAuth } from '@/shared/auth/useAuth';
import EmptyState from '@/shared/components/EmptyState';
import PageHeader from '@/shared/components/PageHeader';
import StatusTag from '@/shared/components/StatusTag';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { APPLICATION_STATUS, DEBRIEF_DECISION, RECOMMENDATION } from '@/shared/constants/labels';
import { ROUTES } from '@/shared/constants/routes';
import NotFoundPage from '@/shared/pages/NotFoundPage';
import { usePositionComparison } from '@/shared/recruitment/queries';
import { formatScore } from '@/shared/recruitment/ScorecardView';
import { getErrorMessage } from '@/shared/utils/errors';
import '../scorecards.css';

const RECOMMENDATION_ORDER = Object.keys(RECOMMENDATION);

/** Puanı 1–5 aralığında yatay çubukla gösterir; sayı her zaman yazılıdır. */
function ScoreBar({ value }) {
  if (value === null || value === undefined) return <span className="muted">—</span>;
  return (
    <span className="score-bar" title={`${formatScore(value)} / 5`}>
      <span className="score-bar__track" aria-hidden="true">
        <span className="score-bar__fill" style={{ width: `${(value / 5) * 100}%` }} />
      </span>
      <span className="score-bar__value">{formatScore(value)}</span>
    </span>
  );
}

/**
 * Pozisyondaki adayların karşılaştırması: gönderilmiş değerlendirmelerden ortalama puan,
 * yetkinlik puanları, öneriler ve ortak karar. Mülakatçı kendi değerlendirmesini göndermediği
 * adayın puanlarını göremez.
 */
export default function ComparisonPage() {
  const { id } = useParams();
  const { hasAnyRole } = useAuth();
  const isHr = hasAnyRole(HR_ROLES);
  const { data, isLoading, isError, error } = usePositionComparison(id);

  if (isLoading) return <TableSkeleton rows={6} />;
  if (isError) {
    return error.response?.status === 404 ? (
      <NotFoundPage />
    ) : (
      <p role="alert">{getErrorMessage(error)}</p>
    );
  }

  return (
    <>
      <Link to={ROUTES.position(data.positionId)} className="back-link">
        <i className="pi pi-arrow-left" aria-hidden="true" /> {data.positionTitle}
      </Link>
      <PageHeader
        title="Aday karşılaştırma"
        subtitle={`${data.positionTitle} · puanlar gönderilmiş mülakat değerlendirmelerinin ağırlıklı ortalamasıdır (1–5).`}
      />

      {data.candidates.length === 0 ? (
        <section className="card">
          <EmptyState
            icon="pi pi-chart-bar"
            title="Karşılaştırılacak aday yok"
            description="Pozisyonun sürecindeki adaylar burada değerlendirme puanlarıyla yan yana görünür."
          />
        </section>
      ) : (
        <section className="card comparison-wrap">
          <table className="comparison">
            <thead>
              <tr>
                <th scope="col">Aday</th>
                <th scope="col">Ortalama</th>
                {data.competencies.map((competency) => (
                  <th key={competency.competencyId} scope="col">
                    {competency.name}
                  </th>
                ))}
                <th scope="col">Öneriler</th>
                <th scope="col">Ortak karar</th>
              </tr>
            </thead>
            <tbody>
              {data.candidates.map((row) => (
                <tr key={row.applicationId}>
                  <th scope="row">
                    {isHr ? (
                      <Link to={ROUTES.candidate(row.candidateId)} className="table-link">
                        {row.candidateName}
                      </Link>
                    ) : (
                      row.candidateName
                    )}
                    <div className="cell-sub">
                      {row.stageName}
                      {row.status !== 'Active' && (
                        <>
                          {' '}
                          <StatusTag value={row.status} map={APPLICATION_STATUS} />
                        </>
                      )}
                    </div>
                    <div className="cell-sub">
                      {row.submittedCount} / {row.expectedCount} değerlendirme
                    </div>
                  </th>
                  {row.hidden ? (
                    <td colSpan={data.competencies.length + 3} className="muted">
                      Bu adayla ilgili değerlendirmenizi gönderince puanlar görünür.
                    </td>
                  ) : (
                    <>
                      <td>
                        <ScoreBar value={row.averageScore} />
                      </td>
                      {data.competencies.map((competency) => (
                        <td key={competency.competencyId}>
                          <ScoreBar
                            value={
                              row.competencyScores.find(
                                (score) => score.competencyId === competency.competencyId,
                              )?.score ?? null
                            }
                          />
                        </td>
                      ))}
                      <td>
                        <div className="tag-list">
                          {RECOMMENDATION_ORDER.filter((key) => row.recommendations[key]).map(
                            (key) => (
                              <Tag
                                key={key}
                                value={`${RECOMMENDATION[key].label} × ${row.recommendations[key]}`}
                                severity={RECOMMENDATION[key].severity}
                              />
                            ),
                          )}
                          {Object.keys(row.recommendations).length === 0 && (
                            <span className="muted">—</span>
                          )}
                        </div>
                      </td>
                      <td>
                        {row.lastDebrief ? (
                          <StatusTag value={row.lastDebrief} map={DEBRIEF_DECISION} />
                        ) : (
                          <span className="muted">—</span>
                        )}
                      </td>
                    </>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      )}
    </>
  );
}
