# Contratos preliminares v1

| Área | Propietario lógico | Escritura permitida desde este servicio |
|---|---|---|
| OrganizationSettings, Route, Assignment | Backend admin | Sí, en fases autorizadas |
| LoanTemplate, PaymentMethod, Tag | Backend admin | Sí, en fases autorizadas |
| LoanExceptionApproval | Backend admin | Sí; notificación/consulta versionada al operativo |
| Customer, Loan, Installment, Payment, Evidence | Backend operativo | No |
| Mapas, mora, recaudo | Backend operativo o proyección contractual | Solo lectura |

La identidad deberá tener una sola autoridad lógica. Claims mínimos: `sub`, `tenant_id`, roles y vigencia. Errores: código estable, mensaje seguro, `traceId`, sin secretos. Lecturas entre servicios serán versionadas y tolerantes a reintentos; no se comparte código interno ni se escriben tablas ajenas.

## Contrato de rutas v1

- Administración: `GET|POST /api/v1/routes`, `PUT /api/v1/routes/{id}`, `GET /api/v1/users/assignable` y `GET|PUT /api/v1/organization-settings`; solo `Admin` del tenant del token.
- Operación: `GET /api/v1/route-access`; devuelve únicamente rutas activas autorizadas para el actor y es consumido por `MS.Cobroup.Backend`.
- Los cupos efectivos son anulables: `null` significa ausencia de límite configurado, no cero.

## Fachada administrativa de clientes v1

`GET|POST /api/v1/customers` y `GET|PUT /api/v1/customers/{id}` exigen `Admin`, reenvían bearer y payload al backend operativo y conservan su estado HTTP. BackendAdmin no persiste ni modifica directamente `Customer`, teléfonos o ubicaciones.
