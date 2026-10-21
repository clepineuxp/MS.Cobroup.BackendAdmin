# Contratos preliminares v1

| Área | Propietario lógico | Escritura permitida desde este servicio |
|---|---|---|
| OrganizationSettings, Route, Assignment | Backend admin | Sí, en fases autorizadas |
| LoanTemplate, PaymentMethod, Tag | Backend admin | Sí, en fases autorizadas |
| LoanExceptionApproval | Backend admin | Sí; notificación/consulta versionada al operativo |
| Customer, Loan, Installment, Payment, Evidence | Backend operativo | No |
| Mapas, mora, recaudo | Backend operativo o proyección contractual | Solo lectura |

La identidad deberá tener una sola autoridad lógica. Claims mínimos: `sub`, `tenant_id`, roles y vigencia. Errores: código estable, mensaje seguro, `traceId`, sin secretos. Lecturas entre servicios serán versionadas y tolerantes a reintentos; no se comparte código interno ni se escriben tablas ajenas.
