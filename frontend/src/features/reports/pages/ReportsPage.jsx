import { Button } from 'primereact/button';
import { Calendar } from 'primereact/calendar';
import { Dropdown } from 'primereact/dropdown';
import { InputText } from 'primereact/inputtext';
import { useState } from 'react';
import { Link } from 'react-router';
import DictionarySelect from '@/shared/components/DictionarySelect';
import PageHeader from '@/shared/components/PageHeader';
import {
  APPLICATION_STATUS,
  BLACKLIST_STATUS,
  HIRING_REQUEST_STATUS,
  INTERVIEW_STATUS,
  POSITION_STATUS,
  toOptions,
} from '@/shared/constants/labels';
import { ROUTES } from '@/shared/constants/routes';
import { usePositions } from '@/features/positions/queries';
import { toIsoDate } from '@/features/dashboard/components/DashboardFilters';
import { downloadListReport } from '../api';
import { useDownload } from '../useDownload';
import '../reports.css';

const CV_REVIEW_STATUS_OPTIONS = [
  { label: 'Karar bekleyen', value: 'Waiting' },
  { label: 'Sonuçlanan', value: 'Decided' },
  { label: 'Geciken', value: 'Overdue' },
];

/**
 * Liste raporları. `byDepartment` false ise birim ve pozisyon filtresi uygulanmaz (kartta belirtilir).
 * `statusOptions` varsa kartta o raporun durumuna göre süzme listesi çıkar.
 */
const REPORTS = [
  {
    kind: 'candidates',
    title: 'Adaylar',
    icon: 'pi pi-users',
    description:
      'İletişim bilgisi, kaynak, yetenekler, ücret beklentisi, açık süreçler ve kara liste durumu.',
    dateNote: 'Tarih: aday kayıt tarihi',
    byDepartment: true,
  },
  {
    kind: 'applications',
    statusOptions: toOptions(APPLICATION_STATUS),
    title: 'Süreçler (başvurular)',
    icon: 'pi pi-sitemap',
    description: 'Her aday-pozisyon süreci: aşama, durum, süre, ret nedeni ve kapanış notu.',
    dateNote: 'Tarih: sürecin başlangıcı',
    byDepartment: true,
  },
  {
    kind: 'hiring-requests',
    statusOptions: toOptions(HIRING_REQUEST_STATUS),
    title: 'Kadro talepleri',
    icon: 'pi pi-file-edit',
    description: 'Talep eden, neden, aciliyet, onay adımları ve sonuçları, aranan yetkinlikler.',
    dateNote: 'Tarih: talep oluşturma',
    byDepartment: true,
  },
  {
    kind: 'positions',
    statusOptions: toOptions(POSITION_STATUS),
    title: 'Pozisyonlar',
    icon: 'pi pi-briefcase',
    description: 'Kadro, dolan ve boş sayısı, durum, sorumlu İK ve aktif süreç sayısı.',
    dateNote: 'Tarih: pozisyon oluşturma',
    byDepartment: true,
  },
  {
    kind: 'interviews',
    statusOptions: toOptions(INTERVIEW_STATUS),
    title: 'Mülakatlar',
    icon: 'pi pi-calendar',
    description: 'Aday, pozisyon, mülakatçılar, yer, durum, değerlendirme kararı ve ortalama puan.',
    dateNote: 'Tarih: mülakat başlangıcı',
    byDepartment: true,
  },
  {
    kind: 'cv-reviews',
    statusOptions: CV_REVIEW_STATUS_OPTIONS,
    title: 'CV değerlendirmeleri',
    icon: 'pi pi-id-card',
    description: 'Öneren, değerlendiren, karar, gerekçe, ret nedeni ve süre.',
    dateNote: 'Tarih: önerilme tarihi',
    byDepartment: true,
  },
  {
    kind: 'blacklist',
    statusOptions: toOptions(BLACKLIST_STATUS),
    title: 'Kara liste',
    icon: 'pi pi-ban',
    description: 'Neden, açıklama, durum, talep eden ve onaylayan, bitiş ve kaldırma bilgisi.',
    dateNote: 'Tarih: kayıt tarihi · Birim ve pozisyon filtresi uygulanmaz',
    byDepartment: false,
  },
];

/**
 * Raporlar: liste raporlarını Excel olarak indirir. Aday dosyası (PDF/Excel) adayın kendi sayfasından alınır.
 * Filtreler bütün kartlara birlikte uygulanır; boş bırakılırsa tüm kayıtlar gelir.
 */
