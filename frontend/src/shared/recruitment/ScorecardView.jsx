import { Tag } from 'primereact/tag';
import StatusTag from '@/shared/components/StatusTag';
import { RECOMMENDATION, SCORE_RUBRIC } from '@/shared/constants/labels';
import { formatDateTime } from '@/shared/utils/format';
import './ScorecardView.css';

/** Puanı "4,17" biçiminde yazar. @param {number | null | undefined} value */
export const formatScore = (value) =>
  value === null || value === undefined
    ? '—'
    : value.toLocaleString('tr-TR', { minimumFractionDigits: 0, maximumFractionDigits: 2 });

/**
 * Gönderilmiş (veya İK'nın baktığı) değerlendirmenin salt okunur görünümü.
 * @param {{ scorecard: object }} props
 */
export default function ScorecardView({ scorecard }) {
  return (
    <div className="scorecard-view">
      <div className="dialog-title">
        {scorecard.recommendation ? (
          <StatusTag value={scorecard.recommendation} map={RECOMMENDATION} />
        ) : (
          <Tag value="Öneri seçilmedi" severity="secondary" />
        )}
        <span className="scorecard-score">Puan: {formatScore(scorecard.weightedScore)}</span>
        {scorecard.submittedAt && (
          <span className="cell-sub">Gönderildi · {formatDateTime(scorecard.submittedAt)}</span>
        )}
      </div>

      {scorecard.competencyScores.length > 0 && (
        <div className="competency-scores">
          {scorecard.competencyScores.map((competency) => (
            <Tag
              key={competency.competencyId}
              value={`${competency.name}: ${formatScore(competency.score)}`}
              severity="info"
            />
          ))}
        </div>
      )}

      {scorecard.items.map((item, index) => (
        <div key={item.id} className="scorecard-view__item">
          <div>
            <strong>
              {index + 1}. {item.text}
            </strong>
            <div className="cell-sub">
              {[
                item.competencyName,
                `ağırlık ${formatScore(item.weight)}`,
                item.isCustom && 'mülakatçının eklediği soru',
              ]
                .filter(Boolean)
                .join(' · ')}
            </div>
            {item.note && <p className="multiline">{item.note}</p>}
          </div>
          <span className="scorecard-view__score">
            {item.score ? `${item.score} · ${SCORE_RUBRIC[item.score]}` : 'Puansız'}
          </span>
        </div>
      ))}

      {scorecard.generalNote && (
        <div>
          <h3 className="section-title">Genel not</h3>
          <p className="multiline">{scorecard.generalNote}</p>
        </div>
      )}
    </div>
  );
}
