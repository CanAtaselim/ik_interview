-- CR-16/17/18: "Olumsuz sonuç" ve "Süreç devam ediyor" aday e-posta şablonları.
-- Şema değişmez; yalnızca başlangıç verisidir. Üretimde uygulama açılışında seed çalışmadığı için
-- (Database:ApplyMigrationsOnStartup = false) yönetim ekranının şablon listesinde görünmeleri ve
-- düzenlenebilmeleri adına elle çalıştırılır. Tekrar çalıştırılabilir (idempotent): aynı kodlu
-- şablon varsa dokunulmaz, yönetim ekranında düzenlenmiş metin ezilmez.
-- Metinler DefaultEmailTemplates.cs ile aynıdır.

INSERT INTO email_templates (id, code, name, subject, body, variables, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), v.code, v.name, v.subject, v.body, v.variables, NOW(), 'sql-script', FALSE
FROM (VALUES
    (
        'RejectionCandidate',
        'Olumsuz sonuç (adaya)',
        'Başvurunuz hakkında: {Pozisyon}',
        $body$<p>Merhaba {AdayAdi},</p>
<p><strong>{Pozisyon}</strong> pozisyonuna gösterdiğiniz ilgi ve süreç boyunca ayırdığınız zaman için teşekkür ederiz.</p>
<p>Yaptığımız değerlendirmeler sonucunda bu pozisyon için sizinle devam etmemeye karar verdik. Bu karar, ileride açılacak diğer pozisyonlar için başvurunuzu değerlendirmemize engel değildir.</p>
<p>Kariyerinizde başarılar dileriz.</p>$body$,
        '{AdayAdi}, {Pozisyon}'
    ),
    (
        'OnHoldCandidate',
        'Süreç devam ediyor (adaya)',
        'Başvuru süreciniz devam ediyor: {Pozisyon}',
        $body$<p>Merhaba {AdayAdi},</p>
<p><strong>{Pozisyon}</strong> pozisyonu için başvuru süreciniz devam etmektedir.</p>
<p>Değerlendirmelerimiz tamamlandığında sizinle yeniden iletişime geçeceğiz. Gösterdiğiniz sabır için teşekkür ederiz.</p>$body$,
        '{AdayAdi}, {Pozisyon}'
    )
) AS v(code, name, subject, body, variables)
WHERE NOT EXISTS (
    SELECT 1 FROM email_templates t WHERE t.code = v.code AND t.is_deleted = FALSE
);
