# Contexto — MS.Cobroup.BackendAdmin

## Propósito

Autoridad administrativa para configuración de organización, rutas, asignaciones, plantillas, medios, etiquetas y aprobación de excepciones. Fase 3 completa configuración organizacional y rutas; las capacidades posteriores permanecen fuera de alcance.

## Límites

- Usuarios: `PUT /api/v1/users/{id}` y auditoría/revocación atómica según [decisión de edición](../../../cobroup.md/DECISION_UBICACION_CLIENTES_EDICION_USUARIOS.md). Contrato detallado en [organizaciones y usuarios v1](../contracts/organization-provisioning-users-v1.md).

- Organizaciones y equipo: [aprovisionamiento privado y usuarios v1](../contracts/organization-provisioning-users-v1.md). Alta de organización con dueño por CLI; listado y creación tenant-safe de supervisores y colaboradores desde el panel.

- Propio: configuración organizacional, rutas, cupos heredables, asignaciones y sus contratos de consulta/auditoría.
- No propio: Customer, Loan, Installment, Payment y PaymentEvidence.
- Consultas operativas de mapas/reportes deberán usar API o proyecciones; nunca escrituras directas en tablas operativas.

## Seguridad inicial

Los contratos modelan `user_id`, `tenant_id`, roles canónicos y rutas. `AdministrativeScopeAuthorizer` requiere simultáneamente tenant coincidente y rol ADMIN para administrar.

## Estado técnico

- .NET 10 / C# 14, solución `.slnx`, sin `src/`.
- Health check: `/health`.
- Persistencia PostgreSQL y configuración/rutas: implementadas hasta fase 3. La identidad JWT debe migrar a `HS256` con secreto independiente por ambiente (ADR-0007); la implementación aún requiere reauditoría.
- Contratos: [fronteras v1](../contracts/platform-boundaries-v1.md).
- Decisiones: [fundación](../decisions/0001-phase-1-foundation.md), [autoridad JWT histórica](../decisions/0002-central-identity-authority.md), [RSA efímero histórico](../decisions/0006-ephemeral-rsa-signing-provider.md) y [firma HS256 vigente](../decisions/0007-symmetric-hs256-per-environment.md).
- Roadmap: [propuesta](../roadmap/phase-proposal.md) y [backlog de firma simétrica](../../../cobroup.md/BACKLOG_SEGURIDAD_FIRMA_SIMETRICA.md).

## Plantillas por rangos y cobro mensual — 2026-10-23

Decisión vigente: [rangos, rutas y mensual](../../../cobroup.md/DECISION_PLANTILLAS_RANGOS_RUTAS.md). Monto/porcentaje mínimo y máximo inclusivos; plantillas publicadas asignadas explícitamente a la ruta del cliente, con validación del servidor. Mensual conserva el día de inicio por meses calendario. Migración administrativa preparada y pendiente de aplicación; no modifica condiciones de préstamos existentes.
