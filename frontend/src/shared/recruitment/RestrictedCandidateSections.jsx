import { Button } from 'primereact/button';
import { Tag } from 'primereact/tag';
import DetailList from '@/shared/components/DetailList';
import { useToast } from '@/shared/components/toast';
import {
  LANGUAGE_LEVEL_LABELS,
  MILITARY_STATUS_LABELS,
  WORK_MODEL_LABELS,
} from '@/shared/constants/labels';
import { getErrorMessage } from '@/shared/utils/errors';
import { openOrDownload } from '@/shared/utils/openDocument';
import { formatDate, formatDateTime, formatFileSize, formatMonths } from '@/shared/utils/format';
import './RestrictedCandidateSections.css';

const externalLink = (url) =>
  url && (
    <a href={url} target="_blank" rel="noreferrer">
      {url.replace(/^https?:\/\/(www\.)?/i, '')}
    </a>
  );

const yesNo = (value) => (value === null || value === undefined ? null : value ? 'Evet' : 'Hayır');

/**
 * Adayın İK dışı görünümü (CV'nin önerildiği takım lideri, mülakatçı): CV dosyaları, ön görüşme
 * özeti ve temel bilgiler. Ücret, İK yorumu ve iletişim bilgisi sunucudan hiç gelmez.
 * @param {{ candidate: object, getDownloadUrl: (documentId: string) => Promise<string> }} props
 */
export default function RestrictedCandidateSections({ candidate, getDownloadUrl }) {
  const toast = useToast();

  // PDF ve resimler yan sekmede açılır; "indir" düğmesi ve Word dosyaları indirilir.
  const download = async (item, { forceDownload = false } = {}) => {
    try {
      await openOrDownload(() => getDownloadUrl(item.id), forceDownload ? null : item.contentType);
    } catch (error) {
      toast.error(getErrorMessage(error));
    }
  };

  return (
    <>
      <section className="card card--padded" aria-labelledby="candidate-documents">
        <h2 id="candidate-documents" className="section-title">
          CV ve dosyalar
        </h2>
        {candidate.documents.length === 0 ? (
          <p className="muted">Bu aday için yüklenmiş dosya yok.</p>
        ) : (
          <ul className="document-list">
            {candidate.documents.map((item) => (
              <li key={item.id} className="document-list__item">
                <i className="pi pi-file" aria-hidden="true" />
                <div className="document-list__main">
                  <button type="button" className="table-link" onClick={() => download(item)}>
                    {item.fileName}
                  </button>
                  <div className="cell-sub">
                    {[
                      item.typeName,
                      formatFileSize(item.sizeBytes),
                      formatDateTime(item.uploadedAt),
                    ].join(' · ')}
                  </div>
                </div>
                <Button
                  icon="pi pi-download"
                  text
                  severity="secondary"
                  aria-label={`${item.fileName} dosyasını indir`}
                  onClick={() => download(item, { forceDownload: true })}
                />
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="card card--padded" aria-labelledby="candidate-screenings">
        <h2 id="candidate-screenings" className="section-title">
          İK ön görüşme özeti
        </h2>
        {candidate.screenings.length === 0 ? (
          <p className="muted">Bu adayla henüz ön görüşme yapılmamış.</p>
        ) : (
          candidate.screenings.map((screening) => (
            <ScreeningSummary key={screening.id} screening={screening} />
          ))
        )}
      </section>

      <section className="card card--padded" aria-labelledby="candidate-profile">
        <h2 id="candidate-profile" className="section-title">
          Aday
        </h2>
        <div className="detail-sections">
          <DetailList
            items={[
              { label: 'Şehir', value: candidate.city },
              { label: 'LinkedIn', value: externalLink(candidate.linkedInUrl) },
              { label: 'GitHub', value: externalLink(candidate.gitHubUrl) },
            ]}
          />
          {candidate.tags.length > 0 && (
            <div className="tag-list">
              {candidate.tags.map((tag) => (
                <Tag key={tag} value={tag} severity="info" />
              ))}
            </div>
          )}
        </div>
      </section>
    </>
  );
}

/** Ön görüşme kaydının salt okunur özeti. */
function ScreeningSummary({ screening }) {
  const educations = screening.educations
    .map((education) =>
      [education.school, education.department, education.degree, education.graduationYear]
        .filter(Boolean)
        .join(', '),
    )
    .filter(Boolean);
  const languages = screening.languages
    .filter((item) => item.language)
    .map((item) =>
      item.level ? `${item.language} (${LANGUAGE_LEVEL_LABELS[item.level]})` : item.language,
    );

  const texts = [
    { label: 'Deneyim özeti', value: screening.experienceSummary },
    { label: 'Teknik yetkinlikler', value: screening.technicalSkills },
    { label: 'Kişisel yetkinlikler', value: screening.softSkills },
    { label: 'Motivasyon', value: screening.motivation },
    { label: 'Eğitim açıklaması', value: screening.educationNote },
    { label: 'Yabancı dil açıklaması', value: screening.languagesNote },
    { label: 'Lokasyon açıklaması', value: screening.locationNote },
  ].filter((item) => item.value);

  return (
    <article className="restricted-screening">
      <p className="restricted-screening__meta">
        {[
          formatDate(screening.interviewedAt),
          screening.interviewerName && `Görüşen: ${screening.interviewerName}`,
        ]
          .filter(Boolean)
          .join(' · ')}
      </p>
      <div className="detail-sections">
        <DetailList
          items={[
            { label: 'Toplam deneyim', value: formatMonths(screening.totalExperienceMonths) },
            { label: 'Alan deneyimi', value: formatMonths(screening.fieldExperienceMonths) },
            {
              label: 'İhbar süresi',
              value:
                screening.noticePeriodWeeks || screening.noticePeriodWeeks === 0
                  ? `${screening.noticePeriodWeeks} hafta`
                  : null,
            },
            { label: 'Çalışma modeli', value: WORK_MODEL_LABELS[screening.workModel] },
            {
              label: 'Lokasyon',
              value: [screening.locationProvince, screening.locationDistrict]
                .filter(Boolean)
                .join(' / '),
            },
            { label: 'Taşınabilir', value: yesNo(screening.openToRelocation) },
            {
              label: 'Seyahat engeli',
              value:
                screening.hasTravelRestriction === null ||
                screening.hasTravelRestriction === undefined
                  ? null
                  : screening.hasTravelRestriction
                    ? 'Var'
                    : 'Yok',
            },
            { label: 'Askerlik', value: MILITARY_STATUS_LABELS[screening.militaryStatus] },
            { label: 'Eğitim', value: educations.join(' · ') },
            { label: 'Yabancı dil', value: languages.join(' · ') },
          ]}
        />
        {screening.technicalSkillTags.length > 0 && (
          <div className="tag-list">
            {screening.technicalSkillTags.map((tag) => (
              <Tag key={tag} value={tag} severity="info" />
            ))}
          </div>
        )}
        {texts.map((item) => (
          <div key={item.label}>
            <h3 className="section-title">{item.label}</h3>
            <p className="multiline">{item.value}</p>
          </div>
        ))}
      </div>
    </article>
  );
}
