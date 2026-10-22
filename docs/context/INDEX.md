# Contexto — MS.Cobroup.BackendAdmin

## Propósito

Autoridad administrativa para configuración de organización, rutas, asignaciones, plantillas, medios, etiquetas y aprobación de excepciones. Fase 3 completa configuración organizacional y rutas; las capacidades posteriores permanecen fuera de alcance.

## Límites

- Propio: configuración organizacional, rutas, cupos heredables, asignaciones y sus contratos de consulta/auditoría.
- No propio: Customer, Loan, Installment, Payment y PaymentEvidence.
- Consultas operativas de mapas/reportes deberán usar API o proyecciones; nunca escrituras directas en tablas operativas.

## Seguridad inicial

Los contratos modelan `user_id`, `tenant_id`, roles canónicos y rutas. `AdministrativeScopeAuthorizer` requiere simultáneamente tenant coincidente y rol ADMIN para administrar.

## Estado técnico

- .NET 10 / C# 14, solución `.slnx`, sin `src/`.
- Health check: `/health`.
- Persistencia PostgreSQL y configuración/rutas: implementadas hasta fase 3. La identidad JWT está reabierta y pendiente de conformidad con la decisión del 2026-10-22.
- Contratos: [fronteras v1](../contracts/platform-boundaries-v1.md).
- Decisiones: [fundación](../decisions/0001-phase-1-foundation.md) y [autoridad JWT](../decisions/0002-central-identity-authority.md).
- Roadmap: [propuesta](../roadmap/phase-proposal.md).
