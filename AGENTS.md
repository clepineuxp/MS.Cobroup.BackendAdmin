# AGENTS — MS.Cobroup.BackendAdmin

## Identidad y restricciones del proyecto

- Producto: COBROUP
- Rol: microservicio administrativo por tenant.
- Documento maestro: `../cobroup.md/COBROUP.md` 1.7.0 y `../cobroup.md/DECISION_AUTENTICACION_JWT.md`.
- Estándar: `../cobroup.md/FRAMEWORK_DESARROLLO_CSHARP.md` 2.0.
- Rama activa observada: `feature/solucion-hallazgos-iniciales`.
- Prohibidos push, merge, tag y PR sin autorización.

## Estado actual

- Fase: 9
- Resultado: IDENTIDAD_JWT_REABIERTA_PARA_ADECUACION_Y_AUDITORIA
- Plataforma: .NET SDK 10.0.401, `net10.0`, C# 14.
- Auditoría independiente: realizada el 2026-10-22; resultado técnico NO APROBADO.
- Línea base funcional versionada; autenticación JWT exclusiva pendiente de conformidad. Auditoría técnica: `../cobroup.md/informe/auditoria-backend-csharp-2026-10-22-v2.html`; adenda vigente: `../cobroup.md/informe/linea-base-auditoria-jwt-2026-10-22.html`.
- Corte actual: el árbol contiene cambios JWT y migraciones sin commit. No se autoriza cierre, commit ni despliegue por el solo hecho de compilar.

## Decisión vigente de autenticación

- `MS.Cobroup.BackendAdmin` es la única autoridad para login, emisión, refresh, revocación y publicación JWKS bajo `../cobroup.md/DECISION_AUTENTICACION_JWT.md`.
- Se usarán exclusivamente access y refresh tokens JWT con firma asimétrica `RS256` o `ES256`; no se mantendrá un mecanismo de identidad paralelo.
- La clave privada es externa, exclusiva de este servicio, rotada por `kid` y obligatoria fuera de Development. No hay secretos/defaults de respaldo.
- Access JWT: máximo 15 minutos, audience `cobroup-api`, validación estricta y entrega al frontend solo para memoria.
- Refresh JWT: máximo 30 días, audience `cobroup-refresh`, cookie HttpOnly/Secure/SameSite, hash server-side, uso único, rotación atómica, familia y detección de replay.
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

