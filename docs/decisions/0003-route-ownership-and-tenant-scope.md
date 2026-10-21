# ADR-0003 — Rutas administrativas y alcance tenant

## Estado

Aceptada para el corte inicial de fase 3 (2026-10-21).

## Decisión

- `MS.Cobroup.BackendAdmin` es autoridad de configuración, rutas y asignaciones.
- Solo un usuario `Admin` autenticado puede crear rutas.
- La organización se obtiene del claim `organization_id`; el contrato de escritura no acepta un tenant enviado por el cliente.
- Una ruta se crea con un supervisor activo y al menos un cobrador activo, todos pertenecientes a la misma organización y con el rol esperado.
- El nombre normalizado es único dentro de la organización, no globalmente.
- La ruta, sus asignaciones y el evento `route.created` se guardan en la misma transacción.

## Alcance de este corte

Incluye el alta segura de una ruta, su persistencia, migración y pruebas de autorización/aislamiento. Edición, desactivación, reasignación, consulta, cupos, herencia de parámetros y georreferenciación quedan para cortes posteriores de la misma fase.

## Consecuencias

- AC-03 comienza con defensa tanto en la política HTTP como en Core.
- No se crean rutas de ejemplo en el bootstrap: la organización y los usuarios base permanecen separados de los datos operativos.
- Los contratos de consulta se definirán antes de integrar el frontend administrativo.
