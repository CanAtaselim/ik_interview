import { useQueryClient } from '@tanstack/react-query';
import { Button } from 'primereact/button';
import { Calendar } from 'primereact/calendar';
import { Chips } from 'primereact/chips';
import { Dropdown } from 'primereact/dropdown';
import { InputNumber } from 'primereact/inputnumber';
import { InputText } from 'primereact/inputtext';
import { InputTextarea } from 'primereact/inputtextarea';
import { Rating } from 'primereact/rating';
import { SelectButton } from 'primereact/selectbutton';
import { Controller, useFieldArray, useForm, useWatch } from 'react-hook-form';
import DictionarySelect from '@/shared/components/DictionarySelect';
import FormField from '@/shared/components/FormField';
import MoneyInput from '@/shared/components/MoneyInput';
import MoneyRangeInput from '@/shared/components/MoneyRangeInput';
import {
  LANGUAGE_LEVEL_LABELS,
  MILITARY_STATUS_LABELS,
  SCREENING_CHANNEL_LABELS,
  toOptions,
  WORK_MODEL_LABELS,
} from '@/shared/constants/labels';
import { districtsOf, PROVINCES } from '@/shared/data/turkeyLocations';
import { useAutosave } from '@/shared/hooks/useAutosave';
import { getErrorMessage } from '@/shared/utils/errors';
import { candidateKeys, useSaveScreening } from '../queries';
import { toScreeningForm, toScreeningInput } from '../schemas';
import SchoolInput from './SchoolInput';

const CHANNEL_OPTIONS = toOptions(SCREENING_CHANNEL_LABELS);
const LEVEL_OPTIONS = toOptions(LANGUAGE_LEVEL_LABELS);
const WORK_MODEL_OPTIONS = toOptions(WORK_MODEL_LABELS);
const MILITARY_OPTIONS = toOptions(MILITARY_STATUS_LABELS);
const YES_NO_OPTIONS = [
  { label: 'Evet', value: true },
  { label: 'Hayır', value: false },
];
const TRAVEL_RESTRICTION_OPTIONS = [
  { label: 'Yok', value: false },
  { label: 'Var', value: true },
];

const timeFormat = new Intl.DateTimeFormat('tr-TR', { hour: '2-digit', minute: '2-digit' });

/**
 * İK ön görüşme formu. Hiçbir alan zorunlu değildir; İK istediği kadarını, istediği zaman doldurur.
 * Kaydet butonu yoktur: yazmayı bıraktıktan kısa süre sonra kendiliğinden kaydedilir.
 * @param {{ candidateId: string, screening: object }} props
 */
