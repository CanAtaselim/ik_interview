import dayjs from 'dayjs';
import { AutoComplete } from 'primereact/autocomplete';
import { Button } from 'primereact/button';
import { Calendar } from 'primereact/calendar';
import { Checkbox } from 'primereact/checkbox';
import { Dialog } from 'primereact/dialog';
import { Dropdown } from 'primereact/dropdown';
import { InputText } from 'primereact/inputtext';
import { InputTextarea } from 'primereact/inputtextarea';
import { MultiSelect } from 'primereact/multiselect';
import { SelectButton } from 'primereact/selectbutton';
import { useState } from 'react';
import { searchActiveApplications } from '@/shared/api/interviews';
import DictionarySelect from '@/shared/components/DictionarySelect';
import FormField from '@/shared/components/FormField';
import { useToast } from '@/shared/components/toast';
import { INTERVIEW_MODE_LABELS, toOptions } from '@/shared/constants/labels';
import { getErrorMessage } from '@/shared/utils/errors';
import { formatTimeRange } from '@/shared/utils/format';
import BlacklistWarning from './BlacklistWarning';
import InterviewerAvailability from './InterviewerAvailability';
import {
  useInterviewers,
  useInterviewTemplates,
  useRescheduleInterview,
  useScheduleInterview,
} from './queries';

const MODE_OPTIONS = toOptions(INTERVIEW_MODE_LABELS);
const DURATIONS = [30, 45, 60, 90, 120];
const TEXT_MAX_LENGTH = 2000;
const CONFLICT_CODE = 'Interview.Conflicts';
const BLACKLIST_CODE = 'Interview.BlacklistMatch';

/** Bir sonraki tam saat. */
const nextHour = () => dayjs().add(1, 'hour').startOf('hour').toDate();

/**
 * Mülakat planlama ve yeniden planlama. Süreç üç yoldan gelir: sabit (kanban kartı), adayın
 * süreçlerinden seçim (aday kartı) veya aday adıyla arama (takvim). Seçilen gün için
 * mülakatçıların müsaitliği ve dolu saatleri pencerede görünür; çakışma uyarısı İK onayıyla geçilir.
 * @param {{ application?: { id: string, candidateName: string, positionTitle: string } | null,
 *   applications?: { id: string, positionTitle: string }[], candidateName?: string,
 *   interview?: (import('@/shared/api/interviews').Interview & { note?: string | null }) | null,
 *   initialStart?: Date | null,
 *   initialDurationMinutes?: number | null, onClose: () => void,
 *   onSaved?: (id: string) => void }} props
 */
