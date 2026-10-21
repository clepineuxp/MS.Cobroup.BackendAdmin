using OpenIddict.Abstractions;

namespace MS.Cobroup.BackendAdmin.Api.Security;

public sealed class OidcClientSeeder(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<OidcClientSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Identity:SeedClientsOnStartup", false))
        {
            logger.LogInformation("OIDC client seeding is disabled.");
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        await EnsureClientAsync(manager, "cobroup-admin-spa",
            configuration["Identity:Clients:Admin:RedirectUri"],
            configuration["Identity:Clients:Admin:PostLogoutRedirectUri"], cancellationToken);
        await EnsureClientAsync(manager, "cobroup-operational-spa",
            configuration["Identity:Clients:Operational:RedirectUri"],
            configuration["Identity:Clients:Operational:PostLogoutRedirectUri"], cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task EnsureClientAsync(
        IOpenIddictApplicationManager manager,
        string clientId,
        string? redirectUri,
        string? postLogoutRedirectUri,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(redirectUri) || string.IsNullOrWhiteSpace(postLogoutRedirectUri))
        {
            throw new InvalidOperationException($"Redirect URIs for OIDC client '{clientId}' are required.");
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            DisplayName = clientId
        };
        descriptor.RedirectUris.Add(new Uri(redirectUri, UriKind.Absolute));
        descriptor.PostLogoutRedirectUris.Add(new Uri(postLogoutRedirectUri, UriKind.Absolute));
        descriptor.Permissions.UnionWith(
        [
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.EndSession,
            OpenIddictConstants.Permissions.Endpoints.Revocation,
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
            OpenIddictConstants.Permissions.ResponseTypes.Code,
            OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Permissions.Prefixes.Scope + "cobroup-api"
        ]);
        descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);

        var application = await manager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            await manager.CreateAsync(descriptor, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(application, descriptor, cancellationToken);
        }
    }
}
