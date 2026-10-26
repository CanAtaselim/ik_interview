import { SCORE_RUBRIC } from '@/shared/constants/labels';

const SCORES = [1, 2, 3, 4, 5];

/**
 * 1–5 puan seçimi; seçili puana tekrar basmak puanı kaldırır. Seçilen puanın rubrikteki anlamı
 * altında yazar.
 * @param {{ value: number | null, onChange: (value: number | null) => void, label: string,
 *   disabled?: boolean }} props
 */
export default function ScoreButtons({ value, onChange, label, disabled = false }) {
  return (
    <div>
      <div className="score-buttons" role="radiogroup" aria-label={label}>
        {SCORES.map((score) => (
          <button
            key={score}
            type="button"
            role="radio"
            aria-checked={value === score}
            title={SCORE_RUBRIC[score]}
            disabled={disabled}
            onClick={() => onChange(value === score ? null : score)}
          >
            {score}
          </button>
        ))}
      </div>
      <p className="score-hint">{value ? `${value} · ${SCORE_RUBRIC[value]}` : 'Puan verilmedi'}</p>
    </div>
  );
}