export default function ScheduleInterviewDialog({
  application = null,
  applications = [],
  candidateName,
  interview = null,
  initialStart = null,
  initialDurationMinutes = null,
  onClose,
  onSaved,
}) {
  const toast = useToast();
  const { data: interviewers = [], isLoading: loadingInterviewers } = useInterviewers();
  const { data: templates = [] } = useInterviewTemplates({ isActive: true });
  const schedule = useScheduleInterview();
  const reschedule = useRescheduleInterview();
  const isReschedule = Boolean(interview);

  const [applicationId, setApplicationId] = useState(
    interview?.applicationId ??
      application?.id ??
      (applications.length === 1 ? applications[0].id : null),
  );
  const [searchValue, setSearchValue] = useState(null);
  const [suggestions, setSuggestions] = useState([]);
  const [typeId, setTypeId] = useState(interview?.typeId ?? null);
  const [startsAt, setStartsAt] = useState(
    interview ? new Date(interview.startsAt) : (initialStart ?? nextHour()),
  );
  const [duration, setDuration] = useState(
    interview?.durationMinutes ?? initialDurationMinutes ?? 60,
  );
  const [interviewerIds, setInterviewerIds] = useState(
    interview?.interviewers.map((item) => item.userId) ?? [],
  );
  const [mode, setMode] = useState(interview?.mode ?? 'InPerson');
  const [roomId, setRoomId] = useState(interview?.roomId ?? null);
  const [meetingUrl, setMeetingUrl] = useState(interview?.meetingUrl ?? '');
  const [note, setNote] = useState(interview?.note ?? '');
  const [templateId, setTemplateId] = useState(interview?.templateId ?? null);
  const [notifyCandidate, setNotifyCandidate] = useState(interview?.notifyCandidate ?? true);
  const [errors, setErrors] = useState({});
  const [conflicts, setConflicts] = useState([]);
  const [blacklistMatches, setBlacklistMatches] = useState([]);

  const durationOptions = [...new Set([...DURATIONS, duration])]
    .sort((a, b) => a - b)
    .map((value) => ({ value, label: `${value} dakika` }));
  const interviewerOptions = interviewers.map((user) => ({
    value: user.id,
    label: user.department ? `${user.displayName} · ${user.department}` : user.displayName,
  }));
  const endsAt = startsAt ? dayjs(startsAt).add(duration, 'minute').toDate() : null;

  // Plan değişince eski uyarılar geçersiz olur; yeniden sorulur.
  const change = (setter, field) => (value) => {
    setter(value);
    setConflicts([]);
    if (field) setErrors((current) => ({ ...current, [field]: undefined }));
  };

  const searchApplications = async (event) => {
    try {
      const found = await searchActiveApplications(event.query);
      setSuggestions(
        found.map((item) => ({ ...item, label: `${item.candidateName} · ${item.positionTitle}` })),
      );
    } catch (error) {
      toast.error(getErrorMessage(error));
    }
  };

  const validate = () => {
    const found = {};
    if (!applicationId) found.applicationId = 'Mülakatın hangi süreç için olduğunu seçmelisiniz.';
    if (!typeId) found.typeId = 'Mülakat tipini seçmelisiniz.';
    if (!startsAt) found.startsAt = 'Mülakatın tarih ve saatini seçmelisiniz.';
    else if (startsAt <= new Date()) found.startsAt = 'Geçmiş bir saate mülakat planlanamaz.';
    if (interviewerIds.length === 0) found.interviewerUserIds = 'En az bir mülakatçı seçmelisiniz.';
    setErrors(found);
    return Object.keys(found).length === 0;
  };

  const submit = () => {
    if (!validate()) return;

    const plan = {
      typeId,
      mode,
      startsAt: startsAt.toISOString(),
      durationMinutes: duration,
      roomId: mode === 'InPerson' ? roomId : null,
      meetingUrl: mode === 'Online' ? meetingUrl.trim() || null : null,
      note: note.trim() || null,
      interviewerUserIds: interviewerIds,
      templateId,
      notifyCandidate,
      confirmConflicts: conflicts.length > 0,
    };
    const onSuccess = (id) => {
      toast.success(
        `${isReschedule ? 'Mülakat güncellendi' : 'Mülakat planlandı'}: ${formatTimeRange(plan.startsAt, endsAt.toISOString())}.`,
      );
      onSaved?.(id ?? interview?.id);
      onClose();
    };
    const onError = (error) => {
      const problem = error.response?.data;
      if (problem?.code === CONFLICT_CODE) setConflicts(problem.conflicts);
      else if (problem?.code === BLACKLIST_CODE) setBlacklistMatches(problem.blacklistMatches);
      else if (problem?.errors)
        setErrors(
          Object.fromEntries(
            Object.entries(problem.errors).map(([field, messages]) => [field, messages[0]]),
          ),
        );
      else toast.error(getErrorMessage(error));
    };

    if (isReschedule) reschedule.mutate({ id: interview.id, ...plan }, { onSuccess, onError });
    else
      schedule.mutate(
        { applicationId, ...plan, confirmBlacklist: blacklistMatches.length > 0 },
        { onSuccess, onError },
      );
  };

  const needsConfirmation = conflicts.length > 0 || blacklistMatches.length > 0;
  const footer = (
    <div className="dialog-actions">
      <Button type="button" label="Vazgeç" text severity="secondary" onClick={onClose} />
      <Button
        type="button"
        label={
          needsConfirmation ? 'Yine de planla' : isReschedule ? 'Değişikliği kaydet' : 'Planla'
        }
        icon="pi pi-calendar-plus"
        severity={needsConfirmation ? 'danger' : undefined}
        loading={schedule.isPending || reschedule.isPending}
        onClick={submit}
      />
    </div>
  );

  const fixedLabel = interview
    ? `${interview.candidateName} · ${interview.positionTitle}`
    : application && `${application.candidateName} · ${application.positionTitle}`;

  return (
    <Dialog
      visible
      header={isReschedule ? 'Mülakatı değiştir' : 'Mülakat planla'}
      footer={footer}
      onHide={onClose}
      className="form-dialog schedule-dialog"
      modal
    >
      <div className="form-grid">
        <div className="form-grid__wide">
          {fixedLabel && (
            <p>
              <strong>{fixedLabel}</strong>
            </p>
          )}
          {!fixedLabel && applications.length > 0 && (
            <FormField
              label={`${candidateName} hangi pozisyon için görüşülecek?`}
              htmlFor="interview-application"
              error={errors.applicationId}
            >
              <Dropdown
                inputId="interview-application"
                value={applicationId}
                options={applications.map((item) => ({
                  value: item.id,
                  label: item.positionTitle,
                }))}
                onChange={(event) => change(setApplicationId, 'applicationId')(event.value)}
                placeholder="Süreç seçin"
                invalid={Boolean(errors.applicationId)}
              />
            </FormField>
          )}
          {!fixedLabel && applications.length === 0 && (
            <FormField label="Aday" htmlFor="interview-application" error={errors.applicationId}>
              <AutoComplete
                inputId="interview-application"
                value={searchValue}
                suggestions={suggestions}
                completeMethod={searchApplications}
                field="label"
                placeholder="Aday adını yazın"
                emptyMessage="Devam eden süreci olan aday bulunamadı."
                showEmptyMessage
                invalid={Boolean(errors.applicationId)}
                onChange={(event) => {
                  setSearchValue(event.value);
                  change(setApplicationId, 'applicationId')(event.value?.applicationId ?? null);
                }}
              />
            </FormField>
          )}
        </div>

        <FormField label="Mülakat tipi" htmlFor="interview-type" error={errors.typeId}>
          <DictionarySelect
            type="InterviewType"
            inputId="interview-type"
            value={typeId}
            invalid={Boolean(errors.typeId)}
            onChange={change(setTypeId, 'typeId')}
          />
        </FormField>

        <FormField label="Şekli" htmlFor="interview-mode">
          <SelectButton
            id="interview-mode"
            value={mode}
            options={MODE_OPTIONS}
            allowEmpty={false}
            onChange={(event) => change(setMode)(event.value)}
          />
        </FormField>

        <FormField label="Tarih ve saat" htmlFor="interview-starts" error={errors.startsAt}>
          <Calendar
            inputId="interview-starts"
            value={startsAt}
            onChange={(event) => change(setStartsAt, 'startsAt')(event.value)}
            showTime
            hourFormat="24"
            stepMinute={15}
            dateFormat="dd.mm.yy"
            minDate={new Date()}
            showIcon
            invalid={Boolean(errors.startsAt)}
          />
        </FormField>

        <FormField label="Süre" htmlFor="interview-duration" error={errors.durationMinutes}>
          <Dropdown
            inputId="interview-duration"
            value={duration}
            options={durationOptions}
            onChange={(event) => change(setDuration, 'durationMinutes')(event.value)}
          />
        </FormField>

        <div className="form-grid__wide">
          <FormField
            label="Mülakatçılar"
            htmlFor="interview-interviewers"
            error={errors.interviewerUserIds}
          >
            <MultiSelect
              inputId="interview-interviewers"
              value={interviewerIds}
              options={interviewerOptions}
              onChange={(event) => change(setInterviewerIds, 'interviewerUserIds')(event.value)}
              placeholder={loadingInterviewers ? 'Yükleniyor…' : 'Bir veya birden çok kişi seçin'}
              emptyMessage="Seçilebilecek kişi yok. Kullanıcılar ilk girişlerinden sonra listelenir."
              emptyFilterMessage="Eşleşen kişi yok."
              display="chip"
              filter={interviewerOptions.length > 8}
              showSelectAll={false}
              invalid={Boolean(errors.interviewerUserIds)}
              disabled={loadingInterviewers}
            />
          </FormField>
        </div>

        <div className="form-grid__wide">
          {mode === 'InPerson' ? (
            <FormField label="Oda (isteğe bağlı)" htmlFor="interview-room" error={errors.roomId}>
              <DictionarySelect
                type="Room"
                inputId="interview-room"
                value={roomId}
                showClear
                placeholder="Oda seçin"
                invalid={Boolean(errors.roomId)}
                onChange={change(setRoomId, 'roomId')}
              />
            </FormField>
          ) : (
            <FormField
              label="Toplantı bağlantısı (isteğe bağlı)"
              htmlFor="interview-link"
              error={errors.meetingUrl}
            >
              <InputText
                id="interview-link"
                value={meetingUrl}
                placeholder="https://teams.microsoft.com/…"
                invalid={Boolean(errors.meetingUrl)}
                onChange={(event) => change(setMeetingUrl, 'meetingUrl')(event.target.value)}
              />
            </FormField>
          )}
        </div>

        {startsAt && (interviewerIds.length > 0 || (mode === 'InPerson' && roomId)) && (
          <div className="form-grid__wide">
            <InterviewerAvailability
              startsAt={startsAt}
              endsAt={endsAt}
              userIds={interviewerIds}
              roomId={mode === 'InPerson' ? roomId : null}
              excludeInterviewId={interview?.id ?? null}
            />
          </div>
        )}

        <div className="form-grid__wide">
          <FormField
            label="Soru şablonu (isteğe bağlı)"
            htmlFor="interview-template"
            error={errors.templateId}
          >
            <Dropdown
              inputId="interview-template"
              value={templateId}
              options={templates.map((template) => ({
                value: template.id,
                label: [template.name, template.interviewTypeName, template.departmentName]
                  .filter(Boolean)
                  .join(' · '),
              }))}
              onChange={(event) => setTemplateId(event.value ?? null)}
              placeholder={
                isReschedule ? 'Şablonsuz' : 'Otomatik: pozisyonun bu tipe uygun şablonu'
              }
              showClear
              filter={templates.length > 8}
              filterInputAutoFocus
              emptyMessage="Aktif şablon yok."
            />
          </FormField>
        </div>

        <div className="form-grid__wide">
          <FormField label="Mülakatçılara not (isteğe bağlı)" htmlFor="interview-note">
            <InputTextarea
              id="interview-note"
              rows={2}
              autoResize
              maxLength={TEXT_MAX_LENGTH}
              value={note}
              placeholder="Odaklanılmasını istediğiniz konular"
              onChange={(event) => setNote(event.target.value)}
            />
          </FormField>
        </div>

        <label className="toolbar__check form-grid__wide" htmlFor="interview-notify-candidate">
          <Checkbox
            inputId="interview-notify-candidate"
            checked={notifyCandidate}
            onChange={(event) => setNotifyCandidate(event.checked)}
          />
          {isReschedule
            ? 'Değişikliği adaya e-postayla bildir (takvim daveti ekli)'
            : 'Adaya e-postayla davet gönder (takvim daveti ekli)'}
        </label>

        {conflicts.length > 0 && (
          <div className="notice form-grid__wide" role="alert">
            <i className="pi pi-exclamation-triangle" aria-hidden="true" />
            <div>
              <p>Seçilen saatte çakışma var. Yine de planlarsanız bu kararınız kayda geçer.</p>
              <ul>
                {conflicts.map((conflict) => (
                  <li key={`${conflict.kind}-${conflict.name}-${conflict.interviewId}`}>
                    <strong>
                      {conflict.kind === 'Room' ? `Oda: ${conflict.name}` : conflict.name}
                    </strong>{' '}
                    — {formatTimeRange(conflict.startsAt, conflict.endsAt)},{' '}
                    {conflict.candidateName} ile mülakat
                  </li>
                ))}
              </ul>
            </div>
          </div>
        )}

        <div className="form-grid__wide">
          <BlacklistWarning
            matches={blacklistMatches}
            message="Bu aday kara listedeki bir kayıtla eşleşiyor. Yine de planlarsanız bu kararınız kayda geçer."
          />
        </div>
      </div>
    </Dialog>
  );
}
