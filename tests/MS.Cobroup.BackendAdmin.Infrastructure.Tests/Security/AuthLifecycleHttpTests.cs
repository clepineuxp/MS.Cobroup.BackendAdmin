using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MS.Cobroup.BackendAdmin.Api;
using MS.Cobroup.BackendAdmin.Api.Security;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Services;
using MS.Cobroup.BackendAdmin.Infrastructure.Persistence;
using Xunit;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Tests.Security;

public sealed class AuthLifecycleHttpTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> factory;
    private readonly List<Guid> createdOrgIds = [];
    private readonly List<Guid> createdUserIds = [];

    public AuthLifecycleHttpTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

            if (createdUserIds.Count > 0 || createdOrgIds.Count > 0)
            {
                var memberships = await db.OrganizationMemberships
                    .Where(m => createdUserIds.Contains(m.UserId) || createdOrgIds.Contains(m.OrganizationId))
                    .ToListAsync();
                db.OrganizationMemberships.RemoveRange(memberships);

                var tokens = await db.RefreshTokens
                    .Where(t => createdUserIds.Contains(t.UserId) || createdOrgIds.Contains(t.OrganizationId))
                    .ToListAsync();
                db.RefreshTokens.RemoveRange(tokens);

                var users = await db.Users
                    .Where(u => createdUserIds.Contains(u.Id))
                    .ToListAsync();
                db.Users.RemoveRange(users);

                var orgs = await db.Organizations
                    .Where(o => createdOrgIds.Contains(o.Id))
                    .ToListAsync();
                db.Organizations.RemoveRange(orgs);

                await db.SaveChangesAsync();
            }
        }
        catch
        {
            // Limpieza garantizada de fixtures en base de datos
        }
    }

    [Fact]
    public async Task Login_EmitsSingleHostCookie_WithRequiredAttributes_AndNoRefreshTokenInBody()
    {
        var client = CreateHttpsClient();

        // Seed a test user and organization
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var (user, org, password) = await CreateTestUserAndOrgAsync(scope.ServiceProvider, db);

        var loginPayload = new
        {
            username = user.Username,
            password = password,
            organizationId = org.Id
        };

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginPayload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // 1. Verify Set-Cookie header
        var setCookieHeaders = response.Headers.GetValues("Set-Cookie").ToList();
        Assert.Single(setCookieHeaders, c => c.StartsWith("__Host-cobroup-refresh="));

        var refreshCookie = setCookieHeaders.First(c => c.StartsWith("__Host-cobroup-refresh="));
        Assert.Contains("httponly", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", refreshCookie, StringComparison.OrdinalIgnoreCase);

        // 2. Verify Body does NOT contain refresh token
        var body = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(body);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("access_token", out var accessTokenProp));
        Assert.False(string.IsNullOrWhiteSpace(accessTokenProp.GetString()));
        Assert.False(root.TryGetProperty("refresh_token", out _));
        Assert.False(root.TryGetProperty("rawRefreshToken", out _));
    }

    [Fact]
    public async Task Refresh_WithoutCsrf_FailsWithDistinguishableAntiforgeryError()
    {
        var client = CreateHttpsClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var (user, org, password) = await CreateTestUserAndOrgAsync(scope.ServiceProvider, db);

        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = user.Username,
            password = password,
            organizationId = org.Id
        });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var refreshCookie = ExtractCookie(loginRes, "__Host-cobroup-refresh");

        var refreshReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refreshReq.Headers.Add("Cookie", $"__Host-cobroup-refresh={refreshCookie}");

        var refreshRes = await client.SendAsync(refreshReq);

        Assert.Equal(HttpStatusCode.BadRequest, refreshRes.StatusCode);
        var resBody = await refreshRes.Content.ReadAsStringAsync();
        Assert.Contains("antiforgery", resBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_WithValidCsrf_RotatesToken_AndReplacesCookie()
    {
        var client = CreateHttpsClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var (user, org, password) = await CreateTestUserAndOrgAsync(scope.ServiceProvider, db);

        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = user.Username,
            password = password,
            organizationId = org.Id
        });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var initialRefreshCookie = ExtractCookie(loginRes, "__Host-cobroup-refresh");

        var (csrfToken, csrfCookie) = await GetAntiforgeryAsync(client);

        var refreshReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refreshReq.Headers.Add("Cookie", $"__Host-cobroup-refresh={initialRefreshCookie}; {csrfCookie}");
        refreshReq.Headers.Add("X-XSRF-TOKEN", csrfToken);

        var refreshRes = await client.SendAsync(refreshReq);
        Assert.Equal(HttpStatusCode.OK, refreshRes.StatusCode);

        var setCookies = refreshRes.Headers.GetValues("Set-Cookie").ToList();
        var newRefreshCookieHeader = setCookies.FirstOrDefault(c => c.StartsWith("__Host-cobroup-refresh="));
        Assert.NotNull(newRefreshCookieHeader);

        var newRefreshCookie = ExtractCookie(refreshRes, "__Host-cobroup-refresh");
        Assert.NotEqual(initialRefreshCookie, newRefreshCookie);

        var (csrfToken2, csrfCookie2) = await GetAntiforgeryAsync(client);
        var replayReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        replayReq.Headers.Add("Cookie", $"__Host-cobroup-refresh={initialRefreshCookie}; {csrfCookie2}");
        replayReq.Headers.Add("X-XSRF-TOKEN", csrfToken2);

        var replayRes = await client.SendAsync(replayReq);
        Assert.Equal(HttpStatusCode.Unauthorized, replayRes.StatusCode);
    }

    [Fact]
    public async Task Logout_ClearsCookie_AndReturnsNoContent()
    {
        var client = CreateHttpsClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var (user, org, password) = await CreateTestUserAndOrgAsync(scope.ServiceProvider, db);

        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = user.Username,
            password = password,
            organizationId = org.Id
        });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var refreshCookie = ExtractCookie(loginRes, "__Host-cobroup-refresh");

        var (csrfToken, csrfCookie) = await GetAntiforgeryAsync(client);

        var logoutReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logoutReq.Headers.Add("Cookie", $"__Host-cobroup-refresh={refreshCookie}; {csrfCookie}");
        logoutReq.Headers.Add("X-XSRF-TOKEN", csrfToken);

        var logoutRes = await client.SendAsync(logoutReq);
        Assert.Equal(HttpStatusCode.NoContent, logoutRes.StatusCode);

        var setCookies = logoutRes.Headers.GetValues("Set-Cookie").ToList();
        var expiredCookie = setCookies.FirstOrDefault(c => c.StartsWith("__Host-cobroup-refresh="));
        Assert.NotNull(expiredCookie);
        Assert.Contains("expires=", expiredCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", expiredCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SelectOrganization_ValidAccessAndRefresh_RevokesOldFamily_CreatesNewFamily_OldRefreshCannotRotate()
    {
        var client = CreateHttpsClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var (user, org1, org2, password) = await CreateTestUserWithTwoOrgsAsync(scope.ServiceProvider, db);

        // 1. Iniciar sesión en org1
        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = user.Username,
            password = password,
            organizationId = org1.Id
        });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var initialRefreshCookie = ExtractCookie(loginRes, "__Host-cobroup-refresh");

        var loginBody = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
        var accessTokenOrg1 = loginBody.GetProperty("access_token").GetString()!;

        // 2. Cambiar a org2 mediante PUT /api/v1/auth/organizations/current
        var (csrfToken, csrfCookie) = await GetAntiforgeryAsync(client, accessTokenOrg1);

        var switchReq = new HttpRequestMessage(HttpMethod.Put, "/api/v1/auth/organizations/current");
        switchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessTokenOrg1);
        switchReq.Headers.Add("Cookie", $"__Host-cobroup-refresh={initialRefreshCookie}; {csrfCookie}");
        switchReq.Headers.Add("X-XSRF-TOKEN", csrfToken);
        switchReq.Content = JsonContent.Create(new { organizationId = org2.Id });

        var switchRes = await client.SendAsync(switchReq);
        Assert.Equal(HttpStatusCode.OK, switchRes.StatusCode);

        var switchBody = await switchRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("authenticated", switchBody.GetProperty("status").GetString());
        var newAccessTokenOrg2 = switchBody.GetProperty("access_token").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(newAccessTokenOrg2));
        Assert.NotEqual(accessTokenOrg1, newAccessTokenOrg2);
        Assert.Equal(org2.Id, switchBody.GetProperty("organization").GetProperty("id").GetGuid());

        var newRefreshCookie = ExtractCookie(switchRes, "__Host-cobroup-refresh");
        Assert.NotEqual(initialRefreshCookie, newRefreshCookie);

        // 3. Demostrar que el refresh anterior ya no puede rotar
        var (csrfToken2, csrfCookie2) = await GetAntiforgeryAsync(client);
        var replayOldReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        replayOldReq.Headers.Add("Cookie", $"__Host-cobroup-refresh={initialRefreshCookie}; {csrfCookie2}");
        replayOldReq.Headers.Add("X-XSRF-TOKEN", csrfToken2);

        var replayOldRes = await client.SendAsync(replayOldReq);
        Assert.Equal(HttpStatusCode.Unauthorized, replayOldRes.StatusCode);

        // 4. Demostrar que la nueva sesión refresh es totalmente funcional
        var (csrfToken3, csrfCookie3) = await GetAntiforgeryAsync(client);
        var refreshNewReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refreshNewReq.Headers.Add("Cookie", $"__Host-cobroup-refresh={newRefreshCookie}; {csrfCookie3}");
        refreshNewReq.Headers.Add("X-XSRF-TOKEN", csrfToken3);

        var refreshNewRes = await client.SendAsync(refreshNewReq);
        Assert.Equal(HttpStatusCode.OK, refreshNewRes.StatusCode);
    }

    [Fact]
    public async Task SelectOrganization_WithoutRefreshCookie_Returns401_ExpiresCookie()
    {
        var client = CreateHttpsClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var (user, org1, org2, password) = await CreateTestUserWithTwoOrgsAsync(scope.ServiceProvider, db);

        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = user.Username,
            password = password,
            organizationId = org1.Id
        });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var loginBody = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = loginBody.GetProperty("access_token").GetString()!;

        var (csrfToken, csrfCookie) = await GetAntiforgeryAsync(client, accessToken);

        // Llamar sin cookie __Host-cobroup-refresh
        var switchReq = new HttpRequestMessage(HttpMethod.Put, "/api/v1/auth/organizations/current");
        switchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        switchReq.Headers.Add("Cookie", csrfCookie);
        switchReq.Headers.Add("X-XSRF-TOKEN", csrfToken);
        switchReq.Content = JsonContent.Create(new { organizationId = org2.Id });

        var switchRes = await client.SendAsync(switchReq);
        Assert.Equal(HttpStatusCode.Unauthorized, switchRes.StatusCode);

        var setCookies = switchRes.Headers.GetValues("Set-Cookie").ToList();
        var expiredCookie = setCookies.FirstOrDefault(c => c.StartsWith("__Host-cobroup-refresh="));
        Assert.NotNull(expiredCookie);
        Assert.Contains("expires=", expiredCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SelectOrganization_WithoutCsrf_FailsWithAntiforgeryError()
    {
        var client = CreateHttpsClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var (user, org1, org2, password) = await CreateTestUserWithTwoOrgsAsync(scope.ServiceProvider, db);

        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = user.Username,
            password = password,
            organizationId = org1.Id
        });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var initialRefreshCookie = ExtractCookie(loginRes, "__Host-cobroup-refresh");
        var loginBody = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = loginBody.GetProperty("access_token").GetString()!;

        // Llamar sin X-XSRF-TOKEN
        var switchReq = new HttpRequestMessage(HttpMethod.Put, "/api/v1/auth/organizations/current");
        switchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        switchReq.Headers.Add("Cookie", $"__Host-cobroup-refresh={initialRefreshCookie}");
        switchReq.Content = JsonContent.Create(new { organizationId = org2.Id });

        var switchRes = await client.SendAsync(switchReq);
        Assert.Equal(HttpStatusCode.BadRequest, switchRes.StatusCode);
    }

    [Fact]
    public async Task SelectOrganization_TargetOrgSuspended_Returns403()
    {
        var client = CreateHttpsClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var (user, org1, org2, password) = await CreateTestUserWithTwoOrgsAsync(scope.ServiceProvider, db);

        // Suspender org2
        org2.Suspend("Prueba de auditoria");
        await db.SaveChangesAsync();

        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = user.Username,
            password = password,
            organizationId = org1.Id
        });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var initialRefreshCookie = ExtractCookie(loginRes, "__Host-cobroup-refresh");
        var loginBody = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = loginBody.GetProperty("access_token").GetString()!;

        var (csrfToken, csrfCookie) = await GetAntiforgeryAsync(client, accessToken);

        var switchReq = new HttpRequestMessage(HttpMethod.Put, "/api/v1/auth/organizations/current");
        switchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        switchReq.Headers.Add("Cookie", $"__Host-cobroup-refresh={initialRefreshCookie}; {csrfCookie}");
        switchReq.Headers.Add("X-XSRF-TOKEN", csrfToken);
        switchReq.Content = JsonContent.Create(new { organizationId = org2.Id });

        var switchRes = await client.SendAsync(switchReq);
        Assert.Equal(HttpStatusCode.Forbidden, switchRes.StatusCode);
    }

    private HttpClient CreateHttpsClient()
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
    }

    private static string ExtractCookie(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            foreach (var c in cookies)
            {
                if (c.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                {
                    var val = c.Substring(name.Length + 1);
                    var semi = val.IndexOf(';');
                    return semi > 0 ? val.Substring(0, semi) : val;
                }
            }
        }
        throw new InvalidOperationException($"Cookie '{name}' was not found in response.");
    }

    private static async Task<(string Token, string Cookie)> GetAntiforgeryAsync(HttpClient client, string? bearerToken = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/antiforgery");
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("requestToken").GetString()!;

        var setCookies = res.Headers.GetValues("Set-Cookie");
        var cookieHeader = setCookies.FirstOrDefault(c => c.Contains("cobroup-antiforgery"));
        var semi = cookieHeader?.IndexOf(';') ?? -1;
        var cookie = semi > 0 ? cookieHeader!.Substring(0, semi) : (cookieHeader ?? "");

        return (token, cookie);
    }

    private async Task<(ApplicationUser User, Organization Org, string Password)> CreateTestUserAndOrgAsync(
        IServiceProvider services,
        IdentityDbContext db)
    {
        var passwordService = services.GetRequiredService<IPasswordCredentialService>();
        var password = "SecurePassword123!";
        var storedHash = passwordService.Hash(password);

        var org = Organization.Create(
            $"test-http-{Guid.NewGuid():N}"[..16],
            "HTTP Test Org",
            DateTimeOffset.UtcNow);

        var user = ApplicationUser.Create(
            $"http-user-{Guid.NewGuid():N}"[..20],
            "HTTP Test User",
            storedHash,
            DateTimeOffset.UtcNow);

        var membership = OrganizationMembership.Create(
            user.Id,
            org.Id,
            UserRole.Admin,
            DateTimeOffset.UtcNow);

        db.Organizations.Add(org);
        db.Users.Add(user);
        db.OrganizationMemberships.Add(membership);
        await db.SaveChangesAsync();

        createdOrgIds.Add(org.Id);
        createdUserIds.Add(user.Id);

        return (user, org, password);
    }

    private async Task<(ApplicationUser User, Organization Org1, Organization Org2, string Password)> CreateTestUserWithTwoOrgsAsync(
        IServiceProvider services,
        IdentityDbContext db)
    {
        var passwordService = services.GetRequiredService<IPasswordCredentialService>();
        var password = "SecurePassword123!";
        var storedHash = passwordService.Hash(password);

        var org1 = Organization.Create(
            $"org1-{Guid.NewGuid():N}"[..16],
            "Organization One",
            DateTimeOffset.UtcNow);

        var org2 = Organization.Create(
            $"org2-{Guid.NewGuid():N}"[..16],
            "Organization Two",
            DateTimeOffset.UtcNow);

        var user = ApplicationUser.Create(
            $"multi-user-{Guid.NewGuid():N}"[..20],
            "Multi Tenant User",
            storedHash,
            DateTimeOffset.UtcNow);

        var m1 = OrganizationMembership.Create(user.Id, org1.Id, UserRole.Admin, DateTimeOffset.UtcNow);
        var m2 = OrganizationMembership.Create(user.Id, org2.Id, UserRole.Supervisor, DateTimeOffset.UtcNow);

        db.Organizations.AddRange(org1, org2);
        db.Users.Add(user);
        db.OrganizationMemberships.AddRange(m1, m2);
        await db.SaveChangesAsync();

        createdOrgIds.Add(org1.Id);
        createdOrgIds.Add(org2.Id);
        createdUserIds.Add(user.Id);

        return (user, org1, org2, password);
    }
}
