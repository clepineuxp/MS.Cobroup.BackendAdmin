# AGENTS — MS.Cobroup.BackendAdmin

## Identidad y restricciones del proyecto

- Producto: COBROUP
- Rol: microservicio administrativo por tenant.
- Documento maestro: `../cobroup.md/COBROUP.md` 1.11.0 y `../cobroup.md/DECISION_AUTENTICACION_JWT.md`.
- Estándar: `../cobroup.md/FRAMEWORK_DESARROLLO_CSHARP.md` 2.0.
- Rama activa observada: `feature/organizaciones-usuarios`, creada desde `dev` por instrucción del usuario.
- Prohibidos push, merge, tag y PR sin autorización.

## Estado actual

- Fase: 9
- Resultado: IDENTIDAD_JWT_REABIERTA_PARA_ADECUACION_Y_AUDITORIA
- Plataforma: .NET SDK 10.0.401, `net10.0`, C# 14.
- Auditoría independiente: realizada el 2026-10-22; resultado técnico NO APROBADO.
- Línea base funcional versionada; autenticación JWT exclusiva pendiente de conformidad. Auditoría técnica: `../cobroup.md/informe/auditoria-backend-csharp-2026-10-22-v2.html`; adenda vigente: `../cobroup.md/informe/linea-base-auditoria-jwt-2026-10-22.html`.
- Corte actual: el árbol contiene cambios JWT y migraciones sin commit. No se autoriza cierre, commit ni despliegue por el solo hecho de compilar.

## Decisión vigente de autenticación

- `MS.Cobroup.BackendAdmin` es la única autoridad de identidad para login, emisión, refresh y revocación bajo `../cobroup.md/DECISION_AUTENTICACION_JWT.md`.
- Se usan exclusivamente access y refresh tokens JWT con firma simétrica `HS256`, con secreto independiente por ambiente (`dev`, `qa`, `prod`). No se utilizan OIDC, OpenIddict, JWKS, RSA, PEM/PFX ni algoritmos asimétricos.
- `BackendAdmin` es el único emisor lógico JWT. El secreto simétrico se provee vía variables de entorno / Kubernetes Secret `jwt-signing-secret` (`Jwt__SigningKeyBase64`, `Jwt__ActiveKeyId`, y opcionalmente `Jwt__PreviousValidationKeys__0__KeyId`, `Jwt__PreviousValidationKeys__0__SigningKeyBase64` durante ventanas de rotación).
- La clave activa debe decodificar a mínimo 32 bytes CSPRNG y poseer `kid` explícito sin defaults en runtime (cumpliendo `^[A-Za-z0-9._-]{1,64}$`).
- Access JWT: máximo 15 minutos, audience `cobroup-api`, validación estricta y entrega al frontend solo para memoria.
- Refresh JWT: máximo 30 días, audience `cobroup-refresh`, cookie HttpOnly/Secure/SameSite, hash server-side, `SigningKeyId` persistido en la entidad `RefreshToken`, uso único, rotación atómica, familia, detección de replay y revocación atómica por `SigningKeyId` al retirar claves en rotación.
- El cambio de organización revoca la familia anterior antes de emitir tokens del nuevo tenant.
- Las referencias de identidad anteriores dentro del historial append-only son evidencia histórica y están supersedidas; no son planeación ni criterio de aceptación vigente.

## Historial cronológico (append-only)

### 2026-10-21 — Inicialización fase 1

- Prompt: inicializar los cuatro repositorios COBROUP sin desarrollar módulos posteriores.
- Autorización Git: `feature/fase-1-inicializacion`; commit autorizado: NO.
- Alcance: solución hexagonal, API, health, autorización preliminar, pruebas y documentación.
- Decisiones: sin Worker, base de datos, broker, proveedor de identidad ni CRUD.
- Archivos: solución, capas, API, pruebas, contexto, contratos, decisión, roadmap, README y gitignore.
- Comandos iniciales: `dotnet new`, `dotnet sln add`, `dotnet add reference`; completados.
- Verificación final: `dotnet restore`, `dotnet build --no-restore` (0 warnings, 0 errors), `dotnet test --no-build` (4/4) y API temporal; `GET http://127.0.0.1:5082/health` respondió 200 `Healthy`. El escaneo de residuos/secretos conocidos no encontró coincidencias.
- Riesgos: autoridad de identidad e integración con backend operativo siguen pendientes.
- Próxima acción sugerida: cerrar fase 1; no iniciar identidad/configuración sin aprobación.

### 2026-10-21 — Renombrado y redirección GitHub

- Prompt: cambiar la carpeta backend administrativa a `MS.Cobroup.BackendAdmin` y redirigirla al repositorio GitHub indicado.
- Rama: `feature/fase-1-inicializacion`; commit autorizado: NO.
- Cambios: carpeta renombrada desde `MS.cobroup-backend-admin`; `origin` actualizado a `https://github.com/clepineuxp/MS.Cobroup.BackendAdmin.git`; referencias documentales sincronizadas.
- Validación: rama, estado local y remoto verificados después del movimiento; no se ejecutó push.

### 2026-10-21 — Cierre versionado de fase 1

- Prompt: realizar commits de fase 1 y pasar a fase 2.
- Resultado: fase 1 validada; commit autorizado: SÍ, únicamente para el alcance acumulado de fase 1.
- Operaciones no autorizadas: push, merge, tag y PR.
- Siguiente rama prevista: `feature/fase-2-identidad-tenant`.

### 2026-10-21 — Identidad y tenant, corte inicial de fase 2

- Confirmación: autoridad única en BackendAdmin, PostgreSQL, usuario único por organización, JWT corto, refresh rotatorio y bootstrap único auditable.
- Implementación: entidades y puertos de identidad; EF Core/Npgsql y migración inicial; PasswordHasher; bootstrap transaccional; sesión protegida con antiforgery y rate limit; OpenIddict Authorization Code + PKCE, refresh y revocación; certificados externos obligatorios fuera de Development.
- Validación: build sin warnings; pruebas Core 5/5 e Infrastructure 2/2; generación de script idempotente EF correcta; health HTTPS 200 y discovery OIDC 200.
- Integración PostgreSQL: el almacén User Secrets fue vinculado a la API sin exponer la cadena; la migración inicial se aplicó en PostgreSQL 18 y una segunda ejecución confirmó que la base estaba actualizada.
- Integración OIDC: los dos clientes públicos fueron creados y luego reconciliados de forma repetible; solicitudes PKCE con scopes `openid profile offline_access cobroup-api` fueron aceptadas y redirigidas a los accesos administrativo y operativo correctos.
- Bootstrap confirmado: se creó una sola organización `COBROUP` y exactamente un usuario por rol (`Admin`, `Supervisor`, `Collector`). Las contraseñas quedaron únicamente en User Secrets bajo `LocalBootstrap:*` y no se imprimieron ni versionaron. Un segundo intento devolvió 409, confirmando la ejecución única.
- Validación integral: para los tres usuarios se verificaron login 204, autorización PKCE 302, token 200, `userinfo` 200 con el rol esperado, rotación de refresh token 200 y revocación 200. Health, discovery, migración repetible y reconciliación de clientes OIDC también quedaron validados.
- Git: commit de cierre de fase 2 autorizado por el usuario; push, merge, tag y PR no autorizados.

