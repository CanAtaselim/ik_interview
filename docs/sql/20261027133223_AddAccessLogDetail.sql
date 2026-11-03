START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261027133223_AddAccessLogDetail') THEN
    ALTER TABLE access_logs ADD detail character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261027133223_AddAccessLogDetail') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261027133223_AddAccessLogDetail', '10.0.12');
    END IF;
END $EF$;
COMMIT;

