# AGENTS — MS.Cobroup.BackendAdmin

## Identidad y restricciones del proyecto

- Producto: COBROUP
- Rol: microservicio administrativo por tenant.
- Documento maestro: `../cobroup.md/COBROUP.md` 1.0.1.
- Estándar: `../cobroup.md/FRAMEWORK_DESARROLLO_CSHARP.md` 2.0.
- Rama requerida y activa: `feature/fase-2-identidad-tenant`.
- Prohibidos push, merge, tag y PR sin autorización.

## Estado actual

- Fase: 2
- Resultado: COMPLETADA
- Plataforma: .NET SDK 10.0.401, `net10.0`, C# 14.
- Auditoría independiente: no realizada.

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
