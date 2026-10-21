# MS.Cobroup.BackendAdmin

Microservicio administrativo y autoridad única de identidad de COBROUP. Los usuarios son identidades globales y pueden tener membresías con rol en varias organizaciones. El login usa solamente usuario y contraseña, selecciona la última organización accedida y permite cambiarla emitiendo una sesión nueva. La fase 3 permanece pausada mientras se estabilizan estas bases.

## Requisitos y ejecución

```powershell
dotnet restore MS.Cobroup.BackendAdmin.slnx
dotnet build MS.Cobroup.BackendAdmin.slnx --no-restore
dotnet test MS.Cobroup.BackendAdmin.slnx --no-build
dotnet tool restore
dotnet dotnet-ef database update --project MS.Cobroup.BackendAdmin.Infrastructure --startup-project MS.Cobroup.BackendAdmin.Api
dotnet run --project MS.Cobroup.BackendAdmin.Api --launch-profile https
```

La conexión se sobrescribe mediante User Secrets con la clave `ConnectionStrings:IdentityDatabase`; la contraseña no está versionada. Para crear o reconciliar los clientes OIDC tras aplicar la migración, establece temporalmente `Identity__SeedClientsOnStartup=true`. El bootstrap requiere `Bootstrap__Key` y el encabezado `X-Bootstrap-Key`; la marca persistida impide una segunda ejecución.

El entorno local ya contiene una organización `COBROUP` y un usuario para cada rol: `admin`, `supervisor` y `collector`. Sus contraseñas aleatorias solo existen en User Secrets bajo `LocalBootstrap:AdminPassword`, `LocalBootstrap:SupervisorPassword` y `LocalBootstrap:CollectorPassword`. Pueden consultarse localmente con `dotnet user-secrets list --project MS.Cobroup.BackendAdmin.Api` sin copiarlas a archivos versionados.

Endpoints: `GET /health`, discovery OIDC en `/.well-known/openid-configuration`, sesión de autoridad en `/api/v1/auth/session`, organizaciones activas en `/api/v1/auth/organizations`, bootstrap en `/api/v1/bootstrap`, alta administrativa en `POST /api/v1/routes` y protocolo en `/connect/*`. Todos los endpoints propios están organizados por controladores en la capa API. En producción son obligatorios certificados separados de firma y cifrado mediante `Identity__Certificates__*`.