### 2026-10-21 — Inicio de configuración y rutas, fase 3

- Alcance inicial: alta de ruta con supervisor y uno o más cobradores; tenant derivado del token; autorización `Admin` en API y Core; roles/usuarios activos del mismo tenant; nombre único por organización; auditoría en la misma transacción.
- Persistencia: entidades de ruta/asignación/auditoría y migración `AddRouteManagement`, aplicada sobre la base local que conserva el bootstrap de fase 2.
- Validación: build con 0 warnings/errores; Core 9/9 e Infrastructure 3/3; segunda ejecución de migración sin cambios pendientes; API inició correctamente, `/health` respondió 200 y `POST /api/v1/routes` sin token fue rechazado con 401.
- Pendiente de fase: consultas, edición/desactivación, reasignación, configuración organizacional, cupos/herencias e integración frontend.
- Git: cambios de fase 3 todavía sin commit; push, merge, tag y PR no autorizados.

### 2026-10-21 — Orígenes frontend fase 3

- Clientes OIDC locales actualizados: operativo `http://localhost:5200` y administrativo `http://localhost:5300`, conservando coincidencia exacta para callback y cierre de sesión.
- Git: cambio sin commit; push, merge, tag y PR no autorizados.

### 2026-10-21 — Pausa y refinamiento de identidad/API

- Decisión: usuario global con username único; pertenencia y rol en `OrganizationMembership`; login sin organización; tenant inicial según `LastAccessedAtUtc`; desempate inicial por incorporación más reciente.
- Sin membresía activa: la sesión queda identificada sin claims tenant y responde `membership_required`; no se emite token tenant. La solicitud de ingreso se muestra como estado, pero su flujo de aprobación queda pendiente de definición.
- Cambio de organización: listado y selección protegidos por cookie/antiforgery; actualiza último acceso, reemplaza la sesión de autoridad y el frontend revoca el refresh token anterior antes de reautorizar.
- API: handlers propios trasladados desde `Program.cs` a `AuthController`, `OrganizationSessionsController`, `OidcController`, `BootstrapController`, `RoutesController` y `HealthController`.
- Datos: migración `GlobalUsersAndMemberships` aplicada con backfill antes de retirar columnas antiguas; login real del administrador 204, una membresía `COBROUP/Admin` y selección 204.
- Validación final: build con 0 warnings/errores; Core 11/11 e Infrastructure 3/3; migración repetible sin pendientes; health 200; autorización OIDC aceptada para callback administrativo.
- Incidencia: una prueba exploratoria posterior con `HttpClient` desde PowerShell falló por construcción local del cliente/formulario; no alteró datos ni secretos. Las validaciones HTTP anteriores y las pruebas automatizadas sí completaron.
- Git: cambios sin commit; fase 3 funcional pausada; push, merge, tag y PR no autorizados.

### 2026-10-21 — Checkpoint y reanudación de fase 3

- Git: checkpoint previo confirmado en `d8885d7` (`feat: establish global memberships and route management`); repositorio limpio tras el commit. Sin push, merge, tag ni PR.
- Backend: `GET /api/v1/routes` lista solo rutas del tenant del token; `GET /api/v1/users/assignable` expone únicamente membresías activas `Supervisor`/`Collector` del mismo tenant. Ambos requieren `Admin` en API y Core.
- Diseño: cada responsabilidad conserva controlador propio (`RoutesController`, `UsersController`); la extracción de actor tenant se centralizó en `TenantActorReader`.
- Pruebas: Core 15/15. Build completo Release con 0 warnings y 0 errores; se usó Release porque una instancia Debug abierta desde Visual Studio bloqueaba sus DLL sin afectar el código.
- Git: este nuevo corte de fase 3 queda sin commit; push, merge, tag y PR no autorizados.

### 2026-10-21 — Cierre funcional de fase 3

- Alcance: configuración organizacional; cupos generales opcionales; rutas con herencia/sobrescritura; alta, consulta, edición, desactivación y reasignación; catálogo tenant-safe; lectura operativa por rol/asignación.
- Seguridad: tenant exclusivamente desde token; escrituras `Admin` en API y Core; rutas operativas activas filtradas para `Admin`, supervisor responsable o cobrador asignado.
- Auditoría: cambios de configuración y ruta guardan actor/fecha; reasignaciones, estado y cupos conservan detalle JSON antes/después en la misma transacción.
- Persistencia: migraciones `CompleteRouteConfiguration` y `AddAdministrativeAuditDetails` aplicadas; segunda ejecución confirmó base actualizada.
- Validación: build Release 0 warnings/errores; Core 22/22 e Infrastructure 3/3; integración OIDC real con login 204, autorización 302, token 200 y lecturas administrativas/operativas 200. No se crearon rutas ficticias.
- Dependencias diferidas: orden de clientes y georreferenciación requieren Customer/Location y decisión de mapas, por lo que permanecen en sus fases propietarias.
- Git: cambios de cierre sin commit; push, merge, tag y PR no autorizados.

### 2026-10-21 — Consolidación del informe de avance

- Documentación: el maestro 1.0.5 formaliza 9 fases totales, 3 completas y 6 pendientes; el detalle se publicó en `../cobroup.md/informe/avance.md` y `avance.html`.
- Roadmap: numeración sincronizada de fases 2–9, sin ampliar ni autorizar alcance funcional.
- Git: no se creó commit, push, merge, tag ni PR.

### 2026-10-21 — Cierre funcional de fase 4

- API: fachada administrativa de clientes con controlador propio; consulta, detalle, traslado de ruta y cambio de estado reservados a `Admin`, sin duplicar persistencia operativa.
- Integración: validador local reutilizable ejecutó Authorization Code + PKCE sin imprimir secretos; login 204, token 200, API operativa 200 y fachada administrativa 200.
- Validación: build Release 0 warnings/errores; Core 24/24 e Infrastructure 5/5.
- Git: commit de fase 4 autorizado; push, merge, tag y PR permanecen prohibidos.

### 2026-10-21 — Inicio de fase 5: catálogo de plantillas

