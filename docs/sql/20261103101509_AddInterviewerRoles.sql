START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103101509_AddInterviewerRoles') THEN
    ALTER TABLE interview_templates ADD target_role character varying(20) NOT NULL DEFAULT 'Technical';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103101509_AddInterviewerRoles') THEN
    ALTER TABLE interview_interviewers ADD role character varying(20) NOT NULL DEFAULT 'Technical';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261103101509_AddInterviewerRoles') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261103101509_AddInterviewerRoles', '10.0.12');
    END IF;
END $EF$;
COMMIT;

