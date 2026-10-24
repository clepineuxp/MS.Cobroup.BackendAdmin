START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024011055_AddLoanTemplateRangesAndRoutes') THEN
    ALTER TABLE loan_templates ADD "MinPrincipalAmount" numeric(18,4) NOT NULL DEFAULT 0.0001;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024011055_AddLoanTemplateRangesAndRoutes') THEN
    ALTER TABLE loan_templates ADD "RouteIdsJson" jsonb NOT NULL DEFAULT '[]';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024011055_AddLoanTemplateRangesAndRoutes') THEN
    ALTER TABLE loan_template_versions ADD "MinPrincipalAmount" numeric(18,4) NOT NULL DEFAULT 0.0001;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024011055_AddLoanTemplateRangesAndRoutes') THEN
    ALTER TABLE loan_template_versions ADD "RouteIdsJson" jsonb NOT NULL DEFAULT '[]';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024011055_AddLoanTemplateRangesAndRoutes') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261024011055_AddLoanTemplateRangesAndRoutes', '10.0.11');
    END IF;
END $EF$;
COMMIT;