- Rama: `feature/fase-5-plantillas-excepciones`; fase 4 ya comprometida y repositorio limpio al iniciar.
- Implementación: autoridad tenant-safe de `LoanTemplate` y `LoanTemplateVersion`, clasificación, borrador/publicada/inactiva, límites exactos, porcentajes deduplicados, historial inmutable y auditoría transaccional.
- Contratos: controlador dedicado `GET|POST /api/v1/loan-templates`, `GET|PUT /api/v1/loan-templates/{id}` y lectura publicada `GET /api/v1/loan-templates/available`.
- Persistencia: migración `AddLoanTemplateCatalog` aplicada a PostgreSQL; segunda ejecución sin pendientes.
- Validación: build Release sin advertencias; Core 33/33 e Infrastructure 5/5.
- Pendiente de fase: solicitudes excepcionales por cliente y decisión exclusiva de Admin; ADR-003 impide anticipar originación o cálculo financiero real.
- Git: cambios de fase 5 sin commit; push, merge, tag y PR no autorizados.

### 2026-10-21 — Cierre funcional de fase 5: bandeja y decisión de excepciones

- Fachada administrativa: controlador dedicado `LoanExceptionsController` con endpoints `GET /api/v1/loan-exceptions`, `GET /api/v1/loan-exceptions/{id}` y `POST /api/v1/loan-exceptions/{id}/decision`, restringidos exclusivamente a la política `tenant-admin`.
- Integración: `OperationalLoanExceptionDirectoryClient` consume la API operativa reenviando el bearer token sin compartir esquemas de base de datos ni duplicar propiedad de datos.
- Auditoría administrativa: `AdministrativeAuditRecord.LoanExceptionDecided` persiste actor, organización, id de excepción y detalle JSON en `IdentityDbContext` con cada decisión exitosa (aprobación/rechazo).
- Seguridad y autorización (AC-08): verificada la denegación estricta en Core con 403 Forbidden para actores sin rol Admin antes de delegar a la autoridad operativa.
- Validación: build Release con 0 warnings y 0 errores; suite de pruebas ampliada a Core 36/36 e Infrastructure 8/8 (total 44/44).
- Git: commit de fase 5 autorizado por el usuario: `b3b8e34` (`feat(loan-exceptions): implement administrative exception decision proxy and audit trail`); push, merge, tag y PR permanecen prohibidos.

### 2026-10-21 — Cierre funcional de fase 6: supervisión administrativa de préstamos

- Fachada administrativa:
  - Controlador `LoansController` (`/api/v1/loans`) exponiendo búsqueda con filtros de ruta, cliente y estado (`GET /api/v1/loans`), detalle con cronograma de cuotas (`GET /api/v1/loans/{id}`), y consulta por cliente (`GET /api/v1/loans/by-customer/{customerId}`).
  - Servicio `LoanAdministrationService`: valida autorización exclusiva para rol `Admin` (`actor.Role is UserRole.Admin`) y delega a la autoridad operativa.
  - Integración desacoplada: `IOperationalLoanDirectory` y `OperationalLoanDirectoryClient` consumiendo la API operativa mediante reenvío del Bearer token sin compartir base de datos.
- Pruebas automatizadas:
  - Build: Release compilado con 0 warnings y 0 errors (`dotnet build -c Release`).
- Git: commit local `57d45d7` (`feat(loans): implement administrative loan supervision and audit proxy`) en rama `feature/fase-6-prestamos-cronograma`; push, merge, tag y PR no autorizados.

### 2026-10-21 — Cierre funcional de fase 7: catálogo y gestión de medios de pago

- Dominio y reglas de negocio:
  - Entidad `PaymentMethod` en `MS.Cobroup.BackendAdmin.Domain.PaymentMethods`: `OrganizationId`, `Name`, `NormalizedName`, `DisplayOrder`, `IsActive`, timestamps de auditoría.
  - Reglas: nombre obligatorio de 1 a 50 caracteres, unicidad por organización ignorando mayúsculas y acentos (`NormalizedName`), orden de visualización de 1 a 999.
  - Autoridad de administración: reservada exclusivamente al rol `Admin` (AC-13); cobradores y supervisores pueden consultar métodos activos mediante `/available`.
- Persistencia y base de datos:
  - Mapeo EF Core en `IdentityDbContext` dentro de la tabla `identity.payment_methods` con índice único sobre `(OrganizationId, NormalizedName)` y clave foránea a `organizations`.
  - Repositorio `EfPaymentMethodRepository` implementando `IPaymentMethodRepository`.
  - Migración EF Core `20261021215708_AddPaymentMethods` generada y aplicada exitosamente en PostgreSQL.
- API y controladores:
  - `PaymentMethodsController` (`/api/v1/payment-methods`): `GET /` (listado administrativo completo), `GET /available` (listado de medios activos para usuarios del tenant), `GET /{id}`, `POST /` (creación con auditoría), `PUT /{id}` (actualización con auditoría), `POST /{id}/toggle-status` (activación/desactivación reactiva).
  - Auditoría administrativa: `AdministrativeAuditRecord.PaymentMethodModified` registra actor, organización, id del medio y detalle JSON del cambio en `identity.administrative_audits`.
- Pruebas automatizadas:
  - Suite ampliada a 56 pruebas en verde: Core 47/47 (7 pruebas unitarias nuevas para `PaymentMethodService`: listado, duplicados, orden, toggle de estado, control de rol Admin) e Infrastructure 9/9 (metamodelo EF Core, unicidad por organización y nombre normalizado).
- Validación de arquitectura: cumplimiento estricto de FRAMEWORK_DESARROLLO_CSHARP 2.0 (organización hexagonal, límites de dominio, controllers delgados y persistencia desacoplada).
- Build: Release compilado con 0 warnings y 0 errors.
- Git: rama `feature/fase-7-pagos-conciliacion`; push, merge, tag y PR no autorizados.

### 2026-10-21 — Cierre versionado de fase 7 en Git

- Prompt: correcto, hacer commits de fase 7 e iniciar fase 8.
- Resultado: fase 7 validada; commit autorizado: SÍ, commit local `6d563c9` (`feat(payment-methods): implement tenant payment methods catalog and status management`).
- Rama de trabajo creada y activa: `feature/fase-8-mapas-analitica`.
- Operaciones no autorizadas: push, merge, tag y PR.

### 2026-10-21 — Implementación de fase 8: mapas, georreferenciación y analítica

- Casos de uso y Core:
  - `IOperationalAgendaDirectory` en `Core/Agenda`: contrato desacoplado para consulta segura de ubicaciones georreferenciadas y analítica operativa.
  - `MapSupervisionService`: orquestación de mapa de supervisión multi-ruta con agregación de clientes, préstamos y saldos.
  - `AnalyticsAdministrationService`: consolidación de analítica ejecutiva, ranking de rendimiento comparativo entre rutas del tenant y reporte global de envejecimiento de cartera (Aging Report).
- Infraestructura:
  - `OperationalAgendaDirectoryClient` en `Infrastructure/Agenda`: cliente HTTP autenticado reenviando Bearer token tenant-safe hacia el backend operativo con tolerancia a fallos.
