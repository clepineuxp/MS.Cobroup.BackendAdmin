using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MS.Cobroup.BackendAdmin.Api.Security;

/// <summary>
/// Fábrica centralizada para la resolución y validación del proveedor de firma JWT (IJwtSigningKeyProvider).
/// Implementa fail-fast estricto para evitar fallbacks silenciosos e impide el uso no autorizado
/// de claves efímeras fuera de entornos de desarrollo.
/// </summary>
public static class JwtSigningKeyProviderFactory
{
    public const string ProviderEphemeralRsa = "EphemeralRsa";
    public const string ProviderPersistentRsa = "PersistentRsa";
    public const string ProviderAwsKms = "AwsKms";

    public static IJwtSigningKeyProvider Create(
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger? logger = null)
    {
        var provider = configuration["Jwt:SigningProvider"];

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new InvalidOperationException(
                "La configuración 'Jwt:SigningProvider' es obligatoria. " +
                "Valores admitidos: 'EphemeralRsa' (desarrollo/pruebas) o proveedores persistentes de backlog ('PersistentRsa', 'AwsKms').");
        }

        if (string.Equals(provider, ProviderEphemeralRsa, StringComparison.OrdinalIgnoreCase))
        {
            var allowEphemeral = configuration.GetValue<bool>("Jwt:AllowEphemeralSigning");

            if (!environment.IsDevelopment())
            {
                var acknowledgeRiskOutsideDev = configuration.GetValue<bool>("Jwt:AcknowledgeNonProductionEphemeralRiskOutsideDevelopment");
                if (!acknowledgeRiskOutsideDev)
                {
                    throw new InvalidOperationException(
                        $"El proveedor '{ProviderEphemeralRsa}' está estrictamente rechazado fuera de Development (entorno actual: '{environment.EnvironmentName}'). " +
                        "La emisión de tokens en este ambiente requiere un proveedor persistente (ver backlog de seguridad productiva).");
                }

                logger?.LogCritical(
                    "ALERTA DE SEGURIDAD CRÍTICA: Se ha habilitado 'EphemeralRsa' fuera de Development bajo el flag explícito " +
                    "'Jwt:AcknowledgeNonProductionEphemeralRiskOutsideDevelopment=true'. Este entorno no garantiza persistencia de sesiones ni alta disponibilidad.");
            }
            else
            {
                if (!allowEphemeral)
                {
                    throw new InvalidOperationException(
                        $"El proveedor '{ProviderEphemeralRsa}' requiere 'Jwt:AllowEphemeralSigning=true' explícito en entorno de desarrollo. " +
                        "No se permite fallback silencioso sin confirmación explícita.");
                }
            }

            return new EphemeralRsaSigningKeyProvider();
        }

        if (string.Equals(provider, ProviderPersistentRsa, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(provider, ProviderAwsKms, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"El proveedor persistente de firma JWT '{provider}' no está implementado en la fase actual. " +
                "Queda registrado como backlog obligatorio previo a producción (ADR-0005: Proveedor persistente de claves RSA).");
        }

        throw new InvalidOperationException(
            $"Proveedor de firma JWT desconocido: '{provider}'. Valores admitidos: '{ProviderEphemeralRsa}'.");
    }
}
