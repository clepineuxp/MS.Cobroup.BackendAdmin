# Organizaciones y usuarios v1

Decisión autorizada el 2026-10-23: alta privada mediante comando/script; colaborador corresponde al rol existente `Collector`. Referencia normativa: [decisión de producto](../../../cobroup.md/DECISION_APROVISIONAMIENTO_ORGANIZACIONES_USUARIOS.md), maestro COBROUP 1.11.0, framework C# 2.0 y decisión JWT vigente.

## Alta privada

`--provision-organization` ejecuta un caso de uso de Core y termina sin iniciar el servidor HTTP. Recibe por stdin un JSON con `organizationCode`, `organizationName`, `username`, `displayName`, `phoneNumber` y `password`. Requiere la conexión segura `ConnectionStrings:IdentityDatabase`; no depende de claves JWT ni del arranque de autenticación HTTP. No usa ni reactiva el bootstrap inicial de una sola ejecución.

La transacción de EF crea una organización activa, un usuario global activo, su membresía `Admin` con `IsOwner=true` y un registro `organization.provisioned`. El código de organización y el username normalizado son únicos. Un conflicto no deja datos parciales y no modifica cuentas existentes. No se vinculan identidades existentes ni se cambian sus contraseñas mediante este flujo.

El proceso devuelve los IDs de organización y dueño. Códigos de salida: `0` creado, `2` código/usuario ocupado, `1` entrada o ejecución fallida. Nunca devuelve contraseña ni hash. El acceso al proceso/contenedor y a su configuración de base de datos es la frontera de autorización del operador; no existe un endpoint público ni un rol global nuevo.

## Administración del equipo

- `GET /api/v1/users`: lista usuarios y membresías exclusivamente del tenant del token. Incluye ID, username, nombre, teléfono, rol, estado efectivo, indicador de dueño y fecha de incorporación. No devuelve credenciales.
- `POST /api/v1/users`: acepta `{ username, displayName, phoneNumber, password, role }`. Solo admite `Supervisor` o `Collector`, nunca `Admin` ni un valor numérico/desconocido que evada esa lista.
- El cliente no selecciona tenant. API y Core exigen `Admin`; Core consulta además que usuario, organización y membresía administrativa sigan activos. Auditoría `user.created` y alta de usuario/membresía se guardan atómicamente.
- `201`: creado; `400`: datos/rol inválidos; `401`: sin autenticación; `403`: sin autorización; `409`: username no disponible. Los errores nuevos contienen código, mensaje seguro y `traceId`.
- El header opcional `X-Cobroup-Expected-Organization` solo detecta cambios de sesión durante un envío: si no coincide con el tenant del token, se devuelve `409 organization_changed`. Nunca concede acceso ni selecciona tenant.
- Los usuarios creados quedan disponibles en `GET /api/v1/users/assignable`. Se asignan a rutas mediante el flujo existente; no se asignan automáticamente.

## Validación y almacenamiento

Username obligatorio hasta 80 caracteres; nombre obligatorio hasta 160; teléfono obligatorio de 7 a 24 caracteres con al menos 7 dígitos; contraseña obligatoria, sin requisitos de longitud ni complejidad y sin recortar su contenido (se admite `1`). Se conserva el hash resistente de ASP.NET Identity existente. Fechas en UTC.

La migración `AddOrganizationOwnershipAndUserContact` incorpora teléfono nullable para datos históricos, fecha de actualización inicializada desde la creación e indicador de dueño con índice único filtrado. Conserva el índice de consulta por organización. No inventa teléfonos ni adjudica dueños retroactivamente a organizaciones existentes; sus administradores conservan permisos.

## Ejecución

Desde la raíz del repositorio, en Windows PowerShell 5.1 o PowerShell 7:

```powershell
./scripts/provision-organization.ps1 -OrganizationCode NORTE -OrganizationName 'Organización Norte' -Username admin.norte -DisplayName 'Administrador Norte' -PhoneNumber '+573001234567'
```

El modo predeterminado corre desde Windows con `dotnet` local y escribe directamente en PostgreSQL mediante Core/Infrastructure. El script configura `Development` únicamente para el proceso hijo, que lee la conexión de los User Secrets del proyecto API (o la variable de conexión existente). Solicita la contraseña oculta. No necesita clúster, kubectl, API desplegada ni cambiar variables manualmente. No introducir contraseñas en argumentos, archivos JSON ni historial de comandos.

El script comprueba que el ejecutable requerido esté en PATH antes de solicitar la contraseña. Si no tienes `kubectl` ni contexto Kubernetes en este equipo, puedes ejecutar el caso de uso desde el proyecto local contra la base definida en sus User Secrets:

```powershell
./scripts/provision-organization.ps1 -OrganizationCode NORTE -OrganizationName 'Organización Norte' -Username admin.norte -DisplayName 'Administrador Norte' -PhoneNumber '+573001234567'
```

Este modo utiliza `dotnet` local y la base configurada, no hace exec en Kubernetes ni necesita un endpoint HTTP. Verifica el ambiente de la conexión antes de ejecutar; no se aplica un cambio silencioso de modo si falta un ejecutable.

El parámetro opcional `-Namespace cobroup-dev` se conserva para operadores con acceso al clúster y habilita explícitamente `kubectl exec -i`; no debe usarse para el alta directa desde Windows sin acceso Kubernetes.

Aplicar la migración antes del rollout de la nueva API mediante el procedimiento de despliegue del ambiente. Se puede generar SQL idempotente con:

```powershell
dotnet ef migrations script AddSigningKeyIdToRefreshTokens AddOrganizationOwnershipAndUserContact --idempotent --project MS.Cobroup.BackendAdmin.Infrastructure --startup-project MS.Cobroup.BackendAdmin.Api --output organization-provisioning-migration.sql
```

Rollback de aplicación: retirar la nueva UI/API y conservar las columnas adicionales, compatibles con la versión anterior. No ejecutar `Down` después de crear datos nuevos sin un plan de recuperación: eliminaría los teléfonos y la marca de propiedad.

## Verificación local y límites

Core e integración HTTP aislada cubren roles, tenant del token, membresía inactiva, entradas inválidas, duplicados y ausencia de credenciales en respuestas; pruebas de CLI verifican stdin y salida segura. El modelo valida el índice único filtrado y el SQL generado. La migración y las transacciones contra PostgreSQL real, así como el flujo por ingress, requieren validación en un ambiente de pruebas antes de publicación. No se aplicaron migraciones ni se crearon organizaciones en bases desplegadas durante esta implementación.

## Aplicación verificada — 2026-10-23

Por instrucción expresa del propietario se aplicó `20261023235308_AddOrganizationOwnershipAndUserContact` en la base configurada de desarrollo `dev_cobroup_identity`. EF finalizó correctamente y la consulta posterior del historial no mostró migraciones pendientes. No se crearon organizaciones ni usuarios reales. Esta aplicación confirma el cambio de esquema en Dev; las pruebas PostgreSQL de atomicidad/concurrencia y el flujo por ingress continúan pendientes.