- API y controladores:
  - `MapSupervisionController` (`/api/v1/map/route-locations`): endpoint de supervisión de ubicaciones georreferenciadas con filtros por ruta y fecha de corte.
  - `AnalyticsController` (`/api/v1/analytics`): endpoints `/overview` (resumen ejecutivo), `/routes-performance` (ranking comparativo de rutas) y `/aging` (reporte de vencimiento por tramos).
  - Registro de servicios en `Program.cs`.
- Pruebas automatizadas:
  - Suite ampliada a 60 pruebas en verde: Core 51/51 (pruebas unitarias para `MapSupervisionService` y `AnalyticsAdministrationService`) e Infrastructure 9/9.
- Validación de arquitectura: cumplimiento estricto de FRAMEWORK_DESARROLLO_CSHARP 2.0 (hexagonal, DTOs explícitos, controllers limpios de lógica).
- Build: Release compilado con 0 warnings y 0 errors (`dotnet build -c Release`).
- Git: rama `feature/fase-8-mapas-analitica`; push, merge, tag y PR no autorizados.

### 2026-10-21 — Cierre versionado de fase 8 en Git

- Prompt: correcto, hacer commits de fase 7 e iniciar fase 8 / proceder con la fase final.
- Resultado: fase 8 validada; commit autorizado: SÍ, commit local `ed609a5` (`feat(map-supervision): implement administrative map supervision and portfolio analytics facade`).
- Rama de trabajo creada y activa: `feature/fase-9-endurecimiento-despliegue`.
- Operaciones no autorizadas: push, merge, tag y PR.

### 2026-10-21 — Implementación de fase 9: endurecimiento y despliegue K3s/Kubernetes

- Observabilidad y Health Checks:
  - `IdentityDatabaseHealthCheck` creado en `MS.Cobroup.BackendAdmin.Api/Health/` implementando `IHealthCheck` para verificar conectividad activa con PostgreSQL de identidad (`SELECT 1`).
  - Registro en `Program.cs` de probes: `GET /health/live` (liveness) y `GET /health/ready` (readiness con comprobación de BD).
- Seguridad HTTP e Ingress:
  - `UseForwardedHeaders` configurado para soporte de cabeceras de proxy (`X-Forwarded-For`, `X-Forwarded-Proto`) de Ingress Traefik.
  - Middleware de cabeceras de seguridad: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `X-XSS-Protection: 0`.
  - Correlation ID middleware (`X-Correlation-ID`) para trazabilidad transversal.
- Contenedorización OCI / Docker:
  - `Dockerfile` multi-stage: build con .NET SDK 10, ejecución con runtime ASP.NET Core 10, usuario no root `1000:1000`, puerto 8080.
  - `.dockerignore` configurado.
- Orquestación K3s y CI/CD:
  - Manifiestos K3s (`k8s/base/backend-admin.yaml`, `namespace.yaml`, `ingress.yaml`, `configmap.yaml`, `secrets-template.yaml`).
  - Pipeline `.github/workflows/ci-cd.yml`: ejecución de tests, build y push a GHCR (`ghcr.io/clepineuxp/ms-cobroup-backendadmin`), y despliegue automatizado por SSH a K3s para ambientes `cobroup-dev`, `cobroup-qa`, `cobroup-prod`.
- Pruebas automatizadas y build:
  - 60 pruebas en verde: Core 51/51, Infrastructure 9/9.
  - Compilación Release con 0 warnings y 0 errores.
- Git: rama `feature/fase-9-endurecimiento-despliegue`; push, merge, tag y PR no autorizados.

### 2026-10-21 — Automatización de Bootstrap Local (`apply-bootstrap.ps1`)

- Script de inicialización: `scripts/apply-bootstrap.ps1` implementado para automatizar el aprovisionamiento de clientes OIDC (`SeedClientsOnStartup=true`), organización inicial `COBROUP` y cuentas administrativas/supervisor/cobrador (`/api/v1/bootstrap`) leyendo de forma segura las credenciales desde `UserSecrets`.
- Soporte para ejecución idempotente con detección de código HTTP `201 Created` / `409 Conflict` y parámetro opcional `-Database` para aprovisionar instancias alternativas.
- Git: commit local autorizado por el usuario para `scripts/apply-bootstrap.ps1`; push no autorizado.

### 2026-10-22 — Rebaselining auditor de identidad JWT

- Prompt: formalizar en toda la planeación la decisión de usar exclusivamente JWT y retirar el mecanismo anterior, actuando solo como auditor.
- Decisión: este servicio es el único emisor y custodio de la clave privada asimétrica; publica JWKS, rota refresh de un uso y revoca familias ante replay. La fase 2 queda reabierta.
- Documentación: maestro 1.7.0, decisión JWT, README, contexto, contratos, ADR, roadmap, avance, guía de ambientes y adenda HTML alineados.
- Auditoría: el contrato queda aprobado; la implementación actual sigue NO APROBADA y debe demostrar firma asimétrica, cookie segura, hash server-side, atomicidad, revocación, rotación de claves y fail-fast.
- Verificación: búsqueda transversal sin referencias normativas activas al mecanismo retirado; los informes y entradas históricas se conservaron como evidencia.
- Git: no se modificó código en esta actividad y no se creó commit, push, merge, tag ni PR.

### 2026-10-22 — Implementación integral de autoridad JWT asimétrica y remediación de hallazgos (Bloques A, B, D)

- Autoridad JWT asimétrica (RS256): implementados `IRsaKeyService`, `RsaKeyService` y `JwtTokenService`. BackendAdmin actúa como custodio exclusivo de clave privada. Publicación de claves públicas y rotación por JWKS en `GET /api/v1/auth/.well-known/jwks.json`.
- Fail-fast estricto: el arranque fuera de Development falla inmediatamente si falta configuración criptográfica o issuer/audiences.
- Access Token: duración máxima de 15 minutos, claims obligatorios (`sub`, `typ=access`, `aud=cobroup-api`, `organization_id`, `organization_code`, `role`), sin llaves simétricas ni fallback.
- Refresh Token & Cookies: entrega exclusivamente en cookie `HttpOnly`, `Secure`, `SameSite=Strict` (`__Host-cobroup-refresh`). Persistencia server-side únicamente del hash SHA-256 (`TokenHash`) con índice único.
- Rotación atómica y Replay: rotación en transacción PostgreSQL aislada con revocación atómica del token padre; detección de reutilización revoca la familia completa (`FamilyId`) y genera registro de seguridad sin guardar tokens en claro.
- Multi-tenancy & Cambio de organización: cambio de tenant revoca atómicamente la familia anterior. Rechazo explícito con 403 Forbidden si el tenant solicitado no corresponde a una membresía activa del usuario.
- Retiro de legado OpenIddict: eliminados del runtime paquetes OpenIddict, controladores `/connect/*`, seeders y servicios. Creada migración `20261022152043_RemoveLegacyOidcAndUpgradeRefreshTokens` que retira las 4 tablas OpenIddict heredadas y migra la estructura de refresh tokens.
- Reconciliación EF Core: `dotnet ef migrations has-pending-model-changes` finaliza con 0 cambios pendientes.
- Validación de pruebas: suite completa verde con 67/67 pruebas (Core 51/51, Infrastructure 16/16), incluyendo pruebas criptográficas, JWKS, claims, hash y revocación familiar.
- Git: cambios preservados en el working tree sin commit, push, merge, tag ni PR.

