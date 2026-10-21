# Contexto — MS.Cobroup.BackendAdmin

## Propósito

Autoridad administrativa para configuración de organización, rutas, asignaciones, plantillas, medios, etiquetas y aprobación de excepciones. Fase 1 crea únicamente la base ejecutable.

## Límites

- Propio: futuras escrituras administrativas y contratos de consulta administrativa.
- No propio: Customer, Loan, Installment, Payment y PaymentEvidence.
- Consultas operativas de mapas/reportes deberán usar API o proyecciones; nunca escrituras directas en tablas operativas.

## Seguridad inicial

Los contratos modelan `user_id`, `tenant_id`, roles canónicos y rutas. `AdministrativeScopeAuthorizer` requiere simultáneamente tenant coincidente y rol ADMIN para administrar.

## Estado técnico

- .NET 10 / C# 14, solución `.slnx`, sin `src/`.
- Health check: `/health`.
- Persistencia e identidad: pendientes.
- Contratos: [fronteras v1](../contracts/platform-boundaries-v1.md).
- Decisiones: [fundación](../decisions/0001-phase-1-foundation.md).
- Roadmap: [propuesta](../roadmap/phase-proposal.md).
