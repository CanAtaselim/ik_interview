START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103071859_MakeLevelOptional') THEN
    ALTER TABLE positions ALTER COLUMN level_id DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103071859_MakeLevelOptional') THEN
    ALTER TABLE hiring_requests ALTER COLUMN level_id DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103071859_MakeLevelOptional') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261103071859_MakeLevelOptional', '10.0.12');
    END IF;
END $EF$;
COMMIT;

