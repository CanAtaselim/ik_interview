import { useMutation } from '@tanstack/react-query';
import { Button } from 'primereact/button';
import { confirmDialog } from 'primereact/confirmdialog';
import { InputText } from 'primereact/inputtext';
import { InputTextarea } from 'primereact/inputtextarea';
import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router';
import { startScorecard, updateScorecard } from '@/shared/api/scorecards';
import PageHeader from '@/shared/components/PageHeader';
import StatusTag from '@/shared/components/StatusTag';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { useToast } from '@/shared/components/toast';
import { RECOMMENDATION, SCORE_RUBRIC, SCORECARD_PROGRESS } from '@/shared/constants/labels';
import { ROUTES } from '@/shared/constants/routes';
import { useAutosave } from '@/shared/hooks/useAutosave';
import NotFoundPage from '@/shared/pages/NotFoundPage';
import { useInterview, useScorecard, useSubmitScorecard } from '@/shared/recruitment/queries';
import ScorecardView, { formatScore } from '@/shared/recruitment/ScorecardView';
import { getErrorMessage } from '@/shared/utils/errors';
import { formatTimeRange } from '@/shared/utils/format';
import ScoreButtons from '../components/ScoreButtons';
import '../scorecards.css';

const timeFormat = new Intl.DateTimeFormat('tr-TR', { hour: '2-digit', minute: '2-digit' });
const RECOMMENDATIONS = Object.entries(RECOMMENDATION);

const toInput = (item) => ({
  id: item.id,
  text: item.isCustom ? item.text : null,
  competencyId: item.isCustom ? item.competencyId : null,
  weight: item.isCustom ? item.weight : null,
  score: item.score,
  note: item.note?.trim() || null,
});

/** Önizleme puanı: Σ(puan × ağırlık) / Σ(ağırlık); sunucudaki hesapla aynı. */
const weightedScore = (items) => {
  const scored = items.filter((item) => item.score);
  if (scored.length === 0) return null;
  const total = scored.reduce((sum, item) => sum + item.score * item.weight, 0);
  return Math.round((total / scored.reduce((sum, item) => sum + item.weight, 0)) * 100) / 100;
};

/**
 * Mülakatçının değerlendirme ekranı: her soruya 1–5 puan ve not, genel öneri ve not. Yazılanlar
 * kendiliğinden kaydedilir; gönderilince değerlendirme kilitlenir. Telefonda tek sütun çalışır.
 */
export default function ScorecardPage() {
  const { id: interviewId } = useParams();
  const { data: interview } = useInterview(interviewId);
  const start = useMutation({ mutationFn: startScorecard });
  const [scorecardId, setScorecardId] = useState(null);
  const { data: scorecard, isLoading, isError, error } = useScorecard(scorecardId);

  // Sayfa açılınca mülakatçının değerlendirmesi açılır (yoksa oluşturulur).
  useEffect(() => {
    start.mutate(interviewId, { onSuccess: setScorecardId });
    // eslint-disable-next-line react-hooks/exhaustive-deps -- yalnızca mülakat değişince
  }, [interviewId]);

  if (start.isError) {
    return start.error.response?.status === 404 ? (
      <NotFoundPage />
    ) : (
      <p role="alert">{getErrorMessage(start.error)}</p>
    );
  }
  if (isError) return <p role="alert">{getErrorMessage(error)}</p>;
  if (!scorecardId || isLoading || !scorecard) return <TableSkeleton rows={6} />;

  return (
    <>
      <Link to={ROUTES.interview(interviewId)} className="back-link">
        <i className="pi pi-arrow-left" aria-hidden="true" /> Mülakat
      </Link>
      <PageHeader
        title={
          <span className="dialog-title">
            Değerlendirme{interview ? ` · ${interview.interview.candidateName}` : ''}
            {scorecard.status === 'Submitted' && (
              <StatusTag value="Submitted" map={SCORECARD_PROGRESS} />
            )}
          </span>
        }
        subtitle={
          interview
            ? `${interview.interview.typeName} mülakatı · ${interview.interview.positionTitle} · ${formatTimeRange(interview.interview.startsAt, interview.interview.endsAt)}`
            : undefined
        }
      />
      {scorecard.canEdit ? (
        <ScorecardForm key={scorecard.id} scorecard={scorecard} interviewId={interviewId} />
      ) : (
        <section className="card card--padded scorecard">
          <p className="muted">
            Değerlendirmeniz gönderildi ve kilitlendi. Düzeltme gerekirse İK Yöneticisine başvurun.
          </p>
          <ScorecardView scorecard={scorecard} />
        </section>
      )}
    </>
  );
}

