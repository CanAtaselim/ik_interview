-- GEÇİCİ (test dönemi): test hesaplarının rollerini eşler.
-- Ldap:EnableTestAccounts=true iken test hesapları (admin, ikyonetici, ikuzman, mudur, takimlideri, yigit,
-- yonetim, calisan; şifre Test123!) "Recruitment-<Rol>" gruplarının üyesi sayılır. Üretimde başlangıç verisi
-- yazılmadığı için bu eşlemelerin elle eklenmesi gerekir; yoksa test hesabı girişte rolsüz kalır.
-- Tekrar çalıştırılabilir (idempotent).
--
-- Gerçek kullanıma geçerken: Ldap:EnableTestAccounts'ı kapatın ve en alttaki "KALDIR" bölümünü çalıştırın.

INSERT INTO role_group_mappings (id, group_name, role, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), v.group_name, v.role, NOW(), 'sql-script', FALSE
FROM (VALUES
    ('Recruitment-SystemAdmin',       'SystemAdmin'),
    ('Recruitment-HrManager',         'HrManager'),
    ('Recruitment-HrSpecialist',      'HrSpecialist'),
    ('Recruitment-TeamLead',          'TeamLead'),
    ('Recruitment-DepartmentManager', 'DepartmentManager'),
    ('Recruitment-Executive',         'Executive')
) AS v(group_name, role)
WHERE NOT EXISTS (
    SELECT 1 FROM role_group_mappings m
    WHERE lower(m.group_name) = lower(v.group_name) AND m.role = v.role AND m.is_deleted = FALSE
);

-- KALDIR (test bitince çalıştırın):
-- UPDATE role_group_mappings SET is_deleted = TRUE, deleted_at = NOW(), deleted_by = 'sql-script'
-- WHERE group_name LIKE 'Recruitment-%' AND is_deleted = FALSE;
-- UPDATE users SET is_active = FALSE
-- WHERE username IN ('admin','ikyonetici','ikuzman','mudur','takimlideri','yigit','yonetim','calisan');