### 2026-10-22 — Remediación Bloqueante B-17 (Auditoría v5): Cambio de organización con revocación confirmada y transacción atómica PostgreSQL

- **Solicitud**: Remediar el bloqueante B-17 documentado en la auditoría v5 (`cobroup.md/informe/auditoria-backend-csharp-2026-10-22-v5.html`), garantizando que `PUT /api/v1/auth/organizations/current` exija simultáneamente Access JWT, Cookie Refresh Token y Antiforgery CSRF, ejecutando la revocación de la familia anterior y la persistencia de la nueva familia en una única transacción atómica de PostgreSQL con commit confirmado antes de responder o emitir cookies.
- **Implementación técnica**:
  - `MS.Cobroup.BackendAdmin.Domain/Interfaces/Services/ITokenService.cs`: Agregado método `SwitchOrganizationSessionAsync` y tipo de resultado `RevokeFamilyResult` para eliminar retornos silenciosos `void` y registrar formalmente la revocación.
  - `MS.Cobroup.BackendAdmin.Domain/Interfaces/Repositories/IIdentityRepository.cs`: Definido método transaccional atómico `SwitchOrganizationSessionAtomicAsync(Guid userId, string previousFamilyId, RefreshTokenRecord newRefreshToken, DateTimeOffset utcNow, CancellationToken ct)`.
  - `MS.Cobroup.BackendAdmin.Infrastructure/Persistence/EfIdentityRepository.cs`: Implementada transacción atómica PostgreSQL (`BeginTransactionAsync` con nivel de aislamiento y bloqueo `SELECT ... FOR UPDATE` sobre `identity.refresh_tokens`). La transacción valida simultáneamente: (1) que el refresh token pertenece al usuario y a la organización actual, (2) que no está revocado ni expirado, (3) revoca todos los tokens de la familia anterior (`FamilyId`), (4) inserta el nuevo refresh token de la nueva familia, y (5) efectúa `CommitAsync`. Si cualquier validación o paso falla, realiza `RollbackAsync` inmediato.
  - `MS.Cobroup.BackendAdmin.Api/Security/JwtTokenService.cs`: Validación criptográfica completa del refresh token entrante (RS256, typ=refresh, aud=cobroup-refresh, kid activo), comprobación de coincidencia `sub == actor.UserId`, `organization_id == actor.OrganizationId`, y orquestación con el repositorio.
  - `MS.Cobroup.BackendAdmin.Api/Controllers/OrganizationSessionsController.cs`: `SwitchOrganization` exige simultáneamente Bearer Access JWT (`[Authorize]`), cookie `__Host-cobroup-refresh` (devuelve 401 si falta y expira cookies espurias), cabecera antiforgery CSRF (`X-XSRF-TOKEN`), y valida que la organización solicitada corresponda a una membresía activa del usuario (`Active`). No emite respuesta HTTP ni cookie sin haber confirmado el commit de la base de datos. Ante fallo borra cookies inseguras con maxAge 0.
- **Archivos modificados/creados**:
  - Modificados: `MS.Cobroup.BackendAdmin.Domain/Interfaces/Services/ITokenService.cs`, `MS.Cobroup.BackendAdmin.Domain/Interfaces/Repositories/IIdentityRepository.cs`, `MS.Cobroup.BackendAdmin.Infrastructure/Persistence/EfIdentityRepository.cs`, `MS.Cobroup.BackendAdmin.Api/Security/JwtTokenService.cs`, `MS.Cobroup.BackendAdmin.Api/Controllers/OrganizationSessionsController.cs`, `MS.Cobroup.BackendAdmin.Domain/Identity/Organization.cs`, `MS.Cobroup.BackendAdmin.Domain/Identity/OrganizationMembership.cs`, `tests/MS.Cobroup.BackendAdmin.Infrastructure.Tests/Security/JwtTokenServiceTests.cs`, `tests/MS.Cobroup.BackendAdmin.Infrastructure.Tests/Security/AuthLifecycleHttpTests.cs`, `tests/MS.Cobroup.BackendAdmin.Infrastructure.Tests/Persistence/PostgresRefreshTokenConcurrencyTests.cs`, `AGENTS.md`.
- **Resultados de pruebas**:
  - Pruebas HTTP completas `AuthLifecycleHttpTests.cs`: 8 escenarios HTTP de extremo a extremo utilizando `WebApplicationFactory` con base PostgreSQL real y limpieza garantizada en `IAsyncLifetime`:
    1. Cambio correcto revoca familia previa y genera exactamente una nueva familia.
    2. Refresh token de la familia anterior queda revocado y ya no puede rotar (401).
    3. Petición sin cookie refresh retorna 401 y expira cookie de forma segura.
    4. Petición sin token antiforgery CSRF retorna 400 y no muta ninguna familia.
    5. Cambio a organización suspendida retorna 403 Forbidden.
    6. Cambio a organización sin membresía activa retorna 403 Forbidden.
  - Prueba de concurrencia real `PostgresRefreshTokenConcurrencyTests.cs`:
    - `ConcurrentOrganizationSwitch_OnRealPostgreSql_AllowsExactlyOneSuccess_AndRevokesPreviousFamily`: Dos hilos concurrentes compitiendo con barrera sincronizada sobre PostgreSQL real; exactamente 1 transacción tiene éxito y 1 es rechazada por revocación/bloqueo de fila, previniendo ramas concurrentes o duplicación de familias activas.
  - Suite completa `MS.Cobroup.BackendAdmin.slnx`: 92/92 pruebas pasando (Core 51/51, Infrastructure 41/41).
  - Compilación Release: 0 warnings, 0 errors (`dotnet build -c Release --no-incremental`).
  - EF Core: 0 cambios pendientes de modelo (`dotnet ef migrations has-pending-model-changes`).
- **Git**: Cambios preservados en working tree sobre rama `feature/solucion-hallazgos-iniciales`; sin commit, push, merge, tag ni PR.

### 2026-10-22 — Decisión arquitectónica: Deferimiento de claves PEM externas y proveedor RSA efímero para desarrollo

