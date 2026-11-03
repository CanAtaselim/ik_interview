START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103100136_AddScreeningDisability') THEN
    ALTER TABLE screenings ADD disability_description text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103100136_AddScreeningDisability') THEN
    ALTER TABLE screenings ADD has_disability boolean;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103100136_AddScreeningDisability') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261103100136_AddScreeningDisability', '10.0.12');
    END IF;
END $EF$;
COMMIT;

