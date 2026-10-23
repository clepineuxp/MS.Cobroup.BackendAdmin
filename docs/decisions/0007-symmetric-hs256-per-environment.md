# ADR-007 — Firma JWT simétrica HS256 por ambiente

**Estado:** Aceptado el 2026-10-22; implementación pendiente de adecuación y reauditoría.  
**Reemplaza:** ADR-0006 y las reglas asimétricas de ADR-0002.

## Decisión

BackendAdmin firma access y refresh JWT exclusivamente con `HS256`. Dev, QA y Prod usan secretos CSPRNG diferentes, Base64, de 32 bytes o más. El secreto del ambiente se administra desde `cobroup.environments` y se inyecta como `Jwt__SigningKeyBase64` en BackendAdmin y Backend. BackendAdmin es el único emisor lógico; Backend solo valida.

Se retiran RSA, PEM/PFX, JWKS, proveedores efímeros/persistentes, banderas de acknowledge y cualquier fallback. Los valores reales nunca se versionan ni se exponen en ConfigMap, imagen, frontend, log o artefacto.

## Controles obligatorios

- Fallo inmediato por ausencia, Base64 inválido o menos de 32 bytes.
- Allowlist exacta `HS256`; rechazo de `none`, RSA y otros HMAC.
- `kid` como versión no secreta; clave activa para emisión y anteriores solo para validación durante una ventana acotada.
- Rotación coordinada en ambos backends y todas sus réplicas, con rollback y revocación de emergencia probados.
- Secreto distinto entre ambientes y acceso Kubernetes restringido a las dos cargas autorizadas.
- Permanecen intactos issuer/audiences, claims, access máximo 15 minutos, cookie segura, CSRF, hash server-side, replay y aislamiento tenant.

## Consecuencia de seguridad aceptada

Ambos backends conocen un secreto capaz de producir firmas válidas. El compromiso del backend operativo puede permitir falsificación de tokens. El propietario acepta ese riesgo bajo mínimo privilegio, aislamiento, rotación, observabilidad y respuesta a incidentes. Esta consecuencia no autoriza que Backend implemente emisión.

La especificación completa y auditable está en `../../../cobroup.md/DECISION_AUTENTICACION_JWT.md` y su backlog en `../../../cobroup.md/BACKLOG_SEGURIDAD_FIRMA_SIMETRICA.md`.
