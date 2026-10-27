START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261027104705_AddUserPreferences') THEN
    CREATE TABLE user_preferences (
        user_id uuid NOT NULL,
        key character varying(100) NOT NULL,
        value jsonb NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT pk_user_preferences PRIMARY KEY (user_id, key),
        CONSTRAINT fk_user_preferences_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261027104705_AddUserPreferences') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261027104705_AddUserPreferences', '10.0.12');
    END IF;
END $EF$;
COMMIT;