- **Decisión obligatoria aplicada**:
  > COBROUP continuará usando JWT firmados. En la fase actual de desarrollo y validación no se configurará una clave privada externa en formato PEM. BackendAdmin utilizará un proveedor RSA efímero generado en memoria, habilitado de forma explícita y restringido a entornos no productivos. La clave y el `kid` cambian al reiniciar la instancia; en consecuencia, los JWT emitidos antes del reinicio pueden dejar de ser válidos. Este modo no se considera apto para producción, alta disponibilidad ni múltiples réplicas emisoras. La custodia de una clave persistente, su rotación, solapamiento de claves y recuperación operativa mediante Kubernetes Secret, PFX, PEM, AWS KMS u otro proveedor aprobado quedan registrados como backlog obligatorio previo a producción.
- **Implementación técnica**:
  - `MS.Cobroup.BackendAdmin.Api/Security/IJwtSigningKeyProvider.cs`: Puerto hexagonal que abstrae la firma JWT (`ActiveKeyId`, `GetSigningCredentials()`, `GetValidationKeys()`, `CreateTokenValidationParameters(...)`, `GetPublicJwks()`, `IDisposable`).
  - `MS.Cobroup.BackendAdmin.Api/Security/EphemeralRsaSigningKeyProvider.cs`: Adaptador RSA 2048-bit en memoria. Confinamiento estricto en RAM; parámetros privados (`d`, `p`, `q`, `dp`, `dq`, `qi`) nunca exportados a JWKS, logs ni archivos; `kid` único no ambiguo por ciclo de vida (`cobroup-ephemeral-{Guid}`); registrado como Singleton en DI y con liberación determinista de recursos criptográficos (`IDisposable`).
  - `MS.Cobroup.BackendAdmin.Api/Security/JwtSigningKeyProviderFactory.cs`: Factoría fail-fast que exige `Jwt:SigningProvider=EphemeralRsa` y `Jwt:AllowEphemeralSigning=true` en `Development`. Rechaza estrictamente el proveedor efímero fuera de Development (Staging, QA, Production). Marca el proveedor persistente como backlog (`NotSupportedException`).
  - `MS.Cobroup.BackendAdmin.Api/Security/JwtTokenService.cs`: Inyección de `IJwtSigningKeyProvider`, desacoplando la emisión y validación de tokens de cualquier conocimiento sobre PEM, rutas de disco o Kubernetes.
  - `MS.Cobroup.BackendAdmin.Api/Controllers/AuthController.cs`: Endpoint `.well-known/jwks.json` expone únicamente claves públicas del proveedor activo.
  - `MS.Cobroup.BackendAdmin.Api/Program.cs`: Registro de singleton `IJwtSigningKeyProvider` y configuración de `AddJwtBearer` ligada a `CreateTokenValidationParameters`.
  - `MS.Cobroup.BackendAdmin.Api/appsettings.json` y `appsettings.Development.json`: Configuración explícita `Jwt:SigningProvider` y `Jwt:AllowEphemeralSigning`.
- **Pruebas automatizadas**:
  - `tests/MS.Cobroup.BackendAdmin.Infrastructure.Tests/Security/EphemeralRsaSigningKeyProviderTests.cs`: 9 pruebas unitarias e integradas cubriendo:
    1. Arranque en Development sin PEM cuando EphemeralRsa está autorizado explícitamente.
    2. Fail-fast si `AllowEphemeralSigning` no está presente o es `false`.
    3. Fail-fast en Staging/QA/Production rechazando EphemeralRsa sin autorización de riesgo.
    4. Access y refresh JWT firmados con RS256 conteniendo el `kid` activo.
    5. Validación criptográfica de tokens con la clave pública del JWKS.
    6. JWKS omite estrictamente parámetros privados (`d`, `p`, `q`, `dp`, `dq`, `qi`).
    7. Emisión y JWKS comparten la misma instancia singleton.
    8. Dos instancias independientes generan claves y `kid`s divergentes.
    9. Un token emitido por una instancia es rechazado por otra instancia independiente (demostración del límite efímero).
  - Suite de pruebas completa: 102/102 pruebas en verde (Core 51/51, Infrastructure 51/51).
  - Compilación Release: 0 warnings, 0 errors (`dotnet build MS.Cobroup.BackendAdmin.slnx -c Release --no-incremental`).
  - EF Core: 0 cambios pendientes de modelo (`dotnet ef migrations has-pending-model-changes`).
- **Limitación y backlog**:
  - El modo efímero actual no es apto para producción, alta disponibilidad ni múltiples réplicas detrás de un balanceador.
  - Backlog formalizado en `cobroup.md/BACKLOG_SEGURIDAD_PRODUCTIVA.md` y `ADR-0006`: implementación de proveedor persistente (Kubernetes Secret con PEM/PFX o KMS) bloquea formalmente la salida a producción.
- **Git**: Cambios preservados en el working tree sobre rama `feature/solucion-hallazgos-iniciales`; sin commit, push, merge, tag ni PR.

### 2026-10-22 — Remediación Hallazgo B-19: Mitigación de coexistencia de emisores efímeros mediante estrategia Recreate

- **Hallazgo atendido (B-19)**:
  La suite de pruebas `EphemeralRsaSigningKeyProviderTests` demuestra que dos instancias en memoria generan claves y `kid`s divergentes y se rechazan mutuamente los tokens. Para evitar que un `RollingUpdate` con `maxSurge > 0` provoque la coexistencia de dos pods incompatibles detrás del Service `admin-backend`, se establece la estrategia `strategy: type: Recreate` para desarrollo.
- **Alineación y documentación**:
  - Actualizado `ADR-0006` (punto 4 de limitaciones operativas) y `README.md` de BackendAdmin.
  - Se formaliza la aceptación de la indisponibilidad breve durante el reinicio del pod en desarrollo.
- **Git**: Cambios preservados en el working tree sobre rama `feature/solucion-hallazgos-iniciales`; sin commit, push, merge, tag ni PR.

### 2026-10-22 / 2026-10-23 — Migración integral a JWT simétrico HS256 por ambiente (BackendAdmin)

- **Decisión aplicada**: `cobroup.md/DECISION_AUTENTICACION_JWT.md`, `BACKLOG_SEGURIDAD_FIRMA_SIMETRICA.md` y `MS.Cobroup.BackendAdmin/docs/decisions/0007-symmetric-hs256-per-environment.md`.
- **Componentes retirados**:
  - Eliminados: `EphemeralRsaSigningKeyProvider.cs`, `IJwtSigningKeyProvider.cs`, `IRsaKeyService.cs`, `RsaKeyService.cs`, `JwtSigningKeyProviderFactory.cs` y `EphemeralRsaSigningKeyProviderTests.cs`.
  - Eliminado el endpoint JWKS `GET /api/v1/auth/.well-known/jwks.json` de `AuthController.cs`.
  - Eliminada configuración legacy: `Jwt:SigningProvider`, `Jwt:AllowEphemeralSigning` y flags de acknowledge.
