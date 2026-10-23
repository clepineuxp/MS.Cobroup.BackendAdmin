# Roadmap vigente de revalidación

Actualizado: 2026-10-22.

1. **Fase 2 — reabierta y bloqueante:** migrar la autoridad JWT a firma exclusiva `HS256`, secreto distinto por ambiente, rotación de refresh por familia, revocación y controles definidos en `../../../cobroup.md/DECISION_AUTENTICACION_JWT.md`; retirar RSA/JWKS/acknowledge.
2. **Fases 3 a 9 — cierre histórico conservado:** su funcionalidad versionada no se elimina, pero toda evidencia dependiente de identidad debe repetirse con JWT antes de aprobar producción.
3. **Cierre de identidad:** exige las pruebas mínimas del contrato, migración coherente de datos y clientes, secreto CSPRNG externo e independiente por ambiente, rotación coordinada y ausencia de componentes heredados activos.

La planeación consolidada mantiene 8 fases cerradas y 1 reabierta. Este documento no aprueba la implementación actual ni autoriza despliegue. Ver `../../../cobroup.md/informe/avance.md`.