function ScorecardForm({ scorecard, interviewId }) {
  const toast = useToast();
  const submit = useSubmitScorecard();
  const [items, setItems] = useState(scorecard.items);
  const [generalNote, setGeneralNote] = useState(scorecard.generalNote ?? '');
  const [recommendation, setRecommendation] = useState(scorecard.recommendation);
  const [newQuestion, setNewQuestion] = useState('');

  const input = {
    items: items.map(toInput),
    generalNote: generalNote.trim() || null,
    recommendation,
  };
  const autosave = useAutosave({
    value: input,
    save: (value) => updateScorecard({ id: scorecard.id, ...value }),
  });

  const updateItem = (id, changes) =>
    setItems((current) => current.map((item) => (item.id === id ? { ...item, ...changes } : item)));

  const addQuestion = () => {
    const text = newQuestion.trim();
    if (!text) return;
    setItems((current) => [
      ...current,
      {
        id: crypto.randomUUID(),
        questionId: null,
        text,
        guidance: null,
        competencyId: null,
        competencyName: null,
        weight: 1,
        score: null,
        note: null,
        isCustom: true,
      },
    ]);
    setNewQuestion('');
  };

  const sendNow = () => {
    if (!recommendation) {
      toast.error('Göndermeden önce genel önerinizi seçmelisiniz.');
      return;
    }
    if (!items.some((item) => item.score)) {
      toast.error('Göndermeden önce en az bir soruya puan vermelisiniz.');
      return;
    }
    confirmDialog({
      header: 'Değerlendirme gönderilsin mi?',
      message:
        'Gönderdikten sonra değiştiremezsiniz. Diğer mülakatçıların değerlendirmelerini gönderdikten sonra görebilirsiniz.',
      acceptLabel: 'Gönder',
      rejectLabel: 'Vazgeç',
      accept: async () => {
        try {
          // Bekleyen otomatik kaydı beklemeden son hali kaydedilir.
          await updateScorecard({ id: scorecard.id, ...input });
          await submit.mutateAsync(scorecard.id);
          toast.success('Değerlendirmeniz gönderildi.');
        } catch (error) {
          toast.error(getErrorMessage(error));
        }
      },
    });
  };

  const preview = weightedScore(items);

  return (
    <div className="scorecard">
      <p className="autosave-status-inline" data-status={autosave.status} role="status">
        {autosave.status === 'saving' && 'Kaydediliyor…'}
        {autosave.status === 'saved' && `Kaydedildi · ${timeFormat.format(autosave.savedAt)}`}
        {autosave.status === 'error' && `Kaydedilemedi: ${getErrorMessage(autosave.error)}`}
        {autosave.status === 'idle' &&
          'Yazdıklarınız kendiliğinden kaydedilir. Gönderene kadar diğer mülakatçılar göremez.'}
      </p>

      {scorecard.reopenReason && (
        <div className="notice" role="status">
          <i className="pi pi-lock-open" aria-hidden="true" />
          <div>Değerlendirmeniz düzeltme için açıldı: {scorecard.reopenReason}</div>
        </div>
      )}

      <ul className="scorecard-rubric" aria-label="Puan anlamları">
        {Object.entries(SCORE_RUBRIC).map(([score, label]) => (
          <li key={score}>
            <strong>{score}</strong> {label}
          </li>
        ))}
      </ul>

      {items.length === 0 && (
        <section className="card card--padded">
          <p className="muted">
            Bu mülakata soru şablonu bağlanmamış. Sorduğunuz soruları aşağıdan ekleyip puanlayın.
          </p>
        </section>
      )}

      {items.map((item, index) => (
        <section
          key={item.id}
          className="card card--padded scorecard-item"
          aria-label={`Soru ${index + 1}`}
        >
          <div className="scorecard-item__head">
            <span className="scorecard-item__number">{index + 1}.</span>
            <div className="scorecard-item__text">
              <p>{item.text}</p>
              <span className="cell-sub">
                {[
                  item.competencyName,
                  `ağırlık ${formatScore(item.weight)}`,
                  item.isCustom && 'sizin eklediğiniz soru',
                ]
                  .filter(Boolean)
                  .join(' · ')}
              </span>
              {item.guidance && (
                <details>
                  <summary>Beklenen cevap</summary>
                  <p className="multiline">{item.guidance}</p>
                </details>
              )}
            </div>
            <Button
              icon="pi pi-times"
              text
              severity="secondary"
              aria-label="Bu soruyu değerlendirmeden çıkar"
              title="Sormadıysanız değerlendirmeden çıkarın"
              onClick={() => setItems((current) => current.filter((other) => other.id !== item.id))}
            />
          </div>
          <ScoreButtons
            value={item.score}
            label={`Soru ${index + 1} puanı`}
            onChange={(score) => updateItem(item.id, { score })}
          />
          <InputTextarea
            id={`note-${item.id}`}
            aria-label={`Soru ${index + 1} için not`}
            rows={2}
            autoResize
            maxLength={4000}
            placeholder="Not (isteğe bağlı): adayın cevabı, gözlemleriniz"
            value={item.note ?? ''}
            onChange={(event) => updateItem(item.id, { note: event.target.value })}
          />
        </section>
      ))}

      <section className="card card--padded scorecard-item" aria-labelledby="add-question-title">
        <h2 id="add-question-title" className="section-title">
          Soru ekle
        </h2>
        <div className="scorecard-add">
          <InputText
            id="new-question"
            value={newQuestion}
            maxLength={2000}
            placeholder="Şablonda olmayan, sorduğunuz bir soru"
            onChange={(event) => setNewQuestion(event.target.value)}
            onKeyDown={(event) => event.key === 'Enter' && addQuestion()}
          />
          <Button
            label="Ekle"
            icon="pi pi-plus"
            outlined
            disabled={!newQuestion.trim()}
            onClick={addQuestion}
          />
        </div>
      </section>

      <section className="card card--padded scorecard-item" aria-labelledby="overall-title">
        <h2 id="overall-title" className="section-title">
          Genel değerlendirme
        </h2>
        <div className="recommendation-buttons" role="radiogroup" aria-label="Genel öneri">
          {RECOMMENDATIONS.map(([value, { label, severity }]) => (
            <Button
              key={value}
              type="button"
              role="radio"
              aria-checked={recommendation === value}
              label={label}
              severity={severity === 'secondary' ? 'secondary' : severity}
              outlined={recommendation !== value}
              onClick={() => setRecommendation(recommendation === value ? null : value)}
            />
          ))}
        </div>
        <InputTextarea
          id="general-note"
          aria-label="Genel not"
          rows={3}
          autoResize
          maxLength={4000}
          placeholder="Genel izleniminiz, güçlü ve gelişime açık yönler"
          value={generalNote}
          onChange={(event) => setGeneralNote(event.target.value)}
        />
      </section>

      <div className="scorecard-footer">
        <span className="scorecard-score">Puan: {formatScore(preview)}</span>
        <Button
          label="Değerlendirmeyi gönder"
          icon="pi pi-send"
          disabled={!scorecard.canSubmit}
          title={scorecard.canSubmit ? undefined : 'Mülakat başladıktan sonra gönderebilirsiniz.'}
          loading={submit.isPending}
          onClick={sendNow}
        />
      </div>
      {!scorecard.canSubmit && (
        <p className="muted">
          Taslağı şimdiden doldurabilirsiniz; göndermek mülakat başladıktan sonra açılır.
        </p>
      )}
      <Link to={ROUTES.interview(interviewId)}>
        Adayın CV’si ve ön görüşme özeti için mülakat sayfasına dönün
      </Link>
    </div>
  );
}
