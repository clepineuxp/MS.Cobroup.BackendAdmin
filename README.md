# MS.Cobroup.BackendAdmin

Alta privada de organizaciones y administración del equipo: [contrato y procedimiento](docs/contracts/organization-provisioning-users-v1.md). El script `scripts/provision-organization.ps1` crea una organización con su administrador dueño; `GET|POST /api/v1/users` permite a administradores activos listar y crear supervisores y colaboradores en su tenant.

Microservicio administrativo y autoridad JWT única de COBROUP. Los usuarios son identidades globales y pueden tener membresías con rol en varias organizaciones. El login usa solamente usuario y contraseña, selecciona la última organización accedida y permite cambiarla emitiendo un nuevo par de tokens y revocando la familia anterior. La fase 3 completa la configuración tenant-safe de organización, rutas, cupos y asignaciones.

En fase 4 expone `GET|POST /api/v1/customers` y `GET|PUT /api/v1/customers/{id}` como fachada exclusiva para `Admin`; reenvía el token al backend operativo y no escribe sus tablas.

## Requisitos y ejecución

```powershell
dotnet restore MS.Cobroup.BackendAdmin.slnx
dotnet build MS.Cobroup.BackendAdmin.slnx --no-restore
dotnet test MS.Cobroup.BackendAdmin.slnx --no-build
dotnet tool restore
dotnet dotnet-ef database update --project MS.Cobroup.BackendAdmin.Infrastructure --startup-project MS.Cobroup.BackendAdmin.Api
dotnet run --project MS.Cobroup.BackendAdmin.Api --launch-profile https
```

La conexión se sobrescribe mediante User Secrets con la clave `ConnectionStrings:IdentityDatabase`; la contraseña no está versionada. La decisión vigente exige firma JWT exclusiva `HS256` con `Jwt:SigningKeyBase64`: secreto CSPRNG Base64 de 32 bytes o más, diferente en Dev/QA/Prod y administrado desde `cobroup.environments`. El mismo valor del ambiente se entrega a BackendAdmin y Backend; falta, Base64 inválido o longitud insuficiente deben producir *fail-fast*. No existen RSA, PEM/PFX, JWKS, acknowledge ni valores de respaldo. La implementación actual debe adecuarse y reauditarse. El bootstrap requiere `Bootstrap__Key` y el encabezado `X-Bootstrap-Key`; la marca persistida impide una segunda ejecución.

El entorno local ya contiene una organización `COBROUP` y un usuario para cada rol: `admin`, `supervisor` y `collector`. Sus contraseñas aleatorias solo existen en User Secrets bajo `LocalBootstrap:AdminPassword`, `LocalBootstrap:SupervisorPassword` y `LocalBootstrap:CollectorPassword`. Pueden consultarse localmente con `dotnet user-secrets list --project MS.Cobroup.BackendAdmin.Api` sin copiarlas a archivos versionados.

Contrato de identidad: `POST /api/v1/auth/login`, `POST /api/v1/auth/refresh`, `POST /api/v1/auth/logout`, `GET /api/v1/auth/me`, `GET /api/v1/auth/organizations` y `PUT /api/v1/auth/organizations/current`. No existe endpoint JWKS. El access token dura entre 5 y 15 minutos y solo vive en memoria del cliente. El refresh token, con máximo de 30 días, rota atómicamente en cada uso, se entrega exclusivamente en cookie `HttpOnly`, `Secure` y `SameSite`, y solo se guarda hasheado en servidor; la reutilización revoca toda su familia. Refresh, logout y cambio de organización deben tener protección CSRF, rate limiting y auditoría sin tokens ni credenciales.

También se exponen `GET /health`, `/api/v1/bootstrap`, `GET|PUT /api/v1/organization-settings`, `GET|POST /api/v1/routes`, `PUT /api/v1/routes/{id}`, `GET /api/v1/users/assignable` y `GET /api/v1/route-access`. El tenant siempre procede del access token validado. Las escrituras requieren `Admin`; la lectura operativa filtra rutas activas por administrador, supervisor responsable o cobrador asignado. El contrato completo y los criterios de auditoría están en [`DECISION_AUTENTICACION_JWT.md`](../cobroup.md/DECISION_AUTENTICACION_JWT.md) y [`ADR-007`](docs/decisions/0007-symmetric-hs256-per-environment.md).

> **Aviso de madurez:** la decisión `HS256` está aprobada, pero no se presume implementada. Antes de desplegar deben cerrarse y auditarse los controles de [`BACKLOG_SEGURIDAD_FIRMA_SIMETRICA.md`](../cobroup.md/BACKLOG_SEGURIDAD_FIRMA_SIMETRICA.md), incluida la rotación coordinada y la consistencia multirréplica.
