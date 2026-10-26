// Tüm işe alım senaryosunu işletecek örnek veriyi üretir. Her kayıt uygulamanın kendi API'sinden geçer
// (iş kuralları ve bildirimler çalışır). Üretilenlerin kimlikleri .state.json'a yazılır; cleanup.mjs
// yalnızca bunları siler. Kullanım: node tools/demo-data/seed.mjs
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { login, quote, sleep, sql } from './lib.mjs';

const STATE_FILE = join(dirname(fileURLToPath(import.meta.url)), '.state.json');
if (existsSync(STATE_FILE)) {
  console.error('Örnek veri zaten yüklü görünüyor (.state.json var). Önce: node tools/demo-data/cleanup.mjs');
  process.exit(1);
}

const state = {
  candidates: [], positions: [], hiringRequests: [], questions: [], templates: [], interviews: [],
  blacklistEntries: [], availability: [], dictionaryItems: [], applications: [], cvReviews: [],
};
const save = () => writeFileSync(STATE_FILE, JSON.stringify(state, null, 2));
const log = (message) => console.log(`  ${message}`);

// ---- Zaman yardımcıları (Türkiye saati sabit UTC+3) ----
const istanbulNow = new Date(Date.now() + 3 * 3600_000);
const businessDay = (offset) => {
  const day = new Date(Date.UTC(istanbulNow.getUTCFullYear(), istanbulNow.getUTCMonth(), istanbulNow.getUTCDate()));
  let remaining = offset;
  while (remaining > 0) {
    day.setUTCDate(day.getUTCDate() + 1);
    if (![0, 6].includes(day.getUTCDay())) remaining -= 1;
  }
  return day;
};
const at = (day, hour, minute = 0) => new Date(day.getTime() + (hour - 3) * 3600_000 + minute * 60_000).toISOString();

// ---- Küçük bir PDF üretici (CV metni arama ve önizleme için gerçek PDF olmalı) ----
function makePdf(lines) {
  const ascii = (text) =>
    text.replace(/[çÇ]/g, (c) => (c === 'ç' ? 'c' : 'C')).replace(/[ğĞ]/g, (c) => (c === 'ğ' ? 'g' : 'G'))
      .replace(/[ıİ]/g, (c) => (c === 'ı' ? 'i' : 'I')).replace(/[öÖ]/g, (c) => (c === 'ö' ? 'o' : 'O'))
      .replace(/[şŞ]/g, (c) => (c === 'ş' ? 's' : 'S')).replace(/[üÜ]/g, (c) => (c === 'ü' ? 'u' : 'U'))
      .replace(/[()\\]/g, '');
  const content = ['BT', '/F1 12 Tf', '50 780 Td', '16 TL', ...lines.map((line) => `(${ascii(line)}) Tj T*`), 'ET'].join('\n');
  const objects = [
    '<< /Type /Catalog /Pages 2 0 R >>',
    '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
    '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>',
    `<< /Length ${Buffer.byteLength(content)} >>\nstream\n${content}\nendstream`,
    '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
  ];
  let pdf = '%PDF-1.4\n';
  const offsets = [];
  objects.forEach((body, index) => {
    offsets.push(Buffer.byteLength(pdf));
    pdf += `${index + 1} 0 obj\n${body}\nendobj\n`;
  });
  const xref = Buffer.byteLength(pdf);
  pdf += `xref\n0 ${objects.length + 1}\n0000000000 65535 f \n`;
  offsets.forEach((offset) => (pdf += `${String(offset).padStart(10, '0')} 00000 n \n`));
  pdf += `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`;
  return Buffer.from(pdf, 'latin1');
}

// ---- Giriş ----
console.log('Kullanıcılar giriş yapıyor…');
const hrManager = await login('ikyonetici'); // Ayşe Yılmaz, İK Yöneticisi
const hr = await login('ikuzman'); // Mehmet Demir, İK Uzmanı
const manager = await login('mudur'); // Ali Çelik, Departman Yöneticisi
const lead = await login('takimlideri'); // Zeynep Kaya, Takım Lideri
await login('yonetim');

