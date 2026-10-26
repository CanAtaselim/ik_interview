import { Button } from 'primereact/button';
import { Menu } from 'primereact/menu';
import { TabPanel, TabView } from 'primereact/tabview';
import { Tag } from 'primereact/tag';
import { useRef, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router';
import DetailList from '@/shared/components/DetailList';
import PageHeader from '@/shared/components/PageHeader';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { useToast } from '@/shared/components/toast';
import { ROUTES } from '@/shared/constants/routes';
import NotFoundPage from '@/shared/pages/NotFoundPage';
import AddToPositionDialog from '@/shared/recruitment/AddToPositionDialog';
import BlacklistRequestDialog from '@/shared/recruitment/BlacklistRequestDialog';
import CloseApplicationDialog from '@/shared/recruitment/CloseApplicationDialog';
import {
  useCandidateApplications,
  useCvReviews,
  useInterviews,
} from '@/shared/recruitment/queries';
import ScheduleInterviewDialog from '@/shared/recruitment/ScheduleInterviewDialog';
import SuggestCandidateDialog from '@/shared/recruitment/SuggestCandidateDialog';
import { getErrorMessage } from '@/shared/utils/errors';
import { formatDate } from '@/shared/utils/format';
import ApplicationsTab from '../components/ApplicationsTab';
import CandidateFormDialog from '../components/CandidateFormDialog';
import CvReviewsTab from '../components/CvReviewsTab';
import DocumentsTab from '../components/DocumentsTab';
import DuplicateWarning from '../components/DuplicateWarning';
import InterviewsTab from '../components/InterviewsTab';
import ScreeningsTab from '../components/ScreeningsTab';
import TimelineTab from '../components/TimelineTab';
import { useCandidate, useSetTalentPool } from '../queries';
import '../candidates.css';

// Açık sekme adres çubuğunda tutulur; bağlantı doğrudan o sekmeyi açar.
const TABS = [
  'genel',
  'on-gorusme',
  'surecler',
  'cv-degerlendirme',
  'mulakatlar',
  'dosyalar',
  'zaman-cizelgesi',
];
const MAX_REVIEWS = 100;
const APPLICATIONS_TAB = TABS.indexOf('surecler');

const externalLink = (url) =>
  url && (
    <a href={url} target="_blank" rel="noreferrer">
      {url.replace(/^https?:\/\/(www\.)?/i, '')}
    </a>
  );

const countLabel = (label, count) => (count ? `${label} (${count})` : label);

/**
 * Aday kartı: üstte özet, uyarılar ve aksiyonlar; altta sekmeler.
 */
export default function CandidateDetailPage() {
  const { id } = useParams();
  const toast = useToast();
  const moreMenu = useRef(null);
  const [params, setParams] = useSearchParams();
  const { data: candidate, isLoading, isError, error } = useCandidate(id);
  const { data: applications = [] } = useCandidateApplications(id);
  const { data: reviewPage } = useCvReviews({ candidateId: id, pageSize: MAX_REVIEWS });
  const { data: interviews = [] } = useInterviews({ candidateId: id });
  const setTalentPool = useSetTalentPool();
  // Açık pencere: null | 'edit' | 'add-position' | 'suggest' | 'interview' | 'blacklist' | { close: application }
  const [dialog, setDialog] = useState(null);

  if (isLoading) return <TableSkeleton rows={6} />;
  if (isError) {
    return error.response?.status === 404 ? (
      <NotFoundPage />
    ) : (
      <p role="alert">{getErrorMessage(error)}</p>
    );
  }

  const fullName = `${candidate.firstName} ${candidate.lastName}`;
  const activeTab = Math.max(TABS.indexOf(params.get('tab')), 0);
  const openTab = (index) => setParams(index === 0 ? {} : { tab: TABS[index] }, { replace: true });
  const activeApplications = applications.filter((application) => application.status === 'Active');
  const reviews = reviewPage?.items ?? [];

  const toggleTalentPool = () =>
    setTalentPool.mutate(
      { id: candidate.id, inTalentPool: !candidate.inTalentPool },
      {
        onSuccess: () =>
          toast.success(
            candidate.inTalentPool ? 'Aday havuzdan çıkarıldı.' : 'Aday yetenek havuzuna alındı.',
          ),
        onError: (mutationError) => toast.error(getErrorMessage(mutationError)),
      },
    );

  // Tek süreç varsa doğrudan sonlandırma penceresi; birden çoksa hangisi olduğu Süreçler sekmesinden seçilir.
  const closeProcess = () =>
    activeApplications.length === 1
      ? setDialog({ close: activeApplications[0] })
      : openTab(APPLICATIONS_TAB);

  const moreItems = [
    {
      label: candidate.inTalentPool ? 'Havuzdan çıkar' : 'Havuzda beklet',
      icon: candidate.inTalentPool ? 'pi pi-star-fill' : 'pi pi-star',
      command: toggleTalentPool,
    },
    {
      label: 'Kara listeye al',
      icon: 'pi pi-ban',
      disabled: Boolean(candidate.blacklist),
      command: () => setDialog('blacklist'),
    },
  ];

  const contacts = [
    candidate.email && (
      <a key="email" href={`mailto:${candidate.email}`}>
        {candidate.email}
      </a>
    ),
    candidate.phone && (
      <a key="phone" href={`tel:${candidate.phone}`}>
        {candidate.phone}
      </a>
    ),
    candidate.city && <span key="city">{candidate.city}</span>,
  ].filter(Boolean);

  return (
    <>
      <Link to={ROUTES.candidates} className="back-link">
        <i className="pi pi-arrow-left" aria-hidden="true" /> Aday Listesi
      </Link>
      <PageHeader
        title={
          <span className="dialog-title">
            {fullName}
            {candidate.inTalentPool && <Tag value="Havuzda" severity="info" />}
            {candidate.blacklist?.status === 'Active' && (
              <Tag value="Kara listede" severity="danger" />
            )}
          </span>
        }
        actions={
          <>
            {activeApplications.length > 0 && (
              <Button label="Süreci sonlandır" severity="danger" outlined onClick={closeProcess} />
            )}
            <Button label="Öner" icon="pi pi-send" outlined onClick={() => setDialog('suggest')} />
            {activeApplications.length > 0 && (
              <Button
                label="Mülakat planla"
                icon="pi pi-calendar-plus"
                outlined
                onClick={() => setDialog('interview')}
              />
            )}
            <Button label="Düzenle" icon="pi pi-pencil" onClick={() => setDialog('edit')} />
            <Button
              icon="pi pi-ellipsis-v"
              text
              severity="secondary"
              aria-label="Diğer işlemler"
              aria-haspopup="menu"
              onClick={(event) => moreMenu.current.toggle(event)}
            />
            <Menu ref={moreMenu} model={moreItems} popup popupAlignment="right" />
          </>
        }
      />

      <div className="candidate-summary">
        <div className="contact-list">{contacts}</div>
        {candidate.tags.length > 0 && (
          <div className="tag-list">
            {candidate.tags.map((tag) => (
              <Tag key={tag} value={tag} severity="info" />
            ))}
          </div>
        )}
        {candidate.blacklist && (
          <div className="notice notice--danger" role="alert">
            <i className="pi pi-ban" aria-hidden="true" />
            <div>
              {candidate.blacklist.status === 'Active'
                ? 'Bu aday kara listede. '
                : 'Bu aday için onay bekleyen bir kara liste talebi var. '}
              <Link to={ROUTES.blacklistEntry(candidate.blacklist.entryId)}>Kaydı görüntüle</Link>
            </div>
          </div>
        )}
        <DuplicateWarning
          duplicates={candidate.possibleDuplicates}
          message="Aynı e-posta veya telefonla kayıtlı başka aday var. Aynı kişiyse kayıtları tek kartta toplamayı düşünün:"
        />
      </div>

      <section className="card">
        <TabView scrollable activeIndex={activeTab} onTabChange={(event) => openTab(event.index)}>
          <TabPanel header="Genel">
            <div className="detail-sections">
              <DetailList
                items={[
                  { label: 'E-posta', value: candidate.email },
                  { label: 'Telefon', value: candidate.phone },
                  { label: 'Şehir', value: candidate.city },
                  { label: 'Kaynak', value: candidate.sourceName },
                  { label: 'LinkedIn', value: externalLink(candidate.linkedInUrl) },
                  { label: 'GitHub', value: externalLink(candidate.gitHubUrl) },
                  { label: 'Eklenme', value: formatDate(candidate.createdAt) },
                ]}
              />
              <section>
                <h2 className="section-title">Notlar</h2>
                {candidate.notes ? (
                  <p className="multiline">{candidate.notes}</p>
                ) : (
                  <p className="muted">Not yok. “Düzenle” ile ekleyebilirsiniz.</p>
                )}
              </section>
            </div>
          </TabPanel>
          <TabPanel header={countLabel('Ön Görüşme', candidate.screenings.length)}>
            <ScreeningsTab candidate={candidate} />
          </TabPanel>
          <TabPanel header={countLabel('Süreçler', applications.length)}>
            <ApplicationsTab
              applications={applications}
              onAdd={() => setDialog('add-position')}
              onClose={(application) => setDialog({ close: application })}
            />
          </TabPanel>
          <TabPanel header={countLabel('CV Değerlendirme', reviews.length)}>
            <CvReviewsTab reviews={reviews} onSuggest={() => setDialog('suggest')} />
          </TabPanel>
          <TabPanel header={countLabel('Mülakatlar', interviews.length)}>
            <InterviewsTab
              interviews={interviews}
              canSchedule={activeApplications.length > 0}
              onSchedule={() => setDialog('interview')}
            />
          </TabPanel>
          <TabPanel header={countLabel('Dosyalar', candidate.documents.length)}>
            <DocumentsTab candidate={candidate} />
          </TabPanel>
          <TabPanel header="Zaman Çizelgesi">
            <TimelineTab candidateId={candidate.id} />
          </TabPanel>
        </TabView>
      </section>

      {dialog === 'edit' && (
        <CandidateFormDialog
          candidate={candidate}
          onClose={() => setDialog(null)}
          onSaved={() => setDialog(null)}
        />
      )}
      {dialog === 'add-position' && (
        <AddToPositionDialog
          candidate={{ id: candidate.id, name: fullName }}
          onClose={() => setDialog(null)}
        />
      )}
      {dialog === 'suggest' && (
        <SuggestCandidateDialog
          candidate={{ id: candidate.id, name: fullName }}
          applications={activeApplications}
          onClose={() => setDialog(null)}
        />
      )}
      {dialog === 'interview' && (
        <ScheduleInterviewDialog
          applications={activeApplications}
          candidateName={fullName}
          onClose={() => setDialog(null)}
        />
      )}
      {dialog === 'blacklist' && (
        <BlacklistRequestDialog
          candidate={{ id: candidate.id, name: fullName }}
          onClose={() => setDialog(null)}
        />
      )}
      {dialog?.close && (
        <CloseApplicationDialog
          application={{
            id: dialog.close.id,
            candidateName: fullName,
            positionTitle: dialog.close.positionTitle,
          }}
          onClose={() => setDialog(null)}
          onClosed={({ openBlacklistRequest }) =>
            setDialog(openBlacklistRequest ? 'blacklist' : null)
          }
        />
      )}
    </>
  );
}
