-- CR-03 / CR-11: "Yabancı diller" sözlüğünün varsayılan öğeleri.
-- Şema değişmez; yalnızca başlangıç verisidir. Üretimde uygulama açılışında seed çalışmadığı için
-- (Database:ApplyMigrationsOnStartup = false) elle çalıştırılır. Tekrar çalıştırılabilir (idempotent):
-- aynı ada sahip öğe zaten varsa eklenmez. Diğer diller yönetim panelinden eklenir.
-- gen_random_uuid() PostgreSQL 13+ ile hazır gelir.

INSERT INTO dictionary_items
    (id, type, name, sort_order, is_active, is_deleted, created_at, created_by)
SELECT gen_random_uuid(), 'Language', v.name, v.sort_order, TRUE, FALSE, NOW(), 'sql-script'
FROM (VALUES ('İngilizce', 1), ('Almanca', 2), ('Fransızca', 3)) AS v(name, sort_order)
WHERE NOT EXISTS (
    SELECT 1 FROM dictionary_items d
    WHERE d.type = 'Language' AND d.name = v.name AND d.is_deleted = FALSE
);