export default function ScreeningForm({ candidateId, screening }) {
  const queryClient = useQueryClient();
  const saveScreening = useSaveScreening();
  const { register, control, setValue } = useForm({ defaultValues: toScreeningForm(screening) });
  const educations = useFieldArray({ control, name: 'educations' });
  const languages = useFieldArray({ control, name: 'languages' });
  const values = useWatch({ control });
  const province = values.locationProvince ?? null;

  const autosave = useAutosave({
    value: toScreeningInput(values),
    save: async (input) => {
      await saveScreening.mutateAsync({ id: candidateId, screeningId: screening.id, input });
      // Kart başlığındaki özet (tarih, puan) güncellensin.
      queryClient.invalidateQueries({ queryKey: candidateKeys.detail(candidateId) });
    },
  });

  const id = (name) => `screening-${screening.id}-${name}`;

  const textArea = (name, label, placeholder, rows = 3) => (
    <div className="form-grid__wide">
      <FormField label={label} htmlFor={id(name)}>
        <InputTextarea
          id={id(name)}
          rows={rows}
          autoResize
          placeholder={placeholder}
          {...register(name)}
        />
      </FormField>
    </div>
  );

  const selectButton = (name, label, options) => (
    <FormField label={label} htmlFor={id(name)}>
      <Controller
        name={name}
        control={control}
        render={({ field }) => (
          <SelectButton
            id={id(name)}
            value={field.value}
            options={options}
            onChange={(event) => field.onChange(event.value ?? null)}
          />
        )}
      />
    </FormField>
  );

  const number = (name, label, props) => (
    <FormField label={label} htmlFor={id(name)}>
      <Controller
        name={name}
        control={control}
        render={({ field }) => (
          <InputNumber
            inputId={id(name)}
            value={field.value}
            onValueChange={(event) => field.onChange(event.value)}
            min={0}
            locale="tr-TR"
            {...props}
          />
        )}
      />
    </FormField>
  );

  const experience = (label, yearsName, monthsName) => (
    <FormField label={label} htmlFor={id(yearsName)}>
      <div className="experience-input">
        <Controller
          name={yearsName}
          control={control}
          render={({ field }) => (
            <InputNumber
              inputId={id(yearsName)}
              value={field.value}
              onValueChange={(event) => field.onChange(event.value)}
              min={0}
              max={70}
              suffix=" yıl"
              placeholder="Yıl"
              aria-label={`${label}, yıl`}
            />
          )}
        />
        <Controller
          name={monthsName}
          control={control}
          render={({ field }) => (
            <InputNumber
              value={field.value}
              onValueChange={(event) => field.onChange(event.value)}
              min={0}
              max={11}
              suffix=" ay"
              placeholder="Ay"
              aria-label={`${label}, ay`}
            />
          )}
        />
      </div>
    </FormField>
  );

  const money = (name, label) => (
    <FormField label={label} htmlFor={id(name)}>
      <Controller
        name={name}
        control={control}
        render={({ field }) => (
          <MoneyInput inputId={id(name)} value={field.value} onChange={field.onChange} />
        )}
      />
    </FormField>
  );

  return (
    <form className="screening-form" onSubmit={(event) => event.preventDefault()} noValidate>
      <p className="autosave-status" data-status={autosave.status} role="status">
        {autosave.status === 'saving' && 'Kaydediliyor…'}
        {autosave.status === 'saved' && `Kaydedildi · ${timeFormat.format(autosave.savedAt)}`}
        {autosave.status === 'error' && `Kaydedilemedi: ${getErrorMessage(autosave.error)}`}
        {autosave.status === 'idle' &&
          'Yazdıklarınız kendiliğinden kaydedilir; hiçbir alan zorunlu değildir.'}
      </p>

      <section className="form-section">
        <h3 className="section-title">Görüşme</h3>
        <div className="form-grid">
          {selectButton('channel', 'Görüşme şekli', CHANNEL_OPTIONS)}
          <FormField label="Görüşme tarihi" htmlFor={id('interviewedAt')}>
            <Controller
              name="interviewedAt"
              control={control}
              render={({ field }) => (
                <Calendar
                  inputId={id('interviewedAt')}
                  value={field.value}
                  onChange={(event) => field.onChange(event.value ?? null)}
                  dateFormat="dd.mm.yy"
                  hourFormat="24"
                  showTime
                  showIcon
                  showButtonBar
                />
              )}
            />
          </FormField>
          <FormField label="Genel puan" htmlFor={id('overallRating')}>
            <Controller
              name="overallRating"
              control={control}
              render={({ field }) => (
                <Rating
                  id={id('overallRating')}
                  value={field.value ?? 0}
                  onChange={(event) => field.onChange(event.value ?? null)}
                  cancel
                />
              )}
            />
          </FormField>
        </div>
      </section>

      <section className="form-section">
        <h3 className="section-title">Deneyim</h3>
        <div className="form-grid">
          {experience('Toplam iş deneyimi', 'experienceYears', 'experienceMonths')}
          {experience('Toplam alan deneyimi', 'fieldYears', 'fieldMonths')}
          {textArea(
            'experienceSummary',
            'Deneyim özeti',
            'Çalıştığı şirketler, roller, öne çıkan işler',
          )}
          {textArea('motivation', 'Motivasyonu', 'Neden iş değiştirmek istiyor, ne arıyor?')}
        </div>
      </section>

      <section className="form-section">
        <h3 className="section-title">Yetkinlikler</h3>
        <div className="form-grid">
          <div className="form-grid__wide">
            <FormField label="Teknik yetkinlik etiketleri" htmlFor={id('technicalSkillTags')}>
              <Controller
                name="technicalSkillTags"
                control={control}
                render={({ field }) => (
                  <Chips
                    inputId={id('technicalSkillTags')}
                    value={field.value}
                    onChange={(event) => field.onChange(event.value ?? [])}
                    separator=","
                    addOnBlur
                    placeholder={
                      field.value.length === 0 ? 'Yazıp Enter’a basın (örn. Java, Kafka)' : ''
                    }
                  />
                )}
              />
            </FormField>
          </div>
          {textArea(
            'technicalSkills',
            'Teknik yetkinlik',
            'Teknik bilgi ve derinlik hakkında notlar',
          )}
          {textArea('softSkills', 'Beşeri yetkinlik', 'İletişim, ekip çalışması, liderlik…')}
        </div>
      </section>

      <section className="form-section">
        <h3 className="section-title">Eğitim</h3>
        {educations.fields.map((field, index) => (
          <div key={field.id} className="row-fields row-fields--education">
            <Controller
              name={`educations.${index}.school`}
              control={control}
              render={({ field: school }) => (
                <SchoolInput value={school.value} onChange={school.onChange} />
              )}
            />
            <InputText
              placeholder="Bölüm"
              aria-label="Bölüm"
              {...register(`educations.${index}.department`)}
            />
            <Controller
              name={`educations.${index}.degree`}
              control={control}
              render={({ field: degree }) => (
                <DictionarySelect
                  type="EducationLevel"
                  valueBy="name"
                  placeholder="Eğitim seviyesi"
                  value={degree.value}
                  onChange={(name) => degree.onChange(name ?? '')}
                  showClear
                />
              )}
            />
            <Controller
              name={`educations.${index}.graduationYear`}
              control={control}
              render={({ field: year }) => (
                <InputNumber
                  value={year.value}
                  onValueChange={(event) => year.onChange(event.value)}
                  useGrouping={false}
                  min={1950}
                  max={2100}
                  placeholder="Mezuniyet yılı"
                  aria-label="Mezuniyet yılı"
                />
              )}
            />
            <Button
              type="button"
              icon="pi pi-trash"
              text
              severity="secondary"
              aria-label="Eğitim satırını sil"
              onClick={() => educations.remove(index)}
            />
          </div>
        ))}
        <Button
          type="button"
          label="Eğitim ekle"
          icon="pi pi-plus"
          text
          onClick={() =>
            educations.append({ school: '', department: '', degree: '', graduationYear: null })
          }
        />
        <div className="form-grid">
          {textArea(
            'educationNote',
            'Eğitim açıklaması (isteğe bağlı)',
            'Devam eden eğitim, sertifikalar, notlar',
            2,
          )}
        </div>
      </section>

      <section className="form-section">
        <h3 className="section-title">Yabancı dil</h3>
        {languages.fields.map((field, index) => (
          <div key={field.id} className="row-fields row-fields--language">
            <InputText
              placeholder="Dil"
              aria-label="Dil"
              {...register(`languages.${index}.language`)}
            />
            <Controller
              name={`languages.${index}.level`}
              control={control}
              render={({ field: level }) => (
                <Dropdown
                  value={level.value}
                  options={LEVEL_OPTIONS}
                  onChange={(event) => level.onChange(event.value ?? null)}
                  placeholder="Seviye"
                  aria-label="Seviye"
                  showClear
                />
              )}
            />
            <Button
              type="button"
              icon="pi pi-trash"
              text
              severity="secondary"
              aria-label="Dil satırını sil"
              onClick={() => languages.remove(index)}
            />
          </div>
        ))}
        <Button
          type="button"
          label="Dil ekle"
          icon="pi pi-plus"
          text
          onClick={() => languages.append({ language: '', level: null })}
        />
        <div className="form-grid">
          {textArea(
            'languagesNote',
            'Yabancı dil açıklaması (isteğe bağlı)',
            'Sertifika, konuşma düzeyi, iş ortamında kullanım…',
            2,
          )}
        </div>
      </section>

      <section className="form-section">
        <h3 className="section-title">Koşullar</h3>
        <div className="form-grid">
          <FormField label="Ücret beklentisi" htmlFor={id('expectedSalary')}>
            <Controller
              name="expectedSalary"
              control={control}
              render={({ field: min }) => (
                <Controller
                  name="expectedSalaryMax"
                  control={control}
                  render={({ field: max }) => (
                    <MoneyRangeInput
                      inputId={id('expectedSalary')}
                      value={min.value}
                      max={max.value}
                      onChange={min.onChange}
                      onMaxChange={max.onChange}
                    />
                  )}
                />
              )}
            />
          </FormField>
          {money('currentSalary', 'Mevcut ücret')}
          {number('noticePeriodWeeks', 'İhbar süresi', { max: 104, suffix: ' hafta' })}
          {selectButton('workModel', 'Çalışma şekli tercihi', WORK_MODEL_OPTIONS)}
          <FormField label="Askerlik durumu" htmlFor={id('militaryStatus')}>
            <Controller
              name="militaryStatus"
              control={control}
              render={({ field }) => (
                <Dropdown
                  inputId={id('militaryStatus')}
                  value={field.value}
                  options={MILITARY_OPTIONS}
                  onChange={(event) => field.onChange(event.value ?? null)}
                  placeholder="Seçin"
                  showClear
                />
              )}
            />
          </FormField>
        </div>
      </section>

      <section className="form-section">
        <h3 className="section-title">Lokasyon</h3>
        <div className="form-grid">
          <FormField label="İl" htmlFor={id('locationProvince')}>
            <Controller
              name="locationProvince"
              control={control}
              render={({ field }) => (
                <Dropdown
                  inputId={id('locationProvince')}
                  value={field.value}
                  options={PROVINCES}
                  onChange={(event) => {
                    field.onChange(event.value ?? null);
                    // İl değişince önceki ilin ilçesi geçersiz olur.
                    setValue('locationDistrict', null);
                  }}
                  placeholder="İl seçin"
                  filter
                  filterInputAutoFocus
                  filterLocale="tr"
                  showClear
                />
              )}
            />
          </FormField>
          <FormField label="İlçe" htmlFor={id('locationDistrict')}>
            <Controller
              name="locationDistrict"
              control={control}
              render={({ field }) => (
                <Dropdown
                  inputId={id('locationDistrict')}
                  value={field.value}
                  options={districtsOf(province)}
                  onChange={(event) => field.onChange(event.value ?? null)}
                  placeholder={province ? 'İlçe seçin' : 'Önce il seçin'}
                  disabled={!province}
                  filter
                  filterInputAutoFocus
                  filterLocale="tr"
                  showClear
                />
              )}
            />
          </FormField>
          {selectButton('openToRelocation', 'Taşınmaya açık mı?', YES_NO_OPTIONS)}
          {selectButton('hasTravelRestriction', 'Seyahat engeli', TRAVEL_RESTRICTION_OPTIONS)}
          {textArea(
            'locationNote',
            'Lokasyon açıklaması (isteğe bağlı)',
            'Taşınma planı, ulaşım durumu, notlar',
            2,
          )}
        </div>
      </section>

      <section className="form-section">
        <h3 className="section-title">İK yorumu</h3>
        <div className="form-grid">
          {textArea(
            'hrComment',
            'Genel değerlendirme',
            'Yalnızca İK görür; takım liderine önerilirken paylaşılmaz.',
            4,
          )}
        </div>
      </section>
    </form>
  );
}
