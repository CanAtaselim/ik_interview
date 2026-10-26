// seed.mjs'in ürettiği örnek verileri geri alır. Yalnızca .state.json'daki kimlikleri (ve onlara bağlı
// kayıtları) siler; sizin elle girdiğiniz verilere dokunmaz. Kullanım: node tools/demo-data/cleanup.mjs [--dry-run] [--clear-mailpit]
// --dry-run: her şeyi dener ama işlemi geri alır (hiçbir şey silinmez).
import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, rmSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { API_CONTAINER, quote, sql, uuidList } from './lib.mjs';

const STATE_FILE = join(dirname(fileURLToPath(import.meta.url)), '.state.json');
if (!existsSync(STATE_FILE)) {
  console.log('Silinecek örnek veri kaydı yok (.state.json bulunamadı).');
  process.exit(0);
}
const state = JSON.parse(readFileSync(STATE_FILE, 'utf8'));
const list = (key) => uuidList(state[key] ?? []);

const counts = () =>
  sql(`select 'aday '||(select count(*) from candidates)||', pozisyon '||(select count(*) from positions)||', talep '||(select count(*) from hiring_requests)||', soru '||(select count(*) from questions)||', şablon '||(select count(*) from interview_templates)||', sözlük '||(select count(*) from dictionary_items)||', bildirim '||(select count(*) from notifications)||', e-posta '||(select count(*) from outgoing_emails)||', dosya '||(select count(*) from candidate_documents)||', müsaitlik '||(select count(*) from interviewer_availabilities);`);

console.log('Önce :', counts());
if (process.argv.includes('--dry-run')) console.log('(Deneme modu: hiçbir şey silinmeyecek)');

// Yüklenen CV dosyalarının yolları (kayıtlar silinmeden önce okunur).
const storageKeys = sql(`select storage_key from candidate_documents where candidate_id in (${list('candidates')});`)
  .split('\n').filter(Boolean);

sql(`
begin;
create temp table d_c as select id from candidates where id in (${list('candidates')});
create temp table d_p as select id from positions where id in (${list('positions')}) or hiring_request_id in (${list('hiringRequests')});
create temp table d_r as select id from hiring_requests where id in (${list('hiringRequests')});
create temp table d_i as select id from interviews where id in (${list('interviews')}) or candidate_id in (select id from d_c) or position_id in (select id from d_p);
-- Demo olmayan bir mülakat veya değerlendirme kullanıyorsa şablon ve soru yerinde bırakılır.
create temp table d_t as select id from interview_templates t where id in (${list('templates')})
  and not exists (select 1 from interviews i where i.template_id = t.id and i.id not in (select id from d_i));
create temp table d_q as select id from questions q where id in (${list('questions')})
  and not exists (select 1 from interview_template_questions tq where tq.question_id = q.id and tq.interview_template_id not in (select id from d_t))
  and not exists (select 1 from scorecard_items si join scorecards sc on sc.id = si.scorecard_id where si.question_id = q.id and sc.interview_id not in (select id from d_i));
create temp table d_a as select id from applications where id in (${list('applications')}) or candidate_id in (select id from d_c) or position_id in (select id from d_p);
create temp table d_v as select id from cv_reviews where id in (${list('cvReviews')}) or candidate_id in (select id from d_c);
create temp table d_b as select id from blacklist_entries where id in (${list('blacklistEntries')}) or candidate_id in (select id from d_c);
create temp table d_s as select id from scorecards where interview_id in (select id from d_i);
create temp table d_ids as
  select id::text as id from d_c union select id::text from d_p union select id::text from d_r union select id::text from d_i
  union select id::text from d_a union select id::text from d_v union select id::text from d_b union select id::text from d_s;

delete from notifications n where exists (select 1 from d_ids x where n.link like '%' || x.id || '%' or n.dedupe_key like '%' || x.id || '%');
delete from outgoing_emails e where e.candidate_id in (select id from d_c) or exists (select 1 from d_ids x where e.dedupe_key like '%' || x.id || '%');
delete from outbox_messages o where exists (select 1 from d_ids x where o.payload::text like '%' || x.id || '%');
delete from scorecard_items where scorecard_id in (select id from d_s) or question_id in (select id from d_q);
delete from scorecards where id in (select id from d_s);
delete from interview_interviewers where interview_id in (select id from d_i);
delete from interviews where id in (select id from d_i);
delete from cv_reviews where id in (select id from d_v);
delete from application_stage_changes where application_id in (select id from d_a);
delete from applications where id in (select id from d_a);
delete from timeline_entries where candidate_id in (select id from d_c);
delete from access_logs where candidate_id in (select id from d_c);
delete from screenings where candidate_id in (select id from d_c);
delete from candidate_documents where candidate_id in (select id from d_c);
delete from blacklist_entries where id in (select id from d_b);
delete from candidates where id in (select id from d_c);
delete from interview_template_questions where interview_template_id in (select id from d_t) or question_id in (select id from d_q);
delete from interview_templates where id in (select id from d_t);
delete from question_versions where question_id in (select id from d_q);
delete from questions where id in (select id from d_q);
delete from positions where id in (select id from d_p);
delete from hiring_request_approval_steps where hiring_request_id in (select id from d_r);
delete from hiring_requests where id in (select id from d_r);
delete from interviewer_availabilities where id in (${list('availability')});
${process.argv.includes('--dry-run') ? 'rollback' : 'commit'};
`);

if (process.argv.includes('--dry-run')) {
  console.log('Deneme geçti: silme işlemi hatasız çalıştı ve geri alındı. Gerçek silme için --dry-run olmadan çalıştırın.');
  process.exit(0);
}

// Eklenen sözlük öğeleri: başka kayıt kullanıyorsa silinemez, o zaman yerinde bırakılır.
for (const id of state.dictionaryItems ?? []) {
  try {
    sql(`delete from dictionary_items where id = ${quote(id)};`);
  } catch {
    console.log(`Sözlük öğesi başka kayıtlarca kullanılıyor, silinmedi: ${id}`);
  }
}

for (const key of storageKeys) {
  if (/^[\w./-]+$/.test(key) && !key.includes('..')) {
    try {
      execFileSync('docker', ['exec', API_CONTAINER, 'rm', '-f', `/app/storage/${key}`]);
    } catch {
      console.log(`Dosya silinemedi: ${key}`);
    }
  }
}

if (process.argv.includes('--clear-mailpit')) {
  // Mailpit'teki TÜM iletileri siler (yalnızca isteğe bağlı bayrakla).
  const mailpit = process.env.MAILPIT_URL ?? 'http://localhost:8025';
  const result = await fetch(`${mailpit}/api/v1/messages`, { method: 'DELETE' }).catch(() => null);
  console.log(result?.ok ? 'Mailpit kutusu temizlendi.' : 'Mailpit temizlenemedi (çalışmıyor olabilir).');
}

rmSync(STATE_FILE);
console.log('Sonra:', counts());
console.log('Örnek veriler silindi.');
