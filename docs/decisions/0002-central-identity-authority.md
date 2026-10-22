# ADR-002 — Autoridad central de identidad por organización

Estado: revisada y aceptada el 2026-10-22. Implementación pendiente de conformidad.

`MS.Cobroup.BackendAdmin` es la única autoridad de organizaciones, usuarios y sesiones. La combinación `(organization_id, normalized_username)` es única en PostgreSQL. Las contraseñas usan `PasswordHasher` de ASP.NET Core y el bootstrap es transaccional, de un solo uso y auditable.

`MS.Cobroup.BackendAdmin` es el único emisor. Emite access y refresh tokens JWT con firma asimétrica `RS256` o `ES256`; únicamente este servicio custodia la clave privada y publica las claves públicas, con `kid`, en `/api/v1/auth/.well-known/jwks.json`. La aplicación falla al iniciar si falta una clave válida y no admite HMAC, secretos compartidos ni valores de respaldo.

Los access tokens duran entre 5 y 15 minutos, usan audiencia `cobroup-api`, `typ=access` y los claims obligatorios definidos en `cobroup.md/DECISION_AUTENTICACION_JWT.md`. Permanecen solo en memoria de las SPA. Los refresh tokens duran como máximo 30 días, usan audiencia separada y `typ=refresh`, se entregan únicamente mediante cookie `HttpOnly`, `Secure` y `SameSite`, y se persisten solo como hash. Cada uso los rota de forma atómica; una reutilización revoca toda la familia.

Login, refresh, logout y cambio de organización revalidan usuario, membresía y organización activos. El cambio de organización revoca la familia previa antes de emitir tokens con el nuevo tenant. Los endpoints basados en cookie aplican protección CSRF; las operaciones de identidad aplican rate limiting, telemetría y auditoría sin datos sensibles. La rotación de claves mantiene una ventana pública controlada para validar tokens no expirados y elimina claves retiradas al finalizarla.
