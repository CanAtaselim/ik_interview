import { Button } from 'primereact/button';
import { Calendar } from 'primereact/calendar';
import { useState } from 'react';
import { Link } from 'react-router';
import DictionarySelect from '@/shared/components/DictionarySelect';
import PageHeader from '@/shared/components/PageHeader';
import { ROUTES } from '@/shared/constants/routes';
import { toIsoDate } from '@/features/dashboard/components/DashboardFilters';
import { downloadListReport } from '../api';
import { useDownload } from '../useDownload';
import '../reports.css';

/** Liste raporları. `byDepartment` false ise birim filtresi uygulanmaz (kartta belirtilir). */
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
    title: 'Süreçler (başvurular)',
    icon: 'pi pi-sitemap',
    description: 'Her aday-pozisyon süreci: aşama, durum, süre, ret nedeni ve kapanış notu.',
    dateNote: 'Tarih: sürecin başlangıcı',
    byDepartment: true,
  },
  {
    kind: 'hiring-requests',
    title: 'Kadro talepleri',
    icon: 'pi pi-file-edit',
    description: 'Talep eden, neden, aciliyet, onay adımları ve sonuçları, aranan yetkinlikler.',
    dateNote: 'Tarih: talep oluşturma',
    byDepartment: true,
  },
  {
    kind: 'positions',
    title: 'Pozisyonlar',
    icon: 'pi pi-briefcase',
    description: 'Kadro, dolan ve boş sayısı, durum, sorumlu İK ve aktif süreç sayısı.',
    dateNote: 'Tarih: pozisyon oluşturma',
    byDepartment: true,
  },
  {
    kind: 'interviews',
    title: 'Mülakatlar',
    icon: 'pi pi-calendar',
    description: 'Aday, pozisyon, mülakatçılar, yer, durum, değerlendirme kararı ve ortalama puan.',
    dateNote: 'Tarih: mülakat başlangıcı',
    byDepartment: true,
  },
  {
    kind: 'cv-reviews',
    title: 'CV değerlendirmeleri',
    icon: 'pi pi-id-card',
    description: 'Öneren, değerlendiren, karar, gerekçe, ret nedeni ve süre.',
    dateNote: 'Tarih: önerilme tarihi',
    byDepartment: true,
  },
  {
    kind: 'blacklist',
    title: 'Kara liste',
    icon: 'pi pi-ban',
    description: 'Neden, açıklama, durum, talep eden ve onaylayan, bitiş ve kaldırma bilgisi.',
    dateNote: 'Tarih: kayıt tarihi · Birim filtresi uygulanmaz',
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
  const [activeKind, setActiveKind] = useState(null);
  const { pending, run } = useDownload();

  const [start, end] = range ?? [];
  const filters = {
    from: start ? toIsoDate(start) : undefined,
    to: end ? toIsoDate(end) : undefined,
    departmentId: departmentId ?? undefined,
  };
  const hasFilters = Boolean(range || departmentId);

  const download = (kind) => {
    setActiveKind(kind);
    return run(() => downloadListReport(kind, filters));
  };

  return (
    <>
      <PageHeader
        title="Raporlar"
        subtitle="Kayıtları Excel olarak indirin. Filtreler aşağıdaki tüm raporlara uygulanır; boş bırakırsanız tüm kayıtlar gelir."
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
            <Button
              type="button"
              label="Excel indir"
              icon="pi pi-file-excel"
              outlined
              loading={pending && activeKind === report.kind}
              disabled={pending}
              onClick={() => download(report.kind)}
            />
          </section>
        ))}
      </div>
    </>
  );
}
