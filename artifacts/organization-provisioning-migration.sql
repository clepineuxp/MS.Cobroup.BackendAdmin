START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023235308_AddOrganizationOwnershipAndUserContact') THEN
    ALTER TABLE users ADD "PhoneNumber" character varying(24);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023235308_AddOrganizationOwnershipAndUserContact') THEN
    ALTER TABLE users ADD "UpdatedAtUtc" timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023235308_AddOrganizationOwnershipAndUserContact') THEN
    ALTER TABLE organization_memberships ADD "IsOwner" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023235308_AddOrganizationOwnershipAndUserContact') THEN
    UPDATE users SET "UpdatedAtUtc" = "CreatedAtUtc";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023235308_AddOrganizationOwnershipAndUserContact') THEN
    CREATE UNIQUE INDEX "IX_organization_memberships_OrganizationId_IsOwner" ON organization_memberships ("OrganizationId", "IsOwner") WHERE "IsOwner" = TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023235308_AddOrganizationOwnershipAndUserContact') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261023235308_AddOrganizationOwnershipAndUserContact', '10.0.11');
    END IF;
END $EF$;
COMMIT;