try {
  // ---- Sözlükler ----
  console.log('Sözlükler');
  const items = (type) => hrManager.get(`/dictionaries/${type}/items?includeInactive=true`);
  const idOf = async (type, name) => {
    const found = (await items(type)).find((item) => item.name === name);
    if (!found) throw new Error(`Sözlükte yok: ${type} / ${name}`);
    return found.id;
  };
  const addItem = async (type, name) => {
    const existing = (await items(type)).find((item) => item.name === name);
    if (existing) return existing.id;
    const { id } = await hrManager.post(`/dictionaries/${type}/items`, { name });
    state.dictionaryItems.push(id);
    return id;
  };
  const sourceLinkedIn = await addItem('CandidateSource', 'LinkedIn');
  const sourceKariyer = await addItem('CandidateSource', 'Kariyer.net');
  const sourceReferral = await addItem('CandidateSource', 'Çalışan referansı');
  const roomA = await addItem('Room', 'Toplantı Odası A');
  await addItem('Room', 'Toplantı Odası B');
  save();

  const department = await idOf('Department', 'Yazılım Geliştirme');
  // Seviye adları yönetim ekranından değiştirilebilir (ör. L1, L2); adlara değil sıraya güveniriz.
  const levels = (await items('Level')).filter((item) => item.isActive).sort((a, b) => a.sortOrder - b.sortOrder);
  if (levels.length === 0) throw new Error('Aktif seviye yok; Yönetim → Sözlükler → Seviyeler\'e ekleyin.');
  const level = { mid: levels[0].id, senior: levels[levels.length - 1].id, lead: levels[levels.length - 1].id };
  const type = { phone: await idOf('InterviewType', 'Telefon'), technical: await idOf('InterviewType', 'Teknik'), manager: await idOf('InterviewType', 'Yönetici') };
  const reject = { technical: await idOf('RejectionReason', 'Teknik yetkinlik yetersiz'), withdrew: await idOf('RejectionReason', 'Aday süreçten çekildi') };
  const blacklistReason = { noShow: await idOf('BlacklistReason', 'Mülakata haber vermeden gelmedi'), false: await idOf('BlacklistReason', 'Yanlış/yanıltıcı bilgi verdi') };
  const competency = {};
  for (const name of ['Teknik bilgi', 'Problem çözme', 'Sistem tasarımı', 'İletişim', 'Takım çalışması', 'Sorumluluk alma', 'Öğrenme isteği']) competency[name] = await idOf('Competency', name);

  // ---- Kadro talepleri ----
  console.log('Kadro talepleri');
  const request = (title, extra = {}) => ({
    title: `${title} (Demo)`, departmentId: department, levelId: level.senior, headcount: 1, reason: 'NewHeadcount',
    replacedEmployeeName: null, urgency: 'Normal', expectedStartDate: businessDay(40).toISOString().slice(0, 10), competencies: ['C#', 'PostgreSQL'],
    description: 'Demo senaryosu için açılmış örnek talep.', ...extra,
  });
  const newRequest = async (input) => {
    // Pozisyon adı "Pozisyon adları" listesinden seçilir; demo adlarını listeye ekleriz.
    await addItem('PositionTitle', input.title);
    const { id } = await lead.post('/hiring-requests', input);
    state.hiringRequests.push(id);
    return id;
  };
  await newRequest(request('Kıdemli Backend Geliştirici', { description: 'Taslak: henüz onaya gönderilmedi.' }));
  log('Taslak: Kıdemli Backend Geliştirici');

  const awaitingManager = await newRequest(request('Frontend Geliştirici', { levelId: level.mid, urgency: 'High', competencies: ['React', 'JavaScript'] }));
  await lead.post(`/hiring-requests/${awaitingManager}/submit`);
  log('Departman yöneticisi onayı bekliyor: Frontend Geliştirici');

  const awaitingHr = await newRequest(request('Veri Mühendisi', { reason: 'Replacement', replacedEmployeeName: 'Örnek Ayrılan Çalışan', competencies: ['SQL', 'ETL'] }));
  await lead.post(`/hiring-requests/${awaitingHr}/submit`);
  await manager.post(`/hiring-requests/${awaitingHr}/approve`, { note: 'Bütçe uygun, onaylıyorum.' });
  log('İK yöneticisi onayı bekliyor: Veri Mühendisi');

  const rejected = await newRequest(request('UX Tasarımcı', { levelId: level.mid, urgency: 'Low' }));
  await lead.post(`/hiring-requests/${rejected}/submit`);
  await manager.post(`/hiring-requests/${rejected}/reject`, { reason: 'Bu çeyrekte tasarım kadrosu açılmayacak.' });
  log('Reddedilmiş: UX Tasarımcı');

  const approved = await newRequest(request('QA Mühendisi', { levelId: level.mid, competencies: ['Test otomasyonu'] }));
  await lead.post(`/hiring-requests/${approved}/submit`);
  await manager.post(`/hiring-requests/${approved}/approve`, { note: 'Uygun.' });
  await hrManager.post(`/hiring-requests/${approved}/approve`, { note: 'Kadro açılabilir.' });
  log('Tamamen onaylı: QA Mühendisi (pozisyon kendiliğinden açılır)');
  save();

  // ---- Pozisyonlar ----
  console.log('Pozisyonlar');
  const createPosition = async (title, extra = {}) => {
    const { id } = await hr.post('/positions', { title: `${title} (Demo)`, departmentId: department, levelId: level.senior, headcount: 2, ...extra });
    state.positions.push(id);
    return id;
  };
  const mainPosition = await createPosition('Backend Geliştirici', { description: 'C# / .NET ile servis geliştirme, PostgreSQL, mikroservis deneyimi.' });
  const holdPosition = await createPosition('DevOps Mühendisi', { headcount: 1 });
  await hr.post(`/positions/${holdPosition}/status`, { status: 'OnHold' });
  log('Backend Geliştirici (Demo) [Açık, 2 kişi], DevOps Mühendisi (Demo) [Beklemede]');
  save();

  // ---- Soru bankası ve şablonlar ----
  console.log('Soru bankası ve şablonlar');
  const question = async (competencyName, kind, difficulty, weight, text, guidance) => {
    const { id } = await hrManager.post('/questions', {
      departmentId: department, positionTitle: 'Backend Geliştirici (Demo)', levelId: level.senior,
      competencyId: competency[competencyName], type: kind, difficulty, weight, text, guidance,
    });
    state.questions.push(id);
    return id;
  };
  const technicalQuestions = [
    await question('Teknik bilgi', 'Technical', 1, 2, 'Async/await ve Task kullanımını, deadlock riskini bir örnekle anlatın.', 'ConfigureAwait, SynchronizationContext, .Result/.Wait() sorunlarını bilmeli.'),
    await question('Teknik bilgi', 'Technical', 2, 1.5, 'EF Core\'da N+1 sorgu sorununu nasıl tespit eder ve çözersiniz?', 'Include/split query, projeksiyon, sorgu günlüğü.'),
    await question('Problem çözme', 'Case', 3, 2, 'Saniyede 5000 istek alan bir uç noktanın yavaşladığını varsayın; nereden başlarsınız?', 'Ölçüm, darboğaz, önbellek, veritabanı, ölçekleme sırasını sorgulayın.'),
    await question('Sistem tasarımı', 'Technical', 3, 2, 'Bir bildirim servisini (e-posta + anlık) ölçeklenebilir olacak şekilde tasarlayın.', 'Kuyruk, tekrar deneme, idempotency, outbox.'),
  ];
  const behavioralQuestions = [
    await question('İletişim', 'Behavioral', 2, 1, 'Teknik olmayan bir paydaşa karmaşık bir konuyu anlattığınız bir durumu paylaşın.', 'STAR yapısında somut örnek beklenir.'),
    await question('Takım çalışması', 'Behavioral', 2, 1, 'Takımda bir görüş ayrılığını nasıl çözdünüz?', 'Dinleme, ortak hedef, sonuç.'),
    await question('Sorumluluk alma', 'Behavioral', 3, 1.5, 'Kendi hatanız yüzünden üretimde yaşanan bir sorunu anlatın.', 'Hatayı sahiplenme ve süreç iyileştirme.'),
  ];
  const makeTemplate = async (name, questionIds, interviewTypeId) => {
    const { id } = await hrManager.post('/interview-templates', {
      name, departmentId: department, levelId: level.senior, interviewTypeId, questionIds,
    });
    state.templates.push(id);
    return id;
  };
  const technicalTemplate = await makeTemplate('Backend Teknik Mülakat (Demo)', technicalQuestions, type.technical);
  const managerTemplate = await makeTemplate('Yönetici Mülakatı (Demo)', behavioralQuestions, type.manager);
  await hr.put(`/positions/${mainPosition}/interview-templates`, { templateIds: [technicalTemplate, managerTemplate] });
  log('7 soru, 2 şablon; şablonlar Backend Geliştirici (Demo) pozisyonuna bağlandı');
  save();

  // ---- Adaylar ----
  console.log('Adaylar');
  const profiles = {
    elif: { firstName: 'Elif', lastName: 'Kaya', city: 'İstanbul', sourceId: sourceLinkedIn, tags: ['demo', 'C#', '.NET', 'PostgreSQL'], years: 6, salary: 160000, role: 'Backend geliştirici' },
    burak: { firstName: 'Burak', lastName: 'Aydın', city: 'Ankara', sourceId: sourceKariyer, tags: ['demo', 'Java', 'Spring'], years: 8, salary: 185000, role: 'Kıdemli yazılımcı' },
    selin: { firstName: 'Selin', lastName: 'Arslan', city: 'İzmir', sourceId: sourceReferral, tags: ['demo', 'C#', 'Azure', 'Kafka'], years: 7, salary: 190000, role: 'Backend geliştirici' },
    mert: { firstName: 'Mert', lastName: 'Yıldız', city: 'İstanbul', sourceId: sourceLinkedIn, tags: ['demo', 'C#', 'Docker'], years: 5, salary: 150000, role: 'Yazılım mühendisi' },
    deniz: { firstName: 'Deniz', lastName: 'Çelik', city: 'Bursa', sourceId: sourceKariyer, tags: ['demo', 'C#', 'SQL Server', 'Redis'], years: 9, salary: 210000, role: 'Takım lideri' },
    ahmet: { firstName: 'Ahmet', lastName: 'Şen', city: 'Ankara', sourceId: sourceKariyer, tags: ['demo', 'PHP'], years: 3, salary: 90000, role: 'Web geliştirici' },
    gizem: { firstName: 'Gizem', lastName: 'Polat', city: 'İstanbul', sourceId: sourceLinkedIn, tags: ['demo', 'C#'], years: 4, salary: 120000, role: 'Yazılım geliştirici' },
    hakan: { firstName: 'Hakan', lastName: 'Özkan', city: 'Kocaeli', sourceId: sourceKariyer, tags: ['demo', 'C#', 'Docker'], years: 6, salary: 170000, role: 'Backend geliştirici' },
    kemal: { firstName: 'Kemal', lastName: 'Demir', city: 'İstanbul', sourceId: sourceKariyer, tags: ['demo'], years: 5, salary: 140000, role: 'Yazılım geliştirici' },
    oguz: { firstName: 'Oğuz', lastName: 'Tan', city: 'Antalya', sourceId: sourceKariyer, tags: ['demo'], years: 4, salary: 130000, role: 'Yazılım geliştirici' },
    zehra: { firstName: 'Zehra', lastName: 'Koç', city: 'İstanbul', sourceId: sourceReferral, tags: ['demo', 'C#', 'Yetenek havuzu'], years: 10, salary: 230000, role: 'Mimari / Lead' },
  };
  const emailOf = (profile, index) => `${profile.firstName}.${profile.lastName}`.toLowerCase()
    .replace(/[çÇ]/g, 'c').replace(/[ğĞ]/g, 'g').replace(/[ıİ]/g, 'i').replace(/[öÖ]/g, 'o').replace(/[şŞ]/g, 's').replace(/[üÜ]/g, 'u') + '@ornek.com';
  const candidates = {};
  let phoneCounter = 1;
  for (const [key, profile] of Object.entries(profiles)) {
    const { id } = await hr.post('/candidates', {
      firstName: profile.firstName, lastName: profile.lastName, email: emailOf(profile), phone: `0555 000 00 ${String(phoneCounter++).padStart(2, '0')}`,
      linkedInUrl: `https://www.linkedin.com/in/${profile.firstName.toLowerCase()}-${profile.lastName.toLowerCase()}-demo`.replace(/[çğıöşü]/g, ''),
      city: profile.city, sourceId: profile.sourceId, tags: profile.tags, notes: `Demo aday. Önceki rol: ${profile.role}.`,
    });
    candidates[key] = id;
    state.candidates.push(id);
  }
  save();
  log(`${Object.keys(candidates).length} aday (hepsinde "demo" etiketi var)`);

  // CV dosyası ve ön görüşme formu
  const screeningFor = async (key, overall, comment) => {
    const profile = profiles[key];
    const { id } = await hr.post(`/candidates/${candidates[key]}/screenings`);
    const district = { İstanbul: 'Kadıköy', Ankara: 'Çankaya', İzmir: 'Karşıyaka', Bursa: 'Nilüfer', Kocaeli: 'İzmit', Antalya: 'Muratpaşa' }[profile.city];
    await hr.put(`/candidates/${candidates[key]}/screenings/${id}`, {
      channel: 'Teams', interviewedAt: at(businessDay(0), 10),
      educations: [{ school: 'Orta Doğu Teknik Üniversitesi', department: 'Bilgisayar Mühendisliği', degree: 'Lisans', graduationYear: 2026 - profile.years - 1 }],
      educationNote: 'Yüksek lisans için başvuru planlıyor.',
      languages: [{ language: 'İngilizce', level: 'Advanced' }], languagesNote: 'Teknik toplantıları İngilizce yürütebiliyor.',
      softSkills: 'İletişimi güçlü, ekip çalışmasına yatkın.',
      technicalSkillTags: profile.tags.filter((tag) => tag !== 'demo' && tag !== 'Yetenek havuzu'), technicalSkills: 'Servis geliştirme, test yazma, CI/CD.',
      experienceSummary: `${profile.years} yıllık deneyim; son rolü ${profile.role}.`, motivation: 'Ürün odaklı bir ekipte büyümek istiyor.',
      totalExperienceMonths: profile.years * 12 + 3, fieldExperienceMonths: Math.max(profile.years - 1, 1) * 12,
      expectedSalary: { amount: profile.salary, currency: 'TRY', isNet: false }, expectedSalaryMax: Math.round(profile.salary * 1.2),
      noticePeriodWeeks: 4, hrComment: comment,
      currentSalary: { amount: Math.round(profile.salary * 0.85), currency: 'TRY', isNet: false },
      workModel: 'Hybrid', locationProvince: profile.city, locationDistrict: district, locationNote: 'Ofise haftada üç gün gelebilir.',
      openToRelocation: false, hasTravelRestriction: false, militaryStatus: 'Done', overallRating: overall,
    });
  };
  for (const [key, rating, comment] of [
    ['elif', 4, 'İletişimi iyi, teknik tarafı güçlü görünüyor. Ücret beklentisi bütçe içinde.'],
    ['burak', 3, 'Deneyimli ama Java ağırlıklı; .NET\'e geçiş isteği var.'],
    ['selin', 5, 'Çok güçlü profil; referansla geldi. Ücret beklentisi üst sınıra yakın.'],
    ['mert', 4, 'Hızlı öğrenen, takım çalışmasına yatkın.'],
    ['deniz', 5, 'Liderlik deneyimi var; teklif aşamasında ücret pazarlığı olabilir.'],
    ['hakan', 3, 'Eksik yönleri var, teknik mülakatta netleşsin.'],
  ]) await screeningFor(key, rating, comment);
  log('6 adayın ön görüşme formu dolduruldu (ücret ve İK yorumu yalnızca İK\'ya görünür)');

  for (const key of ['elif', 'selin', 'mert', 'deniz', 'zehra']) {
    const profile = profiles[key];
    const pdf = makePdf([
      `${profile.firstName} ${profile.lastName} - Ozgecmis`, `Pozisyon: ${profile.role}`, `Deneyim: ${profile.years} yil`, `Yetenekler: ${profile.tags.filter((t) => t !== 'demo').join(', ')}`,
      'Egitim: ODTU Bilgisayar Muhendisligi', 'Bu belge demo senaryosu icin uretilmistir.',
    ]);
    const form = new FormData();
    form.append('file', new Blob([pdf], { type: 'application/pdf' }), `${profile.firstName}_${profile.lastName}_CV.pdf`);
    await hr.upload(`/candidates/${candidates[key]}/documents`, form);
  }
  log('5 adaya CV (PDF) yüklendi; CV içeriği aramada bulunur');

  await hr.post(`/candidates/${candidates.zehra}/talent-pool`, { inTalentPool: true });
  log('Zehra Koç yetenek havuzuna alındı (pozisyona bağlı değil)');

  // ---- Kara liste ----
  console.log('Kara liste');
  const { id: blacklistedId } = await hrManager.post('/blacklist-entries', {
    candidateId: candidates.kemal, reasonId: blacklistReason.noShow, description: 'İki mülakata haber vermeden gelmedi (demo kaydı).', expiresAt: null,
  });
  state.blacklistEntries.push(blacklistedId);
  const { id: pendingBlacklistId } = await hr.post('/blacklist-entries', {
    candidateId: candidates.oguz, reasonId: blacklistReason.false, description: 'CV\'sindeki iş deneyimi doğrulanamadı (demo talebi).', expiresAt: null,
  });
  state.blacklistEntries.push(pendingBlacklistId);
  log('Kemal Demir kara listede (yürürlükte); Oğuz Tan için İK Yöneticisi onayı bekleyen talep var');
  save();

  // ---- Süreç hattı ----
  console.log('Süreç hattı');
  const startApplication = async (key) => {
    const { id } = await hr.post('/applications', { candidateId: candidates[key], positionId: mainPosition });
    state.applications.push(id);
    return id;
  };
  const applications = {};
  for (const key of ['elif', 'burak', 'selin', 'mert', 'deniz', 'ahmet', 'gizem', 'hakan']) applications[key] = await startApplication(key);
  save();

  const pipeline = await hr.get(`/positions/${mainPosition}/pipeline`);
  const stage = Object.fromEntries(pipeline.stages.map((item) => [item.name, item.id]));
  const moveTo = async (key, ...names) => {
    for (const name of names) await hr.post(`/applications/${applications[key]}/move`, { stageId: stage[name] });
  };

  const suggest = async (key, note) => {
    const { reviewIds } = await hr.post('/cv-reviews', { candidateId: candidates[key], applicationId: applications[key], reviewerUserIds: [lead.id], note });
    state.cvReviews.push(...reviewIds);
    return reviewIds[0];
  };
  const decide = (reviewId, decision, reason, rejectionReasonId) =>
    lead.post(`/cv-reviews/${reviewId}/decide`, { decision, reason, rejectionReasonId: rejectionReasonId ?? null });

  // Burak: CV Zeynep'in kararını bekliyor
  await moveTo('burak', 'İK ön görüşme', 'CV değerlendirme');
  await suggest('burak', 'Java ağırlıklı ama deneyimli; görüşünüzü rica ederim.');
  log('Burak Aydın: CV değerlendirme aşamasında, Zeynep Kaya\'nın kararını bekliyor');

  // Hakan: CV'si gecikmiş
  await moveTo('hakan', 'İK ön görüşme', 'CV değerlendirme');
  const overdueReview = await suggest('hakan', 'Bir önceki haftadan bekleyen öneri (gecikmiş senaryosu).');
  sql(`update cv_reviews set suggested_at = suggested_at - interval '8 days', due_at = now() - interval '2 days', escalate_at = now() - interval '1 day' where id = ${quote(overdueReview)};`);
  log('Hakan Özkan: CV değerlendirmesi gecikmiş (Geciken filtresi ve İK uyarısı için)');

  // Ahmet: CV olumsuz, süreç sonlanmış
  await moveTo('ahmet', 'İK ön görüşme', 'CV değerlendirme');
  const negativeReview = await suggest('ahmet', 'Deneyim seviyesi pozisyona uygun mu?');
  await decide(negativeReview, 'Negative', 'PHP ağırlıklı; .NET ve mimari deneyimi yok.', reject.technical);
  await hr.post(`/applications/${applications.ahmet}/close`, { status: 'Rejected', rejectionReasonId: reject.technical, note: 'CV olumsuz bulundu.' });
  log('Ahmet Şen: CV olumsuz, süreç "Olumsuz" olarak sonlandı');

  // Gizem: adaydan çekilme
  await moveTo('gizem', 'İK ön görüşme');
  await hr.post(`/applications/${applications.gizem}/close`, { status: 'Withdrawn', rejectionReasonId: reject.withdrew, note: 'Başka bir teklifi kabul etti.' });
  log('Gizem Polat: süreçten çekildi');

  // Selin: CV olumlu, teknik mülakat yarın
  await moveTo('selin', 'İK ön görüşme', 'CV değerlendirme');
  await decide(await suggest('selin', 'Referansla gelen güçlü bir aday.'), 'Positive', 'Dağıtık sistem deneyimi çok iyi; mülakata alalım.');
  await moveTo('selin', 'Teknik mülakat');
  const tomorrow = businessDay(1);
  const afterTomorrow = businessDay(2);
  const selinInterview = await hr.post('/interviews', {
    applicationId: applications.selin, typeId: type.technical, mode: 'Online', startsAt: at(tomorrow, 14), durationMinutes: 60,
    meetingUrl: 'https://teams.microsoft.com/l/meetup-join/demo-selin', note: 'Sistem tasarımı ve async konularına ağırlık verin.',
    interviewerUserIds: [lead.id, manager.id], confirmConflicts: true, notifyCandidate: true,
  });
  state.interviews.push(selinInterview.id);
  log('Selin Arslan: CV olumlu, Teknik mülakat yarın 14:00 (Zeynep Kaya + Ali Çelik)');
  save();

  // Mert ve Deniz: geçmiş teknik mülakat + puanlama
  const pastInterview = async (key, hour, interviewers, daysAgo) => {
    await moveTo(key, 'İK ön görüşme', 'CV değerlendirme');
    await decide(await suggest(key, 'Teknik mülakata alınabilir mi?'), 'Positive', 'CV uygun, mülakata alalım.');
    await moveTo(key, 'Teknik mülakat');
    const created = await hr.post('/interviews', {
      applicationId: applications[key], typeId: type.technical, mode: 'InPerson', startsAt: at(afterTomorrow, hour), durationMinutes: 60, roomId: roomA,
      note: 'Backend ağırlıklı teknik mülakat.', interviewerUserIds: interviewers.map((person) => person.id), confirmConflicts: true, notifyCandidate: false,
    });
    state.interviews.push(created.id);
    return { id: created.id, daysAgo };
  };
  const mertInterview = await pastInterview('mert', 10, [lead, manager], 6);
  const denizInterview = await pastInterview('deniz', 11, [lead, manager], 5);
  save();

  const fillScorecard = async (person, interviewId, scores, recommendation, note, submit) => {
    const { id } = await person.post(`/interviews/${interviewId}/scorecard`);
    const card = await person.get(`/scorecards/${id}`);
    await person.put(`/scorecards/${id}`, {
      items: card.items.map((item, index) => ({ id: item.id, score: scores[index] ?? null, note: scores[index] ? 'Demo notu.' : null })),
      generalNote: note, recommendation,
    });
    if (submit) await person.post(`/scorecards/${id}/submit`);
    return id;
  };

  // Mülakatlar "geçmişte" olsun: başlama saati geçmeden değerlendirme gönderilemez.
  const backdate = (interview) => sql(`update interviews set starts_at = starts_at - interval '${interview.daysAgo + 2} days', ends_at = ends_at - interval '${interview.daysAgo + 2} days' where id = ${quote(interview.id)};`);
  for (const interview of [mertInterview, denizInterview]) backdate(interview);

  // Deniz: iki değerlendirme de gönderildi, ortak karar verildi, final aşamasına geçti
  await fillScorecard(lead, denizInterview.id, [5, 4, 5, 4], 'StrongHire', 'Teknik olarak çok güçlü, tasarım yaklaşımı olgun.', true);
  await fillScorecard(manager, denizInterview.id, [4, 4, 5, 5], 'Hire', 'Liderlik potansiyeli yüksek.', true);
  await hr.post(`/interviews/${denizInterview.id}/outcome`, { outcome: 'Completed' });
  await hr.post(`/interviews/${denizInterview.id}/debrief`, { decision: 'Proceed', reason: 'İki mülakatçı da olumlu; final mülakata geçsin.' });
  await moveTo('deniz', 'Final mülakat');
  const finalInterview = await hr.post('/interviews', {
    applicationId: applications.deniz, typeId: type.manager, mode: 'InPerson', startsAt: at(businessDay(3), 15), durationMinutes: 45, roomId: roomA,
    note: 'Takım liderliği ve kültür uyumuna odaklanın.', interviewerUserIds: [manager.id], confirmConflicts: true, notifyCandidate: true,
  });
  state.interviews.push(finalInterview.id);
  log('Deniz Çelik: Teknik mülakat bitti, iki değerlendirme gönderildi, ortak karar "Geçsin"; Final mülakat planlandı');

  // Mert: Zeynep gönderdi, Ali'nin değerlendirmesi taslak (puanlanacak mülakatlar senaryosu)
  await fillScorecard(lead, mertInterview.id, [4, 3, 4, 3], 'Hire', 'Temeller sağlam; sistem tasarımında gelişmeli.', true);
  await fillScorecard(manager, mertInterview.id, [4, null, null, null], null, '', false);
  await hr.post(`/interviews/${mertInterview.id}/outcome`, { outcome: 'Completed' });
  log('Mert Yıldız: Teknik mülakat yapıldı; Zeynep gönderdi, Ali Çelik\'in değerlendirmesi taslak (Ali göndermeden aday sonraki aşamaya geçemez)');

  // Müsaitlik
  console.log('Müsaitlik');
  const addAvailability = async (person, day, fromHour, toHour) => {
    const { id } = await person.post('/me/availability', { startsAt: at(day, fromHour), endsAt: at(day, toHour) });
    state.availability.push(id);
  };
  await addAvailability(lead, tomorrow, 9, 12);
  await addAvailability(lead, tomorrow, 14, 17);
  await addAvailability(manager, tomorrow, 13, 17);
  await addAvailability(lead, afterTomorrow, 13, 18);
  await addAvailability(manager, afterTomorrow, 9, 12);
  log('Zeynep Kaya ve Ali Çelik için yarın ve ertesi iş günü müsaitlik aralıkları');

  // Kadro talebinden açılan pozisyon
  console.log('Kadro talebinden açılan pozisyon bekleniyor…');
  for (let attempt = 0; attempt < 24; attempt += 1) {
    const list = await hr.get('/positions?search=QA%20M%C3%BChendisi&pageSize=20');
    const found = list.items.find((item) => item.title === 'QA Mühendisi (Demo)');
    if (found) {
      state.positions.push(found.id);
      log('QA Mühendisi (Demo) pozisyonu otomatik açıldı');
      break;
    }
    await sleep(5000);
  }
} catch (error) {
  console.error(`\nHATA: ${error.message}`);
  console.error('Yarım kalan veriyi temizlemek için: node tools/demo-data/cleanup.mjs');
  process.exitCode = 1;
} finally {
  save();
}

if (!process.exitCode) {
  console.log('\nHazır. Test senaryolarını izleyebilirsiniz. Verileri silmek için: node tools/demo-data/cleanup.mjs');
}
