# ADR-0005 — Configuración organizacional y cupos heredables

## Estado

Aceptada para cierre de fase 3 (2026-10-21).

## Decisión

- `OrganizationSettings` almacena zona horaria IANA, moneda ISO y límites generales opcionales de clientes/préstamos activos por ruta.
- Un límite `null` significa que la organización todavía no impone ese cupo; no se inventa un número durante bootstrap o migración.
- Cada ruta usa `Inherited` o `Overridden` de forma independiente para clientes y préstamos. `Inherited` resuelve siempre el valor organizacional vigente; `Overridden` exige un entero positivo.
- Cambiar configuración no reescribe préstamos ni clientes; las fases propietarias validarán los valores efectivos al crear, activar o trasladar recursos.
- Toda actualización registra actor, tenant, fecha y detalle JSON auditable.

## Consecuencias

La configuración puede completarse sin elegir arbitrariamente cupos para organizaciones existentes. Las rutas heredadas reflejan cambios futuros; las sobrescritas permanecen estables hasta una acción administrativa explícita.
