# ADR-006 — Proveedor RSA efímero en memoria y desacoplamiento de firma JWT

**Estado:** SUPERSEDIDO el 2026-10-22 por ADR-0007. No implementar; contenido conservado solo como trazabilidad histórica.  
**Bloqueante de producción:** Sí, hasta la implementación del proveedor persistente.

---

## 1. Contexto

La arquitectura de seguridad de COBROUP establece que `MS.Cobroup.BackendAdmin` es la única autoridad de identidad y emisor de tokens JWT (access y refresh) firmados asimétricamente con `RS256`, mientras que el backend operativo (`MS.Cobroup.Backend`) valida dichos tokens exclusivamente a través del endpoint público JWKS (`/api/v1/auth/.well-known/jwks.json`).

En la fase actual de desarrollo y validación, la gestión y distribución de una clave privada RSA externa en formato PEM a través de pipelines de CI/CD, secretos de repositorio y volúmenes de Kubernetes introdujo fricción operativa innecesaria y defectos de sustitución multilínea en manifiestos (hallazgo B-13).

---

## 2. Decisión obligatoria

Se incorpora formalmente la siguiente decisión arquitectónica:

> COBROUP continuará usando JWT firmados con RS256. En la fase actual no se configurará un proveedor persistente ni una clave privada externa en formato PEM. BackendAdmin utilizará un proveedor RSA efímero generado en memoria. Development requiere autorización local explícita; Staging, QA y otros ambientes no productivos pueden usarlo temporalmente con `Jwt:AcknowledgeNonProductionEphemeralRiskOutsideDevelopment=true`, una sola réplica y despliegue sin solapamiento. Producción continúa prohibida hasta implementar el proveedor persistente del backlog.

---

## 3. Diseño técnico e implementación

### 3.1. Abstracción del puerto criptográfico (`IJwtSigningKeyProvider`)
Se introduce la interfaz `IJwtSigningKeyProvider` para desacoplar el pipeline de autenticación y los controladores HTTP de cualquier conocimiento sobre archivos, rutas físicas, PEM o infraestructura de Kubernetes:

```csharp
public interface IJwtSigningKeyProvider : IDisposable
{
    string ActiveKeyId { get; }
    SigningCredentials GetSigningCredentials();
    IReadOnlyCollection<SecurityKey> GetValidationKeys();
    TokenValidationParameters CreateTokenValidationParameters(string validIssuer, string validAudience);
    JsonWebKeySet GetPublicJwks();
}
```

### 3.2. Adaptador efímero (`EphemeralRsaSigningKeyProvider`)
- **Generación criptográfica segura:** Crea una clave RSA de 2048 bits en memoria utilizando `RSA.Create(2048)`.
- **Confinamiento de memoria:** La clave privada reside únicamente en la memoria de la instancia y nunca se escribe ni exporta a archivos, logs, base de datos ni variables de entorno.
- **Identificador de clave unívoco:** Genera un `kid` no ambiguo por ciclo de vida (`cobroup-ephemeral-{Guid}`), evitando reutilizar identificadores fijos con distinto material criptográfico.
- **Publicación JWKS estrictamente pública:** Publica únicamente los parámetros `n` (módulo) y `e` (exponente) en formato Base64Url. Los parámetros privados (`d`, `p`, `q`, `dp`, `dq`, `qi`) son omitidos y permanecen nulos.
- **Ciclo de vida Singleton:** Se registra como singleton en el contenedor de inyección de dependencias para garantizar consistencia entre emisión de tokens, validación local y JWKS.
- **Liberación de recursos:** Implementa `IDisposable` para liberar determinísticamente el objeto `RSA` y la `RsaSecurityKey` subyacente.

### 3.3. Configuración explícita y fail-fast (`JwtSigningKeyProviderFactory`)
El arranque del servicio es gobernado por la fábrica con políticas estrictas:
- Requiere `Jwt:SigningProvider=EphemeralRsa`.
- En `Development` exige `Jwt:AllowEphemeralSigning=true`. Si el flag falta o es `false`, la aplicación aborta inmediatamente el inicio (`InvalidOperationException`).
- En `Staging`, `QA` u otro ambiente no productivo, el modo `EphemeralRsa` requiere `Jwt:AcknowledgeNonProductionEphemeralRiskOutsideDevelopment=true`; sin esa aceptación explícita, el arranque debe fallar.
- En `Production`, el modo `EphemeralRsa` debe rechazarse incluso si se configura el acknowledge.
- El acknowledge no autoriza `alg=none`, tokens sin firma, HMAC compartido, múltiples réplicas ni rollouts con pods emisores solapados.

---

## 4. Limitaciones operativas reconocidas

1. **Reinicio de proceso:** Al reiniciar `BackendAdmin`, se genera un nuevo par de claves RSA y un nuevo `kid`. Los tokens emitidos antes del reinicio pueden ser rechazados inmediatamente por el backend operativo.
2. **Alta disponibilidad / Múltiples réplicas:** No es compatible con despliegues multi-réplica detrás de un balanceador de carga, dado que cada réplica mantendría claves y `kid`s divergentes.
3. **Inadecuación productiva:** Este modo es apto para desarrollo y validación funcional no productiva con riesgo explícitamente aceptado; no es apto para producción ni para certificar alta disponibilidad.
4. **Rollout y coexistencia de emisores efímeros (B-19):** Si se utiliza una estrategia `RollingUpdate` con `maxSurge > 0`, coexisten temporalmente el pod anterior y el nuevo detrás del mismo Service Kubernetes. Dado que cada pod efímero genera claves y `kid`s independientes en memoria, las solicitudes de login, refresh y validación sufren intermitencia. Para mitigar esto, el despliegue en desarrollo exige `strategy: type: Recreate` (validado por el pipeline CI/CD), impidiendo la superposición real de emisores. La breve indisponibilidad de segundos durante el reinicio del pod queda aceptada y documentada para este entorno.

---

## 5. Backlog de producción

La transición hacia QA con características productivas, alta disponibilidad o Producción requiere la implementación del proveedor `PersistentRsa` conforme a los 10 requisitos establecidos en `cobroup.md/BACKLOG_SEGURIDAD_PRODUCTIVA.md`. El acknowledge únicamente habilita validación funcional temporal y no cierra esta condición bloqueante.
