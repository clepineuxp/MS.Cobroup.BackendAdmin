CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021071052_InitialIdentityAuthority') THEN
    CREATE TABLE bootstrap_markers (
        "Id" character varying(32) NOT NULL,
        "CompletedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_bootstrap_markers" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021071052_InitialIdentityAuthority') THEN
    CREATE TABLE "OpenIddictApplications" (
        "Id" uuid NOT NULL,
        "ApplicationType" character varying(50),
        "ClientId" character varying(100),
        "ClientSecret" text,
        "ClientType" character varying(50),
        "ConcurrencyToken" character varying(50),
        "ConsentType" character varying(50),
        "DisplayName" text,
        "DisplayNames" text,
        "JsonWebKeySet" text,
        "Permissions" text,
        "PostLogoutRedirectUris" text,
        "Properties" text,
        "RedirectUris" text,
        "Requirements" text,
        "Settings" text,
        CONSTRAINT "PK_OpenIddictApplications" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021071052_InitialIdentityAuthority') THEN
    CREATE TABLE "OpenIddictScopes" (
        "Id" uuid NOT NULL,
        "ConcurrencyToken" character varying(50),
        "Description" text,
        "Descriptions" text,
        "DisplayName" text,
        "DisplayNames" text,
        "Name" character varying(200),
        "Properties" text,
        "Resources" text,
        CONSTRAINT "PK_OpenIddictScopes" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021071052_InitialIdentityAuthority') THEN
    CREATE TABLE organizations (
        "Id" uuid NOT NULL,
        "Code" character varying(48) NOT NULL,
        "Name" character varying(160) NOT NULL,
        "Status" character varying(24) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_organizations" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021071052_InitialIdentityAuthority') THEN
    CREATE TABLE security_audit (
        "Id" uuid NOT NULL,
        "EventType" character varying(96) NOT NULL,
        "Actor" character varying(160) NOT NULL,
        "SourceIp" character varying(64) NOT NULL,
        "OccurredAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_security_audit" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021071052_InitialIdentityAuthority') THEN
    CREATE TABLE "OpenIddictAuthorizations" (
        "Id" uuid NOT NULL,
        "ApplicationId" uuid,
        "ConcurrencyToken" character varying(50),
        "CreationDate" timestamp with time zone,
        "Properties" text,
        "Scopes" text,
        "Status" character varying(50),
        "Subject" character varying(400),
        "Type" character varying(50),
        CONSTRAINT "PK_OpenIddictAuthorizations" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_OpenIddictAuthorizations_OpenIddictApplications_Application~" FOREIGN KEY ("ApplicationId") REFERENCES "OpenIddictApplications" ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021071052_InitialIdentityAuthority') THEN
    CREATE TABLE users (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Username" character varying(80) NOT NULL,
        "NormalizedUsername" character varying(80) NOT NULL,
        "DisplayName" character varying(160) NOT NULL,
        "PasswordHash" character varying(512) NOT NULL,
        "Role" character varying(32) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_users" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_users_organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES organizations ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021071052_InitialIdentityAuthority') THEN
    CREATE TABLE "OpenIddictTokens" (
        "Id" uuid NOT NULL,
        "ApplicationId" uuid,
        "AuthorizationId" uuid,
        "ConcurrencyToken" character varying(50),
        "CreationDate" timestamp with time zone,
        "ExpirationDate" timestamp with time zone,
        "Payload" text,
        "Properties" text,
        "RedemptionDate" timestamp with time zone,
        "ReferenceId" character varying(100),
        "Status" character varying(50),
        "Subject" character varying(400),
        "Type" character varying(150),
        CONSTRAINT "PK_OpenIddictTokens" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_OpenIddictTokens_OpenIddictApplications_ApplicationId" FOREIGN KEY ("ApplicationId") REFERENCES "OpenIddictApplications" ("Id"),
        CONSTRAINT "FK_OpenIddictTokens_OpenIddictAuthorizations_AuthorizationId" FOREIGN KEY ("AuthorizationId") REFERENCES "OpenIddictAuthorizations" ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021071052_InitialIdentityAuthority') THEN
    CREATE UNIQUE INDEX "IX_OpenIddictApplications_ClientId" ON "OpenIddictApplications" ("ClientId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021071052_InitialIdentityAuthority') THEN
    CREATE INDEX "IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type" ON "OpenIddictAuthorizations" ("ApplicationId", "Status", "Subject", "Type");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021071052_InitialIdentityAuthority') THEN
    CREATE UNIQUE INDEX "IX_OpenIddictScopes_Name" ON "OpenIddictScopes" ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021071052_InitialIdentityAuthority') THEN
    CREATE INDEX "IX_OpenIddictTokens_ApplicationId_Status_Subject_Type" ON "OpenIddictTokens" ("ApplicationId", "Status", "Subject", "Type");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021071052_InitialIdentityAuthority') THEN
    CREATE INDEX "IX_OpenIddictTokens_AuthorizationId" ON "OpenIddictTokens" ("AuthorizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021071052_InitialIdentityAuthority') THEN
    CREATE UNIQUE INDEX "IX_OpenIddictTokens_ReferenceId" ON "OpenIddictTokens" ("ReferenceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021071052_InitialIdentityAuthority') THEN
    CREATE UNIQUE INDEX "IX_organizations_Code" ON organizations ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021071052_InitialIdentityAuthority') THEN
    CREATE INDEX "IX_security_audit_OccurredAtUtc" ON security_audit ("OccurredAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021071052_InitialIdentityAuthority') THEN
    CREATE UNIQUE INDEX "IX_users_OrganizationId_NormalizedUsername" ON users ("OrganizationId", "NormalizedUsername");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021071052_InitialIdentityAuthority') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261021071052_InitialIdentityAuthority', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021123623_AddRouteManagement') THEN
    CREATE TABLE administrative_audit (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "ActorUserId" uuid NOT NULL,
        "EventType" character varying(96) NOT NULL,
        "EntityType" character varying(64) NOT NULL,
        "EntityId" uuid NOT NULL,
        "OccurredAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_administrative_audit" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_administrative_audit_organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES organizations ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_administrative_audit_users_ActorUserId" FOREIGN KEY ("ActorUserId") REFERENCES users ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021123623_AddRouteManagement') THEN
    CREATE TABLE routes (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Name" character varying(120) NOT NULL,
        "NormalizedName" character varying(120) NOT NULL,
        "Description" character varying(500),
        "Status" character varying(24) NOT NULL,
        "SupervisorUserId" uuid NOT NULL,
        "CreatedByUserId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_routes" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_routes_organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES organizations ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_routes_users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES users ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_routes_users_SupervisorUserId" FOREIGN KEY ("SupervisorUserId") REFERENCES users ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021123623_AddRouteManagement') THEN
    CREATE TABLE route_collector_assignments (
        "RouteId" uuid NOT NULL,
        "CollectorUserId" uuid NOT NULL,
        "AssignedByUserId" uuid NOT NULL,
        "AssignedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_route_collector_assignments" PRIMARY KEY ("RouteId", "CollectorUserId"),
        CONSTRAINT "FK_route_collector_assignments_routes_RouteId" FOREIGN KEY ("RouteId") REFERENCES routes ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_route_collector_assignments_users_AssignedByUserId" FOREIGN KEY ("AssignedByUserId") REFERENCES users ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_route_collector_assignments_users_CollectorUserId" FOREIGN KEY ("CollectorUserId") REFERENCES users ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021123623_AddRouteManagement') THEN
    CREATE INDEX "IX_administrative_audit_ActorUserId" ON administrative_audit ("ActorUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021123623_AddRouteManagement') THEN
    CREATE INDEX "IX_administrative_audit_OrganizationId_OccurredAtUtc" ON administrative_audit ("OrganizationId", "OccurredAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021123623_AddRouteManagement') THEN
    CREATE INDEX "IX_route_collector_assignments_AssignedByUserId" ON route_collector_assignments ("AssignedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021123623_AddRouteManagement') THEN
    CREATE INDEX "IX_route_collector_assignments_CollectorUserId" ON route_collector_assignments ("CollectorUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021123623_AddRouteManagement') THEN
    CREATE INDEX "IX_routes_CreatedByUserId" ON routes ("CreatedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021123623_AddRouteManagement') THEN
    CREATE UNIQUE INDEX "IX_routes_OrganizationId_NormalizedName" ON routes ("OrganizationId", "NormalizedName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021123623_AddRouteManagement') THEN
    CREATE INDEX "IX_routes_SupervisorUserId" ON routes ("SupervisorUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021123623_AddRouteManagement') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261021123623_AddRouteManagement', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021140747_GlobalUsersAndMemberships') THEN
    ALTER TABLE users DROP CONSTRAINT "FK_users_organizations_OrganizationId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021140747_GlobalUsersAndMemberships') THEN
    DROP INDEX "IX_users_OrganizationId_NormalizedUsername";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021140747_GlobalUsersAndMemberships') THEN
    CREATE TABLE organization_memberships (
        "UserId" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Role" character varying(32) NOT NULL,
        "IsActive" boolean NOT NULL,
        "JoinedAtUtc" timestamp with time zone NOT NULL,
        "LastAccessedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_organization_memberships" PRIMARY KEY ("UserId", "OrganizationId"),
        CONSTRAINT "FK_organization_memberships_organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES organizations ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_organization_memberships_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021140747_GlobalUsersAndMemberships') THEN
    INSERT INTO organization_memberships
        ("UserId", "OrganizationId", "Role", "IsActive", "JoinedAtUtc", "LastAccessedAtUtc")
    SELECT "Id", "OrganizationId", "Role", TRUE, "CreatedAtUtc", "CreatedAtUtc"
    FROM users;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021140747_GlobalUsersAndMemberships') THEN
    ALTER TABLE users DROP COLUMN "OrganizationId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021140747_GlobalUsersAndMemberships') THEN
    ALTER TABLE users DROP COLUMN "Role";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021140747_GlobalUsersAndMemberships') THEN
    CREATE UNIQUE INDEX "IX_users_NormalizedUsername" ON users ("NormalizedUsername");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021140747_GlobalUsersAndMemberships') THEN
    CREATE INDEX "IX_organization_memberships_OrganizationId" ON organization_memberships ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021140747_GlobalUsersAndMemberships') THEN
    CREATE INDEX "IX_organization_memberships_UserId_LastAccessedAtUtc" ON organization_memberships ("UserId", "LastAccessedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021140747_GlobalUsersAndMemberships') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261021140747_GlobalUsersAndMemberships', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021171353_CompleteRouteConfiguration') THEN
    ALTER TABLE routes ADD "MaxActiveCustomersMode" character varying(16) NOT NULL DEFAULT 'Inherited';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021171353_CompleteRouteConfiguration') THEN
    ALTER TABLE routes ADD "MaxActiveCustomersOverride" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021171353_CompleteRouteConfiguration') THEN
    ALTER TABLE routes ADD "MaxActiveLoansMode" character varying(16) NOT NULL DEFAULT 'Inherited';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021171353_CompleteRouteConfiguration') THEN
    ALTER TABLE routes ADD "MaxActiveLoansOverride" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021171353_CompleteRouteConfiguration') THEN
    CREATE TABLE organization_settings (
        "OrganizationId" uuid NOT NULL,
        "TimeZoneId" character varying(100) NOT NULL,
        "CurrencyCode" character varying(3) NOT NULL,
        "MaxActiveCustomersPerRoute" integer,
        "MaxActiveLoansPerRoute" integer,
        "UpdatedByUserId" uuid NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_organization_settings" PRIMARY KEY ("OrganizationId"),
        CONSTRAINT "FK_organization_settings_organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES organizations ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_organization_settings_users_UpdatedByUserId" FOREIGN KEY ("UpdatedByUserId") REFERENCES users ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021171353_CompleteRouteConfiguration') THEN
    CREATE INDEX "IX_organization_settings_UpdatedByUserId" ON organization_settings ("UpdatedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021171353_CompleteRouteConfiguration') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261021171353_CompleteRouteConfiguration', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021172327_AddAdministrativeAuditDetails') THEN
    ALTER TABLE administrative_audit ADD "DetailsJson" character varying(4000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021172327_AddAdministrativeAuditDetails') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261021172327_AddAdministrativeAuditDetails', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021192550_AddLoanTemplateCatalog') THEN
    CREATE TABLE loan_templates (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Name" character varying(120) NOT NULL,
        "NormalizedName" character varying(120) NOT NULL,
        "Description" character varying(500),
        "Classification" character varying(24) NOT NULL,
        "Status" character varying(16) NOT NULL,
        "Version" integer NOT NULL,
        "MaxLoanDays" integer NOT NULL,
        "MaxPrincipalAmount" numeric(18,4) NOT NULL,
        "AllowedPercentagesJson" jsonb NOT NULL,
        "CreatedByUserId" uuid NOT NULL,
        "UpdatedByUserId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_loan_templates" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_loan_templates_organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES organizations ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_loan_templates_users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES users ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_loan_templates_users_UpdatedByUserId" FOREIGN KEY ("UpdatedByUserId") REFERENCES users ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021192550_AddLoanTemplateCatalog') THEN
    CREATE TABLE loan_template_versions (
        "Id" uuid NOT NULL,
        "LoanTemplateId" uuid NOT NULL,
        "Version" integer NOT NULL,
        "Name" character varying(120) NOT NULL,
        "Description" character varying(500),
        "Classification" character varying(24) NOT NULL,
        "Status" character varying(16) NOT NULL,
        "MaxLoanDays" integer NOT NULL,
        "MaxPrincipalAmount" numeric(18,4) NOT NULL,
        "AllowedPercentagesJson" jsonb NOT NULL,
        "RecordedByUserId" uuid NOT NULL,
        "RecordedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_loan_template_versions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_loan_template_versions_loan_templates_LoanTemplateId" FOREIGN KEY ("LoanTemplateId") REFERENCES loan_templates ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_loan_template_versions_users_RecordedByUserId" FOREIGN KEY ("RecordedByUserId") REFERENCES users ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021192550_AddLoanTemplateCatalog') THEN
    CREATE UNIQUE INDEX "IX_loan_template_versions_LoanTemplateId_Version" ON loan_template_versions ("LoanTemplateId", "Version");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021192550_AddLoanTemplateCatalog') THEN
    CREATE INDEX "IX_loan_template_versions_RecordedByUserId" ON loan_template_versions ("RecordedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021192550_AddLoanTemplateCatalog') THEN
    CREATE INDEX "IX_loan_templates_CreatedByUserId" ON loan_templates ("CreatedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021192550_AddLoanTemplateCatalog') THEN
    CREATE UNIQUE INDEX "IX_loan_templates_OrganizationId_NormalizedName" ON loan_templates ("OrganizationId", "NormalizedName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021192550_AddLoanTemplateCatalog') THEN
    CREATE INDEX "IX_loan_templates_OrganizationId_Status_Classification" ON loan_templates ("OrganizationId", "Status", "Classification");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021192550_AddLoanTemplateCatalog') THEN
    CREATE INDEX "IX_loan_templates_UpdatedByUserId" ON loan_templates ("UpdatedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021192550_AddLoanTemplateCatalog') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261021192550_AddLoanTemplateCatalog', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021215708_AddPaymentMethods') THEN
    CREATE TABLE payment_methods (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Name" character varying(50) NOT NULL,
        "NormalizedName" character varying(50) NOT NULL,
        "DisplayOrder" integer NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_payment_methods" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_payment_methods_organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES organizations ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021215708_AddPaymentMethods') THEN
    CREATE INDEX "IX_payment_methods_OrganizationId_DisplayOrder" ON payment_methods ("OrganizationId", "DisplayOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021215708_AddPaymentMethods') THEN
    CREATE UNIQUE INDEX "IX_payment_methods_OrganizationId_NormalizedName" ON payment_methods ("OrganizationId", "NormalizedName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021215708_AddPaymentMethods') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261021215708_AddPaymentMethods', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022135103_AddRefreshTokensTable') THEN
    CREATE TABLE refresh_tokens (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Token" character varying(256) NOT NULL,
        "ExpiresAtUtc" timestamp with time zone NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "RevokedAtUtc" timestamp with time zone,
        "ReplacedByToken" text,
        CONSTRAINT "PK_refresh_tokens" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_refresh_tokens_organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES organizations ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_refresh_tokens_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022135103_AddRefreshTokensTable') THEN
    CREATE INDEX "IX_refresh_tokens_OrganizationId" ON refresh_tokens ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022135103_AddRefreshTokensTable') THEN
    CREATE UNIQUE INDEX "IX_refresh_tokens_Token" ON refresh_tokens ("Token");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022135103_AddRefreshTokensTable') THEN
    CREATE INDEX "IX_refresh_tokens_UserId_ExpiresAtUtc" ON refresh_tokens ("UserId", "ExpiresAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022135103_AddRefreshTokensTable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261022135103_AddRefreshTokensTable', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022152043_RemoveLegacyOidcAndUpgradeRefreshTokens') THEN
    DROP TABLE "OpenIddictScopes";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022152043_RemoveLegacyOidcAndUpgradeRefreshTokens') THEN
    DROP TABLE "OpenIddictTokens";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022152043_RemoveLegacyOidcAndUpgradeRefreshTokens') THEN
    DROP TABLE "OpenIddictAuthorizations";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022152043_RemoveLegacyOidcAndUpgradeRefreshTokens') THEN
    DROP TABLE "OpenIddictApplications";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022152043_RemoveLegacyOidcAndUpgradeRefreshTokens') THEN
    DROP INDEX "IX_refresh_tokens_Token";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022152043_RemoveLegacyOidcAndUpgradeRefreshTokens') THEN
    DELETE FROM refresh_tokens;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022152043_RemoveLegacyOidcAndUpgradeRefreshTokens') THEN
    ALTER TABLE refresh_tokens DROP COLUMN "ReplacedByToken";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022152043_RemoveLegacyOidcAndUpgradeRefreshTokens') THEN
    ALTER TABLE refresh_tokens DROP COLUMN "Token";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022152043_RemoveLegacyOidcAndUpgradeRefreshTokens') THEN
    ALTER TABLE refresh_tokens ADD "FamilyId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022152043_RemoveLegacyOidcAndUpgradeRefreshTokens') THEN
    ALTER TABLE refresh_tokens ADD "ReplacedByTokenHash" character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022152043_RemoveLegacyOidcAndUpgradeRefreshTokens') THEN
    ALTER TABLE refresh_tokens ADD "RevocationReason" character varying(256);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022152043_RemoveLegacyOidcAndUpgradeRefreshTokens') THEN
    ALTER TABLE refresh_tokens ADD "TokenHash" character varying(64) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022152043_RemoveLegacyOidcAndUpgradeRefreshTokens') THEN
    CREATE INDEX "IX_refresh_tokens_FamilyId" ON refresh_tokens ("FamilyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022152043_RemoveLegacyOidcAndUpgradeRefreshTokens') THEN
    CREATE UNIQUE INDEX "IX_refresh_tokens_TokenHash" ON refresh_tokens ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022152043_RemoveLegacyOidcAndUpgradeRefreshTokens') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261022152043_RemoveLegacyOidcAndUpgradeRefreshTokens', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023151001_AddSigningKeyIdToRefreshTokens') THEN
    DELETE FROM refresh_tokens;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023151001_AddSigningKeyIdToRefreshTokens') THEN
    ALTER TABLE refresh_tokens ADD "SigningKeyId" character varying(64) NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023151001_AddSigningKeyIdToRefreshTokens') THEN
    CREATE INDEX "IX_refresh_tokens_SigningKeyId" ON refresh_tokens ("SigningKeyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023151001_AddSigningKeyIdToRefreshTokens') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261023151001_AddSigningKeyIdToRefreshTokens', '10.0.11');
    END IF;
END $EF$;
COMMIT;

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

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024064552_AddRouteInitialBalance') THEN
    ALTER TABLE routes ADD "InitialBalance" numeric(18,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024064552_AddRouteInitialBalance') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261024064552_AddRouteInitialBalance', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261026200735_AddRouteTimeZoneAndDayReopeningPermission') THEN
    ALTER TABLE routes ADD "TimeZoneId" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261026200735_AddRouteTimeZoneAndDayReopeningPermission') THEN
    ALTER TABLE organization_memberships ADD "CanReopenDay" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261026200735_AddRouteTimeZoneAndDayReopeningPermission') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261026200735_AddRouteTimeZoneAndDayReopeningPermission', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027225241_AddMachineAuthorization') THEN
    CREATE TABLE machine_authorization_requests (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "RouteId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "MachineId" character varying(128) NOT NULL,
        "OtpHash" character varying(64) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "ExpiresAtUtc" timestamp with time zone NOT NULL,
        "ApprovedAtUtc" timestamp with time zone,
        "ApprovedByUserId" uuid,
        CONSTRAINT "PK_machine_authorization_requests" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027225241_AddMachineAuthorization') THEN
    CREATE TABLE machine_authorizations (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "RouteId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "MachineId" character varying(128) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "AuthorizedUntilUtc" timestamp with time zone,
        "IsIndefinite" boolean NOT NULL,
        "Status" character varying(16) NOT NULL,
        "RevokedAtUtc" timestamp with time zone,
        "RevokedByUserId" uuid,
        CONSTRAINT "PK_machine_authorizations" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027225241_AddMachineAuthorization') THEN
    CREATE INDEX "IX_machine_authorization_requests_OrganizationId_ExpiresAtUtc" ON machine_authorization_requests ("OrganizationId", "ExpiresAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027225241_AddMachineAuthorization') THEN
    CREATE INDEX "IX_machine_authorizations_OrganizationId_RouteId_UserId_Machin~" ON machine_authorizations ("OrganizationId", "RouteId", "UserId", "MachineId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027225241_AddMachineAuthorization') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261027225241_AddMachineAuthorization', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261028160135_CancelMachineAuthorizationRequest') THEN
    ALTER TABLE machine_authorization_requests ADD "CancelledAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261028160135_CancelMachineAuthorizationRequest') THEN
    ALTER TABLE machine_authorization_requests ADD "CancelledByUserId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261028160135_CancelMachineAuthorizationRequest') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261028160135_CancelMachineAuthorizationRequest', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261028162109_MachineAuthorizationOtpSecret') THEN
    ALTER TABLE machine_authorization_requests ADD "ProtectedOtp" character varying(512);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261028162109_MachineAuthorizationOtpSecret') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261028162109_MachineAuthorizationOtpSecret', '10.0.11');
    END IF;
END $EF$;
COMMIT;

