-- AD grubu → uygulama rolü eşlemesi (role_group_mappings).
-- Şema değişmez; yalnızca başlangıç verisidir. Üretimde seed çalışmadığı için elle çalıştırılır.
-- Tekrar çalıştırılabilir (idempotent): aynı grup + rol çifti zaten varsa dokunulmaz.
--
-- ÖNEMLİ: Aşağıdaki liste, şirketin AD grup adlarına göre ÖNERİDİR. Çalıştırmadan önce grup adlarını
-- kontrol edin; yanlış eşleme yanlış kişiye yetki verir. Eşleme sonradan Yönetim → Rol eşlemeleri
-- ekranından da eklenip silinebilir. Bir kullanıcı birden çok gruptan birden çok rol alabilir.
-- Grup adı büyük/küçük harf duyarsız karşılaştırılır; CN ya da tam DN yazılabilir.
-- Eşlemesi olmayan AD kullanıcısı uygulamaya girer ama rolü olmadığı için erişemez (İK rol verir).

INSERT INTO role_group_mappings (id, group_name, role, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), v.group_name, v.role, NOW(), 'sql-script', FALSE
FROM (VALUES
    -- İK
    ('G_HR',              'HrSpecialist'),
    ('G_HR_LEADER',       'HrManager'),
    ('G_HR_LEADER',       'SystemAdmin'),   -- İK Yöneticisi aynı zamanda sistem yöneticisi
    -- Takım liderleri
    ('G_DEV_LEADER',      'TeamLead'),
    ('G_IT_LEADER',       'TeamLead'),
    ('G_DATA_TEAMLEADER', 'TeamLead'),
    ('G_DATA_LEADERS',    'TeamLead'),
    -- Birim yöneticileri
    ('G_MANAGERS',        'DepartmentManager')
    -- Üst yönetim (Executive) için grup adı verilirse aynı biçimde eklenir.
) AS v(group_name, role)
WHERE NOT EXISTS (
    SELECT 1 FROM role_group_mappings m
    WHERE lower(m.group_name) = lower(v.group_name) AND m.role = v.role AND m.is_deleted = FALSE
);