- **Implementación técnica**:
  - `MS.Cobroup.BackendAdmin.Api/Security/ISymmetricKeyService.cs` y `SymmetricKeyService.cs`:
    - Proveedor simétrico singleton con fail-fast estricto en startup: valida que `Jwt:SigningKeyBase64` esté presente, no vacío, sin placeholders, sea Base64 válido y decodifique a mínimo 32 bytes (256 bits).
    - Exclusividad de algoritmo: `SecurityAlgorithms.HmacSha256` (`alg=HS256`).
    - Emisión con `ActiveKeyId` configurado explícitamente (`k1`).
    - Soporte de claves anteriores (`Jwt:PreviousValidationKeys`) exclusivamente para validación durante ventanas acotadas de rotación.
  - `MS.Cobroup.BackendAdmin.Api/Security/JwtTokenService.cs`:
    - Emisión de access y refresh tokens firmados exclusivamente con `HS256` y `kid`.
    - Validación estricta de cookies de refresh con allowlist de algoritmos `HS256`.
  - `MS.Cobroup.BackendAdmin.Api/Security/AccessJwtValidator.cs`:
    - Validación estricta con `ValidAlgorithms = [SecurityAlgorithms.HmacSha256]`.
  - `MS.Cobroup.BackendAdmin.Api/Program.cs`:
    - Registro de `ISymmetricKeyService` como Singleton y configuración de `JwtBearerOptions` con parámetros simétricos.
- **Pruebas automatizadas**:
  - Creada suite `SymmetricKeyServiceTests.cs` (16 pruebas): fail-fast por clave ausente, vacía, placeholder, base64 inválido, 31 bytes; aceptación de 32 bytes; emisión con clave activa; validación de clave activa y anterior; rechazo por retiro de clave anterior; consistencia multirréplica; y rechazo de algoritmos `none`, RS256, ES256, HS384, HS512.
  - Actualizadas `AccessJwtValidationTests.cs`, `JwtTokenServiceTests.cs`, `AuthLifecycleHttpTests.cs`, `PostgresRefreshTokenConcurrencyTests.cs`.
  - Suite completa: 115/115 pruebas pasando en Release (Core 51/51, Infrastructure 64/64).
  - Compilación Release: 0 warnings, 0 errors.
  - EF Core: 0 cambios pendientes de modelo (`dotnet ef migrations has-pending-model-changes`).
- **Git**: Cambios preservados en working tree sobre rama `feature/solucion-hallazgos-iniciales`; sin commit, push, merge, tag ni PR.

### 2026-10-23 — Remediación v9: Contrato único de rotación, persistencia de SigningKeyId y revocación atómica

- **Auditoría atendida**: `cobroup.md/informe/auditoria-backend-csharp-2026-10-23-v9.html`.
- **Contrato único de configuración**:
  - `Jwt__ActiveKeyId`, `Jwt__SigningKeyBase64`, `Jwt__PreviousValidationKeys__0__KeyId`, `Jwt__PreviousValidationKeys__0__SigningKeyBase64`.
  - Fail-fast estricto en `SymmetricKeyService`: validación de formato `kid` con regex `^[A-Za-z0-9._-]{1,64}$`, rechazo de defaults en runtime (`k1`), verificación estricta de pares en previous keys (ambos presentes o ambos ausentes), rechazo de hijos incompletos (sin `continue` silencioso), verificación de propiedades desconocidas y rechazo de coincidencia de material o `kid` entre activo y previo.
- **Entidad y persistencia de RefreshToken**:
  - Añadido `SigningKeyId` (varchar(64), obligatorio) a `RefreshToken` para asociar atómicamente cada sesión/familia a la clave que emitió su credencial.
  - `JwtTokenService` propaga `ActiveKeyId` al crear y rotar refresh tokens.
  - Implementado `RevokeRefreshTokensBySigningKeyIdAsync` en `IIdentityRepository` y `EfIdentityRepository` para revocación atómica durante la etapa de retiro de clave (Etapa 3 del runbook).
  - Migración EF Core `20261023151001_AddSigningKeyIdToRefreshTokens` aplicada a la base de datos de pruebas.
- **Limpieza de configuración huérfana**:
  - Eliminados bloques huérfanos `Identity:Clients` y `SeedClientsOnStartup` en `appsettings.json` y `appsettings.Development.json`.
- **Pruebas y verificación**:
  - Suite completa: 129/129 pruebas pasando (Core 51/51, Infrastructure 78/78).
  - Cero cambios pendientes de modelo en EF Core (`has-pending-model-changes`).
- **Git**: Cambios preservados en working tree sobre rama `feature/solucion-hallazgos-iniciales`; sin commit, push, merge, tag ni PR.
### 2026-10-23 — Remediación obligatoria de hallazgo B-21: Rotación HS256, eliminación de defaults y CLI de revocación

- **Auditoría atendida**: `cobroup.md/informe/auditoria-backend-csharp-2026-10-23-v10.html` (Hallazgo B-21).
- **Eliminación total de defaults `k1`**:
  - `MS.Cobroup.BackendAdmin.Domain/Identity/RefreshToken.cs`: Eliminado valor por defecto `"k1"` de la propiedad y del parámetro de constructor; exige `signingKeyId` obligatorio y válido cumpliendo `^[A-Za-z0-9._-]{1,64}$`.
  - `MS.Cobroup.BackendAdmin.Infrastructure/Persistence/IdentityDbContext.cs`: Eliminado `.HasDefaultValue("k1")` sobre la columna `SigningKeyId`.
  - Migración EF Core `20261023151001_AddSigningKeyIdToRefreshTokens`: Actualizada con purga segura y limpia de refresh tokens heredados sin clave (`DELETE FROM refresh_tokens;`).
  - Eliminado `"ActiveKeyId": "k1"` de `MS.Cobroup.BackendAdmin.Api/appsettings.json` y `appsettings.Development.json`. Fail-fast en arranque si `Jwt:ActiveKeyId` falta o no cumple regex, incluso en entorno Development.
- **Revocación operacional para Kubernetes Job**:
  - `IIdentityRepository` y `EfIdentityRepository`: Implementado `RevokeAllRefreshTokensAsync` para soporte de revocación total en contingencia/emergencia.
  - `IOperationalSessionRevocationService` e implementación en `MS.Cobroup.BackendAdmin.Core/Security/OperationalSessionRevocationService.cs`: Orquestación de revocación por `kid` y revocación total.
  - `MS.Cobroup.BackendAdmin.Api/Program.cs`: Modo de comando CLI `--revoke-kid <kid>` y `--revoke-all`. Valida argumentos, ejecuta vía DI sobre PostgreSQL real, retorna JSON estructurado a stdout con conteo y timestamp no sensible, y finaliza con código de salida 0 (éxito) o 1 (error), sin exponer endpoints HTTP ni contraseñas.
  - Añadido `IdentityDbContextFactory` para soporte de EF Core tools en tiempo de diseño.
