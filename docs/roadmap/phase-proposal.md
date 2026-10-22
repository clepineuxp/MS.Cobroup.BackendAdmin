# Roadmap vigente de revalidación

Actualizado: 2026-10-22.

1. **Fase 2 — reabierta y bloqueante:** implementar la autoridad JWT asimétrica, rotación de refresh por familia, publicación JWKS, revocación y controles definidos en `../../../cobroup.md/DECISION_AUTENTICACION_JWT.md`.
2. **Fases 3 a 9 — cierre histórico conservado:** su funcionalidad versionada no se elimina, pero toda evidencia dependiente de identidad debe repetirse con JWT antes de aprobar producción.
3. **Cierre de identidad:** exige las pruebas mínimas del contrato, migración coherente de datos y clientes, material criptográfico externo por ambiente y ausencia de componentes heredados activos.

La planeación consolidada mantiene 8 fases cerradas y 1 reabierta. Este documento no aprueba la implementación actual ni autoriza despliegue. Ver `../../../cobroup.md/informe/avance.md`.
