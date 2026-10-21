# ADR-0004 — Usuarios globales, membresías y controladores API

## Estado

Aceptada durante la pausa de refinamiento de fase 3 (2026-10-21).

## Decisiones

1. `ApplicationUser` representa una identidad global y su nombre de usuario normalizado es único en la plataforma.
2. La relación con una organización se representa mediante `OrganizationMembership`, que contiene el rol, estado, fecha de ingreso y `LastAccessedAtUtc`.
3. El login recibe exclusivamente usuario y contraseña. Si existen membresías activas, selecciona la de acceso más reciente; si nunca se accedió, usa la membresía incorporada más recientemente como desempate inicial.
4. Un usuario sin membresías activas obtiene el estado `membership_required` y no recibe tokens tenant. La interfaz muestra “Solicitar unirme a una organización”; el flujo de solicitud/aprobación se definirá antes de implementarlo.
5. El usuario puede consultar sus membresías activas y seleccionar otra. La selección actualiza `LastAccessedAtUtc`, reemplaza la sesión de autoridad y obliga al frontend a revocar el refresh token anterior y obtener tokens nuevos.
6. El tenant nunca se acepta en el login ni se toma de un parámetro de negocio: proviene de la membresía seleccionada y queda fijado en los claims del token.
7. Cada responsabilidad HTTP tiene un controlador en el proyecto API. `Program.cs` se limita a composición, middleware y mapeo de controladores.

## Controladores actuales

- `AuthController`: antiforgery, login, logout y contexto autenticado.
- `OrganizationSessionsController`: listado y cambio de organización activa.
- `OidcController`: autorización, cierre OIDC y userinfo.
- `BootstrapController`: bootstrap único de plataforma.
- `RoutesController`: configuración de rutas.
- `HealthController`: estado técnico.

Las responsabilidades futuras usarán controladores propios (`UsersController`, `CollectionsController`, etc.) y no agregarán handlers de negocio directamente en `Program.cs`.

## Migración

`GlobalUsersAndMemberships` copia cada relación existente de `users` a `organization_memberships` antes de retirar `OrganizationId` y `Role` del usuario. Los tres usuarios bootstrap y sus roles se conservan.