export default function ReportsPage() {
  const [range, setRange] = useState(null);
  const [departmentId, setDepartmentId] = useState(null);
  const [positionId, setPositionId] = useState(null);
  const [searchText, setSearchText] = useState('');
  // Rapor türüne göre seçilen durum süzgeçleri: { [kind]: değer }
  const [statuses, setStatuses] = useState({});
  const [activeKind, setActiveKind] = useState(null);
  const { pending, run } = useDownload();
  const { data: positions } = usePositions({ pageSize: 100 });

  const [start, end] = range ?? [];
  const search = searchText.trim();
  const filters = {
    from: start ? toIsoDate(start) : undefined,
    to: end ? toIsoDate(end) : undefined,
    departmentId: departmentId ?? undefined,
    positionId: positionId ?? undefined,
    search: search || undefined,
  };
  const hasFilters = Boolean(
    range || departmentId || positionId || search || Object.values(statuses).some(Boolean),
  );

  const download = (report) => {
    setActiveKind(report.kind);
    // Birimle ilişkisi olmayan raporlara birim ve pozisyon gönderilmez.
    const { departmentId: department, positionId: position, ...common } = filters;
    return run(() =>
      downloadListReport(report.kind, {
        ...common,
        ...(report.byDepartment ? { departmentId: department, positionId: position } : {}),
        status: statuses[report.kind] ?? undefined,
      }),
    );
  };

  return (
    <>
      <PageHeader
        title="Raporlar"
        subtitle="Kayıtları Excel olarak indirin. Üstteki filtreler bütün raporlara, her kartın durum seçimi yalnızca o rapora uygulanır; boş bırakırsanız tüm kayıtlar gelir. Her ekranda, o ekrana ait rapor düğmesi de bulunur."
      />

      <div className="report-filters" role="group" aria-label="Rapor filtreleri">
        <Calendar
          selectionMode="range"
          value={range}
          dateFormat="dd.mm.yy"
          placeholder="Tüm tarihler"
          showIcon
          showButtonBar
          readOnlyInput
          aria-label="Tarih aralığı"
          onChange={(event) => setRange(event.value ?? null)}
        />
        <DictionarySelect
          type="Department"
          value={departmentId}
          placeholder="Tüm birimler"
          showClear
          onChange={setDepartmentId}
        />
        <Dropdown
          value={positionId}
          options={positions?.items ?? []}
          optionLabel="title"
          optionValue="id"
          placeholder="Tüm pozisyonlar"
          aria-label="Pozisyona göre filtrele"
          showClear
          filter
          filterInputAutoFocus
          onChange={(event) => setPositionId(event.value ?? null)}
        />
        <InputText
          value={searchText}
          onChange={(event) => setSearchText(event.target.value)}
          placeholder="Aday adı veya başlık ara"
          aria-label="Aday adı veya başlık ara"
          className="report-filters__search"
        />
        {hasFilters && (
          <Button
            type="button"
            label="Filtreleri temizle"
            icon="pi pi-filter-slash"
            text
            severity="secondary"
            onClick={() => {
              setRange(null);
              setDepartmentId(null);
              setPositionId(null);
              setSearchText('');
              setStatuses({});
            }}
          />
        )}
      </div>

      <div className="report-grid">
        <section className="card report-card report-card--wide">
          <header className="report-card__header">
            <i className="pi pi-file-pdf" aria-hidden="true" />
            <h2 className="report-card__title">Aday dosyası</h2>
          </header>
          <p className="report-card__text">
            Tek bir adayın sistemdeki tüm kayıtları: profil, ön görüşme formu, süreçler ve aşama
            geçmişi, CV değerlendirmeleri, mülakatlar, puan kartları, kara liste, e-postalar ve
            zaman çizelgesi. PDF ya da Excel olarak alınır.
          </p>
          <p className="cell-sub">Adayın sayfasındaki “Rapor” düğmesinden indirilir.</p>
          <Link to={ROUTES.candidates} className="report-card__link">
            Aday listesine git <i className="pi pi-angle-right" aria-hidden="true" />
          </Link>
        </section>

        {REPORTS.map((report) => (
          <section key={report.kind} className="card report-card">
            <header className="report-card__header">
              <i className={report.icon} aria-hidden="true" />
              <h2 className="report-card__title">{report.title}</h2>
            </header>
            <p className="report-card__text">{report.description}</p>
            <p className="cell-sub">{report.dateNote}</p>
            {report.statusOptions && (
              <Dropdown
                value={statuses[report.kind] ?? null}
                options={report.statusOptions}
                placeholder="Tüm durumlar"
                aria-label={`${report.title} raporu durum filtresi`}
                showClear
                onChange={(event) =>
                  setStatuses((current) => ({ ...current, [report.kind]: event.value ?? null }))
                }
              />
            )}
            <Button
              type="button"
              label="Excel indir"
              icon="pi pi-file-excel"
              outlined
              loading={pending && activeKind === report.kind}
              disabled={pending}
              onClick={() => download(report)}
            />
          </section>
        ))}
      </div>
    </>
  );
}
