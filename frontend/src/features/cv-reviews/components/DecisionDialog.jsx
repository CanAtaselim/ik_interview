import { Button } from 'primereact/button';
import { Dialog } from 'primereact/dialog';
import { InputTextarea } from 'primereact/inputtextarea';
import { SelectButton } from 'primereact/selectbutton';
import { useState } from 'react';
import DictionarySelect from '@/shared/components/DictionarySelect';
import FormField from '@/shared/components/FormField';
import { useToast } from '@/shared/components/toast';
import { useDecideCvReview } from '@/shared/recruitment/queries';
import { getErrorMessage } from '@/shared/utils/errors';

const REASON_MAX_LENGTH = 2000;
const SUCCESS_MESSAGES = {
  Positive: 'Olumlu kararınız İK ekibine iletildi.',
  Negative: 'Olumsuz kararınız İK ekibine iletildi.',
  OnHold: 'Değerlendirme beklemeye alındı. Kararınızı daha sonra verebilirsiniz.',
};

/**
 * Takım liderinin kararı: olumlu / olumsuz / beklet + gerekçe (zorunlu). Olumsuz kararda ret
 * nedeni de seçilir. Olumlu ve olumsuz karar kesindir.
 * @param {{ review: import('@/shared/api/cvReviews').CvReview, initialDecision: string,
 *   onClose: () => void }} props
 */
export default function DecisionDialog({ review, initialDecision, onClose }) {
  const toast = useToast();
  const decide = useDecideCvReview();
  const [decision, setDecision] = useState(initialDecision);
  const [reason, setReason] = useState('');
  const [rejectionReasonId, setRejectionReasonId] = useState(null);
  const [errors, setErrors] = useState({});

  const options = [
    { label: 'Olumlu', value: 'Positive' },
    { label: 'Olumsuz', value: 'Negative' },
    // Zaten bekletilen değerlendirme yeniden bekletilemez.
    ...(review.decision === 'OnHold' ? [] : [{ label: 'Beklet', value: 'OnHold' }]),
  ];

  const submit = () => {
    const found = {};
    if (!reason.trim()) found.reason = 'Kararınızın gerekçesini yazmalısınız.';
    if (decision === 'Negative' && !rejectionReasonId)
      found.rejectionReasonId = 'Olumsuz karar için ret nedeni seçmelisiniz.';
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    decide.mutate(
      {
        id: review.id,
        decision,
        reason: reason.trim(),
        rejectionReasonId: decision === 'Negative' ? rejectionReasonId : null,
      },
      {
        onSuccess: () => {
          toast.success(SUCCESS_MESSAGES[decision]);
          onClose();
        },
        onError: (error) => {
          const fieldErrors = error.response?.data?.errors;
          if (fieldErrors) {
            setErrors(
              Object.fromEntries(
                Object.entries(fieldErrors).map(([field, messages]) => [field, messages[0]]),
              ),
            );
          } else toast.error(getErrorMessage(error));
        },
      },
    );
  };

  const footer = (
    <div className="dialog-actions">
      <Button type="button" label="Vazgeç" text severity="secondary" onClick={onClose} />
      <Button type="button" label="Kararı kaydet" loading={decide.isPending} onClick={submit} />
    </div>
  );

  return (
    <Dialog
      visible
      header={`${review.candidateName} için kararınız`}
      footer={footer}
      onHide={onClose}
      className="form-dialog"
      modal
    >
      <div className="form-grid">
        <div className="form-grid__wide">
          <FormField label="Karar" htmlFor="decision-value">
            <SelectButton
              id="decision-value"
              value={decision}
              options={options}
              allowEmpty={false}
              onChange={(event) => setDecision(event.value)}
            />
          </FormField>
        </div>

        {decision === 'Negative' && (
          <div className="form-grid__wide">
            <FormField
              label="Ret nedeni"
              htmlFor="decision-rejection-reason"
              error={errors.rejectionReasonId}
            >
              <DictionarySelect
                type="RejectionReason"
                inputId="decision-rejection-reason"
                value={rejectionReasonId}
                invalid={Boolean(errors.rejectionReasonId)}
                onChange={(id) => {
                  setRejectionReasonId(id);
                  setErrors((current) => ({ ...current, rejectionReasonId: undefined }));
                }}
              />
            </FormField>
          </div>
        )}

        <div className="form-grid__wide">
          <FormField label="Gerekçe" htmlFor="decision-reason" error={errors.reason}>
            <InputTextarea
              id="decision-reason"
              rows={4}
              autoResize
              maxLength={REASON_MAX_LENGTH}
              value={reason}
              invalid={Boolean(errors.reason)}
              placeholder={
                decision === 'OnHold'
                  ? 'Neden beklettiğinizi yazın (ör. önce diğer adayları görmek istiyorum).'
                  : 'İK ekibinin göreceği kısa değerlendirmeniz.'
              }
              onChange={(event) => {
                setReason(event.target.value);
                setErrors((current) => ({ ...current, reason: undefined }));
              }}
            />
          </FormField>
        </div>

        <p className="muted form-grid__wide">
          {decision === 'OnHold'
            ? 'Bekletilen değerlendirme listenizde kalır; kararınızı sonra verebilirsiniz.'
            : 'Olumlu ve olumsuz karar kaydedildikten sonra değiştirilemez.'}
        </p>
      </div>
    </Dialog>
  );
}
