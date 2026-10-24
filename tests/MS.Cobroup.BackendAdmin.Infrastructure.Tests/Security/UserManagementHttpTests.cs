using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Repositories;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Tests.Security;

public sealed class UserManagementHttpTests
{
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);
    private const string Issuer = "https://test.cobroup.internal";
    private readonly Guid organizationId = Guid.NewGuid();
    private readonly Guid adminId = Guid.NewGuid();

    private WebApplicationFactory<Program> Factory(MemoryUsers repository) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:IdentityDatabase", "Host=localhost;Database=unused;Username=unused");
            builder.UseSetting("Identity:Issuer", Issuer);
            builder.UseSetting("Jwt:SigningKeyBase64", Convert.ToBase64String(Key));
            builder.UseSetting("Jwt:ActiveKeyId", "users-http-test");
            builder.ConfigureServices(services => {
                services.AddSingleton<IUserManagementRepository>(repository);
                services.AddSingleton<IUserEditingRepository>(repository);
            });
        });

    private HttpClient Client(WebApplicationFactory<Program> factory, UserRole? role)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        if (role is null) return client;
        var now = DateTimeOffset.UtcNow;
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Key) { KeyId = "users-http-test" }, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(Issuer, "cobroup-api", [
            new("sub", adminId.ToString()), new("organization_id", organizationId.ToString()),
            new("organization_code", "TEST"), new("role", role.ToString()!), new("typ", "access"),
            new("jti", Guid.NewGuid().ToString()), new("iat", now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        ], now.AddSeconds(-1).UtcDateTime, now.AddMinutes(5).UtcDateTime, credentials);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(UserRole.Supervisor, HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Collector, HttpStatusCode.Forbidden)]
    public async Task RejectsUnauthenticatedAndOperationalRoles(UserRole? role, HttpStatusCode expected)
    {
        var repository = new MemoryUsers(organizationId, adminId);
        using var factory = Factory(repository);
        using var client = Client(factory, role);
        Assert.Equal(expected, (await client.GetAsync("/api/v1/users")).StatusCode);
        Assert.Equal(expected, (await client.PostAsJsonAsync("/api/v1/users", Payload())).StatusCode);
        Assert.Null(repository.CreatedMembership);
    }

    private static object Payload(string role = "Collector") => new {
        username = "equipo.norte", displayName = "Equipo Norte", password = "private-password-test",
        phoneNumber = "+573001234567", role, organizationId = Guid.NewGuid()
    };

    private static object UpdatePayload(string role = "Collector") => new {
        username = "equipo.norte", displayName = "Equipo Actualizado", phoneNumber = "+573001234567", role, isActive = true, password = (string?)null
    };

    [Fact]
    public async Task UpdatesOnlyTenantMemberAndDoesNotExposeCredentials()
    {
        var repository = new MemoryUsers(organizationId, adminId);
        var user = ApplicationUser.Create("equipo.norte", "Equipo Norte", "old-hash", DateTimeOffset.UtcNow, "+573001234567");
        repository.Editable = OrganizationMembership.Create(user.Id, organizationId, UserRole.Collector, DateTimeOffset.UtcNow);
        typeof(OrganizationMembership).GetProperty(nameof(OrganizationMembership.User))!.SetValue(repository.Editable, user);
        using var factory = Factory(repository); using var client = Client(factory, UserRole.Admin);
        var response = await client.PutAsJsonAsync($"/api/v1/users/{user.Id}", UpdatePayload());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Equipo Actualizado", user.DisplayName); Assert.Equal("old-hash", user.PasswordHash);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("hash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/v1/users/{Guid.NewGuid()}", UpdatePayload())).StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(UserRole.Supervisor, HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Collector, HttpStatusCode.Forbidden)]
    public async Task UpdateRejectsUnauthenticatedAndOperationalRoles(UserRole? role, HttpStatusCode expected)
    {
        using var factory = Factory(new MemoryUsers(organizationId, adminId)); using var client = Client(factory, role);
        Assert.Equal(expected, (await client.PutAsJsonAsync($"/api/v1/users/{Guid.NewGuid()}", UpdatePayload())).StatusCode);
    }

    [Fact]
    public async Task UpdateRejectsSessionOrganizationChanges()
    {
        using var factory = Factory(new MemoryUsers(organizationId, adminId)); using var client = Client(factory, UserRole.Admin);
        client.DefaultRequestHeaders.Add("X-Cobroup-Expected-Organization", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/v1/users/{Guid.NewGuid()}", UpdatePayload())).StatusCode);
    }

    [Fact]
    public async Task CreatesInTokenTenantAndNeverReturnsPasswordOrHash()
    {
        var repository = new MemoryUsers(organizationId, adminId);
        using var factory = Factory(repository);
        using var client = Client(factory, UserRole.Admin);
        var response = await client.PostAsJsonAsync("/api/v1/users", Payload());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(organizationId, repository.CreatedMembership!.OrganizationId);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Collector", body);
    }

    [Fact]
    public async Task AdminCannotCreateAnotherAdmin()
    {
        var repository = new MemoryUsers(organizationId, adminId);
        using var factory = Factory(repository);
        using var client = Client(factory, UserRole.Admin);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/users", Payload("Admin"))).StatusCode);
        Assert.Null(repository.CreatedMembership);
    }

    [Fact]
    public async Task OrganizationChangeDuringSubmissionCannotCreateInNewTenant()
    {
        var repository = new MemoryUsers(organizationId, adminId);
        using var factory = Factory(repository);
        using var client = Client(factory, UserRole.Admin);
        client.DefaultRequestHeaders.Add("X-Cobroup-Expected-Organization", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/users", Payload())).StatusCode);
        Assert.Null(repository.CreatedMembership);
    }

    [Fact]
    public async Task AdminMembershipInAnotherTenantDoesNotGrantAccess()
    {
        var repository = new MemoryUsers(Guid.NewGuid(), adminId);
        using var factory = Factory(repository);
        using var client = Client(factory, UserRole.Admin);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/users", Payload())).StatusCode);
    }

    private sealed class MemoryUsers(Guid organizationId, Guid adminId) : IUserManagementRepository, IUserEditingRepository
    {
        public OrganizationMembership? Editable { get; set; }
        public OrganizationMembership? CreatedMembership { get; private set; }
        public Task<bool> IsActiveAdminAsync(Guid organization, Guid user, CancellationToken cancellationToken) =>
            Task.FromResult(organization == organizationId && user == adminId);
        public Task<IReadOnlyCollection<OrganizationUser>> ListAsync(Guid organization, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<OrganizationUser>>([]);
        public Task<bool> TryCreateAsync(ApplicationUser user, OrganizationMembership membership, AdministrativeAuditRecord audit, CancellationToken cancellationToken)
        {
            CreatedMembership = membership;
            return Task.FromResult(true);
        }
        public Task<OrganizationMembership?> FindAsync(Guid organization, Guid user, CancellationToken cancellationToken) =>
            Task.FromResult(Editable?.OrganizationId == organization && Editable.UserId == user ? Editable : null);
        public Task<bool> HasOtherMembershipsAsync(Guid organization, Guid user, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> HasActiveRouteAssignmentsAsync(Guid organization, Guid user, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> SaveAsync(OrganizationMembership membership, AdministrativeAuditRecord audit, bool revokeAllSessions, bool revokeTenantSessions, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
