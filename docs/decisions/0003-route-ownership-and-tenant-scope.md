# ADR-0003 — Rutas administrativas y alcance tenant

## Estado

Aceptada y completada para fase 3 (2026-10-21).

## Decisión

- `MS.Cobroup.BackendAdmin` es autoridad de configuración, rutas y asignaciones.
- Solo un usuario `Admin` autenticado puede crear rutas.
- La organización se obtiene del claim `organization_id`; el contrato de escritura no acepta un tenant enviado por el cliente.
- Una ruta se crea con un supervisor activo y al menos un cobrador activo, todos pertenecientes a la misma organización y con el rol esperado.
- El nombre normalizado es único dentro de la organización, no globalmente.
- La ruta, sus asignaciones y el evento `route.created` se guardan en la misma transacción.

## Alcance completado

Incluye alta, consulta, edición, desactivación, reasignación, configuración organizacional y cupos de clientes/préstamos con modos `Inherited` y `Overridden`. Las reasignaciones y cambios de estado/cupo conservan antes/después en auditoría. El contrato operativo expone únicamente rutas activas: todas para `Admin`, supervisadas para `Supervisor` y asignadas para `Collector`.

La selección/orden de clientes y la asistencia geográfica no forman parte de esta fase porque requieren primero las entidades Customer/Location y la decisión del proveedor de mapas. Se mantienen en sus fases dependientes; no son una configuración de ruta implementable de forma íntegra sin esos datos.

## Consecuencias

- AC-03 comienza con defensa tanto en la política HTTP como en Core.
- No se crean rutas de ejemplo en el bootstrap: la organización y los usuarios base permanecen separados de los datos operativos.
- Los contratos administrativos y operativos quedaron integrados con ambos frontend.
