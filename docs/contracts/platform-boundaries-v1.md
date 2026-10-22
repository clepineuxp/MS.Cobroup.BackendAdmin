# Contratos preliminares v1

| Área | Propietario lógico | Escritura permitida desde este servicio |
|---|---|---|
| OrganizationSettings, Route, Assignment | Backend admin | Sí, en fases autorizadas |
| LoanTemplate, PaymentMethod, Tag | Backend admin | Sí, en fases autorizadas |
| LoanExceptionApproval | Backend admin | Sí; notificación/consulta versionada al operativo |
| Customer, Loan, Installment, Payment, Evidence | Backend operativo | No |
| Mapas, mora, recaudo | Backend operativo o proyección contractual | Solo lectura |

BackendAdmin es la única autoridad JWT y el único custodio de la clave privada asimétrica. Los access tokens usan audiencia `cobroup-api` y claims mínimos `typ=access`, `sub`, `iss`, `aud`, `exp`, `iat`, `nbf`, `jti`, `organization_id` y rol; las claves públicas se publican con `kid`. Los refresh tokens usan tipo y audiencia separados, cookie segura, hash server-side, rotación de un uso y revocación familiar. Errores: código estable, mensaje seguro, `traceId`, sin secretos ni tokens. Lecturas entre servicios son versionadas y tolerantes a reintentos; no se comparte código interno ni se escriben tablas ajenas. Ver `../../../cobroup.md/DECISION_AUTENTICACION_JWT.md`.

## Contrato de rutas v1

- Administración: `GET|POST /api/v1/routes`, `PUT /api/v1/routes/{id}`, `GET /api/v1/users/assignable` y `GET|PUT /api/v1/organization-settings`; solo `Admin` del tenant del token.
- Operación: `GET /api/v1/route-access`; devuelve únicamente rutas activas autorizadas para el actor y es consumido por `MS.Cobroup.Backend`.
- Los cupos efectivos son anulables: `null` significa ausencia de límite configurado, no cero.

## Fachada administrativa de clientes v1

`GET|POST /api/v1/customers` y `GET|PUT /api/v1/customers/{id}` exigen `Admin`, reenvían bearer y payload al backend operativo y conservan su estado HTTP. BackendAdmin no persiste ni modifica directamente `Customer`, teléfonos o ubicaciones.
