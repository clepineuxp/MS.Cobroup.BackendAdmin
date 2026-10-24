using System.Text.Json;
using MS.Cobroup.BackendAdmin.Core.Identity;

namespace MS.Cobroup.BackendAdmin.Api.Provisioning;

public static class OrganizationProvisioningCli
{
    public static async Task<int> RunAsync(IServiceProvider services, TextReader input, TextWriter output,
        TextWriter error, CancellationToken cancellationToken = default)
    {
        try
        {
            // Secrets arrive through stdin, never command arguments, files, or application logs.
            var json = await input.ReadToEndAsync(cancellationToken);
            var command = JsonSerializer.Deserialize<ProvisionOrganizationCommand>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new JsonException();
            using var scope = services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ProvisionOrganizationService>();
            var result = await service.ExecuteAsync(command, cancellationToken);
            if (!result.Created)
            {
                await error.WriteLineAsync("No se creó la organización: el código o nombre de usuario ya está registrado.");
                return 2;
            }
            await output.WriteLineAsync(JsonSerializer.Serialize(new { result.OrganizationId, result.OwnerUserId }));
            return 0;
        }
        catch (ArgumentException exception)
        {
            await error.WriteLineAsync(exception.Message);
            return 1;
        }
        catch (JsonException)
        {
            await error.WriteLineAsync("Entrada inválida. Se requiere JSON de aprovisionamiento por stdin.");
            return 1;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await error.WriteLineAsync("No se pudo completar el aprovisionamiento. Comprueba la conexión y las migraciones de la base de datos.");
            return 1;
        }
    }
}
