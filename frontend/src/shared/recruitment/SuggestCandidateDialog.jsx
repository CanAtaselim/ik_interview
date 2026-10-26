import { Button } from 'primereact/button';
import { Dialog } from 'primereact/dialog';
import { Dropdown } from 'primereact/dropdown';
import { InputTextarea } from 'primereact/inputtextarea';
import { MultiSelect } from 'primereact/multiselect';
import { useState } from 'react';
import FormField from '@/shared/components/FormField';
import { useToast } from '@/shared/components/toast';
import { getErrorMessage } from '@/shared/utils/errors';
import BlacklistWarning from './BlacklistWarning';
import { useSuggestCandidate, useTeamLeads } from './queries';

const BLACKLIST_CODE = 'CvReview.BlacklistMatch';
const NO_POSITION = 'none';
const NOTE_MAX_LENGTH = 2000;

/**
 * Adayın CV'sini bir veya birden çok takım liderine değerlendirmesi için önerir. Aday birden çok
 * pozisyonda süreçteyse hangisi için önerildiği seçilir; süreci yoksa pozisyon belirtilmeden önerilir.
 * @param {{ candidate: { id: string, name: string },
 *   applications?: { id: string, positionTitle: string }[], onClose: () => void }} props
 */
export default function SuggestCandidateDialog({ candidate, applications = [], onClose }) {
  const toast = useToast();
  const { data: leads = [], isLoading } = useTeamLeads();
  const suggest = useSuggestCandidate();
  const [target, setTarget] = useState(
    applications.length === 1 ? applications[0].id : NO_POSITION,
  );
  const [reviewerIds, setReviewerIds] = useState([]);
  const [note, setNote] = useState('');
  const [reviewerError, setReviewerError] = useState(null);
  const [blacklistMatches, setBlacklistMatches] = useState([]);

  const targetOptions = [
    ...applications.map((application) => ({
      value: application.id,
      label: application.positionTitle,
    })),
    { value: NO_POSITION, label: 'Pozisyon belirtmeden (genel değerlendirme)' },
  ];

  const leadOptions = leads.map((lead) => ({
    value: lead.id,
    label: lead.department ? `${lead.displayName} · ${lead.department}` : lead.displayName,
  }));

  const submit = () => {
    if (reviewerIds.length === 0) {
      setReviewerError('En az bir takım lideri seçmelisiniz.');
      return;
    }

    suggest.mutate(
      {
        candidateId: candidate.id,
        applicationId: target === NO_POSITION ? null : target,
        reviewerUserIds: reviewerIds,
        note: note.trim() || null,
        confirmBlacklist: blacklistMatches.length > 0,
      },
      {
        onSuccess: (reviewIds) => {
          toast.success(
            reviewIds.length === 1
              ? `${candidate.name} takım liderine önerildi.`
              : `${candidate.name} ${reviewIds.length} takım liderine önerildi.`,
          );
          onClose();
        },
        onError: (error) => {
          const problem = error.response?.data;
          if (problem?.code === BLACKLIST_CODE) setBlacklistMatches(problem.blacklistMatches);
          else if (problem?.errors?.reviewerUserIds)
            setReviewerError(problem.errors.reviewerUserIds[0]);
          else toast.error(getErrorMessage(error));
        },
      },
    );
  };

  const footer = (
    <div className="dialog-actions">
      <Button type="button" label="Vazgeç" text severity="secondary" onClick={onClose} />
      <Button
        type="button"
        label={blacklistMatches.length > 0 ? 'Yine de öner' : 'Öner'}
        icon="pi pi-send"
        severity={blacklistMatches.length > 0 ? 'danger' : undefined}
        loading={suggest.isPending}
        onClick={submit}
      />
    </div>
  );

  return (
    <Dialog
      visible
      header="Takım liderine öner"
      footer={footer}
      onHide={onClose}
      className="form-dialog"
      modal
    >
      <div className="form-grid">
        <p className="form-grid__wide">
          <strong>{candidate.name}</strong> adayının CV’si ve ön görüşme özeti seçtiğiniz takım
          liderlerine gider. Ücret bilgisi ve İK yorumu paylaşılmaz.
        </p>

        {applications.length > 0 && (
          <div className="form-grid__wide">
            <FormField label="Hangi pozisyon için?" htmlFor="suggest-target">
              <Dropdown
                inputId="suggest-target"
                value={target}
                options={targetOptions}
                onChange={(event) => setTarget(event.value)}
              />
            </FormField>
          </div>
        )}

        <div className="form-grid__wide">
          <FormField label="Takım liderleri" htmlFor="suggest-reviewers" error={reviewerError}>
            <MultiSelect
              inputId="suggest-reviewers"
              value={reviewerIds}
              options={leadOptions}
              onChange={(event) => {
                setReviewerIds(event.value);
                setReviewerError(null);
              }}
              placeholder={isLoading ? 'Yükleniyor…' : 'Bir veya birden çok kişi seçin'}
              emptyMessage="Seçilebilecek takım lideri yok. Kullanıcılar ilk girişlerinden sonra listelenir."
              emptyFilterMessage="Eşleşen takım lideri yok."
              display="chip"
              filter={leadOptions.length > 8}
              showSelectAll={false}
              invalid={Boolean(reviewerError)}
              disabled={isLoading}
              aria-describedby={reviewerError ? 'suggest-reviewers-error' : undefined}
            />
          </FormField>
        </div>

        <div className="form-grid__wide">
          <FormField label="Not (isteğe bağlı)" htmlFor="suggest-note">
            <InputTextarea
              id="suggest-note"
              rows={3}
              autoResize
              maxLength={NOTE_MAX_LENGTH}
              value={note}
              placeholder="Özellikle bakmasını istediğiniz bir konu varsa yazın."
              onChange={(event) => setNote(event.target.value)}
            />
          </FormField>
        </div>

        <div className="form-grid__wide">
          <BlacklistWarning
            matches={blacklistMatches}
            message="Bu aday kara listedeki bir kayıtla eşleşiyor. Yine de önerirseniz bu kararınız kayda geçer."
          />
        </div>
      </div>
    </Dialog>
  );
}