- **Pruebas automatizadas**:
  - Implementada suite `B21MandatoryRotationTests.cs` (13 pruebas) en `MS.Cobroup.BackendAdmin.Infrastructure.Tests/Security/`: startup fail-fast por falta de `ActiveKeyId`, validación de constructor `RefreshToken`, regex de `kid`, atómica de switch de organización, persistencia de `SigningKeyId` nuevo al rotar, y simulación de revocación por CLI.
  - Suite completa: 142/142 pruebas en verde (Core 51/51, Infrastructure 91/91).
  - Cero cambios pendientes de modelo en EF Core (`has-pending-model-changes`).
- **Git**: Cambios preservados en working tree sobre rama `feature/solucion-hallazgos-iniciales`; sin commit, push, merge, tag ni PR.

### 2026-10-23 — Desacoplamiento total de pruebas entre BackendAdmin y Backend

- **Problema corregido**: `MS.Cobroup.BackendAdmin.Infrastructure.Tests.csproj` referenciaba directamente `../MS.Cobroup.Backend/MS.Cobroup.Backend.Api.csproj`; funcionaba únicamente cuando ambos repositorios estaban presentes como carpetas hermanas y fallaba en GitHub Actions, donde cada repositorio se descarga de forma independiente.
- **Implementación**:
  - Eliminado el `ProjectReference` externo y el alias `BackendApi`.
  - `B21MandatoryRotationTests` valida ahora exclusivamente el contrato de emisión, solapamiento, rotación y retiro propiedad de BackendAdmin mediante `SymmetricKeyService` y `JwtTokenService` locales.
  - Añadida prueba arquitectónica que inspecciona todos los `.csproj` y falla si cualquier `ProjectReference` resuelve fuera de la raíz de `MS.Cobroup.BackendAdmin`.
- **Verificación**: solución restaurada y compilada en Release con 0 warnings/errores; 148/148 pruebas aprobadas (Core 51/51, Infrastructure 97/97).
- **Git**: Cambios preservados en working tree; sin commit, push, merge, tag ni PR.

### 2026-10-23 — PostgreSQL efímero para pruebas de integración en GitHub Actions

- **Problema corregido**: el job `Run Automated Tests` no aprovisionaba PostgreSQL ni definía `ConnectionStrings__IdentityDatabase`; por ello fallaban exactamente las 8 pruebas HTTP de ciclo de autenticación y las 2 pruebas de concurrencia PostgreSQL, aunque las 87 pruebas sin dependencia de base de datos aprobaban.
- **Implementación**:
  - Añadido un service container `postgres:18-alpine` aislado al job de pruebas, con health check y credenciales exclusivas no productivas.
  - Añadida la variable de conexión del job y un paso previo que restaura `dotnet-ef` desde el manifiesto versionado y aplica todas las migraciones sobre la base efímera.
  - Corregido `IdentityDbContextFactory` para consumir el contrato real `ConnectionStrings__IdentityDatabase`, eliminando el nombre divergente `IdentityConnection`.
- **Seguridad**: la contraseña visible en el workflow pertenece únicamente al contenedor efímero del runner y no corresponde a ningún ambiente desplegado; no requiere un GitHub Secret.
- **Verificación local**: restauración de `dotnet-ef` 10.0.11 correcta; la factoría de diseño resolvió `ConnectionStrings__IdentityDatabase`; compilación Release con 0 warnings/errores y suite completa en verde (Core 51/51, Infrastructure 97/97; total 148/148).
- **Git**: Cambios preservados en working tree; sin commit, push, merge, tag ni PR.

### 2026-10-23 — Aprovisionamiento privado y usuarios de organización

- Solicitud: crear organizaciones con administrador dueño y permitir altas de supervisores y colaboradores desde frontend admin. El usuario confirmó script privado y equivalencia colaborador–Collector; pidió basarse en el maestro/framework C# y ramas feature desde dev.
- Normativa efectiva: COBROUP 1.11.0, FRAMEWORK_DESARROLLO_CSHARP 2.0 y decisión JWT vigente. Alcance registrado en la decisión de aprovisionamiento del repositorio documental.
- Git: `feature/organizaciones-usuarios` creada desde `dev` (52f39a0, igual a origin/dev al consultar); se conservaron los cambios iniciados en la rama feature anterior. Commit autorizado: NO; sin push, merge, tag ni PR.
- Cambios: puertos Domain, casos de uso Core, repositorios EF y transporte HTTP/CLI; script PowerShell local/Kubernetes; usuario global con teléfono y fecha de actualización; membresía con dueño único filtrado; migración aditiva; auditoría atómica. GET/POST usuarios exige Admin activo en el tenant del JWT y solo crea Supervisor/Collector. El comando privado crea organización/dueño sin servidor HTTP ni secretos en argumentos.
- Verificación: build Release correcto, 0 warnings/errores; Core 66/66; Infrastructure y HTTP/CLI aislados 98/98. Pruebas nuevas de PostgreSQL para atomicidad/duplicados/concurrencia compiladas, no ejecutadas localmente. Se excluyeron AuthLifecycleHttpTests y clases Postgres porque requieren PostgreSQL real; no se modificó la base desplegada.
- Migración: EF no detecta cambios pendientes de modelo; SQL idempotente generado en artifacts/organization-provisioning-migration.sql sin aplicar. Script PowerShell sin errores de sintaxis; smoke del ejecutable real rechazó stdin inválido con exit 1, sin iniciar HTTP ni consultar base.
- Límites: validar migración y pruebas PostgreSQL en la base aislada de CI, y flujo por ingress antes del despliegue. Organizaciones históricas conservan administradores; no se inventan dueños ni teléfonos. Auditoría independiente no realizada; la reapertura de JWT no se cierra con esta intervención.

### 2026-10-23 — Aplicación de migración y autorización de commits

- Solicitud expresa: aplicar migraciones y hacer commit del alcance de organizaciones/usuarios.
- Destino verificado sin exponer credenciales: PostgreSQL `dev_cobroup_identity`, conexión de desarrollo obtenida de User Secrets e inyectada únicamente al proceso EF.
- Resultado: `dotnet ef database update AddOrganizationOwnershipAndUserContact --configuration Release --no-build` aplicó `20261023235308_AddOrganizationOwnershipAndUserContact` correctamente. `dotnet ef migrations list` confirmó las once migraciones aplicadas, sin pendientes.
- La validación PostgreSQL de transacciones/concurrencia y el flujo por ingress siguen pendientes; no se ejecutaron pruebas de creación de usuarios sobre la base de desarrollo ni se crearon cuentas reales.
- Commit autorizado: SÍ, alcance de aprovisionamiento/usuarios, migración y documentación de esta solicitud. Push, merge, tag y PR no solicitados.
