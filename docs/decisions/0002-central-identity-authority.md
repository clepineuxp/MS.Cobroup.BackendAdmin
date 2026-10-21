# ADR-002 — Autoridad central de identidad por organización

Estado: aceptada para fase 2.

`MS.Cobroup.BackendAdmin` es la única autoridad de organizaciones, usuarios y sesiones. La combinación `(organization_id, normalized_username)` es única en PostgreSQL. Las contraseñas usan `PasswordHasher` de ASP.NET Core y el bootstrap es transaccional, de un solo uso y auditable.

Los clientes públicos usan OpenID Connect Authorization Code + PKCE. Los access tokens JWT duran cinco minutos, los refresh tokens treinta días y se rotan mediante el token endpoint; el logout revoca el refresh token. Las SPA mantienen tokens solo en memoria, nunca en almacenamiento local. Los valores de PKCE de un solo uso sí se conservan temporalmente en `sessionStorage` durante la redirección.

En desarrollo se permiten certificados efímeros. Producción debe inyectar certificados X.509 separados y secretos mediante configuración externa. Las rutas `/identity` y `/api` se resuelven por proxy/gateway para evitar CORS y compartir un origen controlado.
