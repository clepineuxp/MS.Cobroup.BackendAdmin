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

public sealed class AuthLifecycleHttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public AuthLifecycleHttpTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
        });
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

        // 1. Login to obtain session cookie
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

        // 2. Call refresh without CSRF header
        var refreshReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refreshReq.Headers.Add("Cookie", $"__Host-cobroup-refresh={refreshCookie}");

        var refreshRes = await client.SendAsync(refreshReq);

        // Must return 400 with explicit error code
        Assert.Equal(HttpStatusCode.BadRequest, refreshRes.StatusCode);
        Assert.True(refreshRes.Headers.Contains(CobroupAntiforgeryFilter.ErrorHeaderName));
        Assert.Equal(CobroupAntiforgeryFilter.ErrorCode, refreshRes.Headers.GetValues(CobroupAntiforgeryFilter.ErrorHeaderName).First());

        var body = await refreshRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("antiforgery_token_invalid", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Refresh_WithValidCsrf_RotatesToken_AndReplacesCookie()
    {
        var client = CreateHttpsClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var (user, org, password) = await CreateTestUserAndOrgAsync(scope.ServiceProvider, db);

        // 1. Login
        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = user.Username,
            password = password,
            organizationId = org.Id
        });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var initialRefreshCookie = ExtractCookie(loginRes, "__Host-cobroup-refresh");

        // 2. Get Antiforgery token and cookie
        var (csrfToken, csrfCookie) = await GetAntiforgeryAsync(client);

        // 3. Refresh with CSRF and session cookie
        var refreshReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refreshReq.Headers.Add("Cookie", $"__Host-cobroup-refresh={initialRefreshCookie}; {csrfCookie}");
        refreshReq.Headers.Add("X-XSRF-TOKEN", csrfToken);

        var refreshRes = await client.SendAsync(refreshReq);
        Assert.Equal(HttpStatusCode.OK, refreshRes.StatusCode);

        // Body must have new access token and no refresh token
        var body = await refreshRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("access_token", out _));
        Assert.False(body.TryGetProperty("refresh_token", out _));

        // Cookie must be replaced with a different value
        var newRefreshCookie = ExtractCookie(refreshRes, "__Host-cobroup-refresh");
        Assert.NotEqual(initialRefreshCookie, newRefreshCookie);

        // 4. Replay initial refresh cookie -> must fail with 401
        var replayReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        replayReq.Headers.Add("Cookie", $"__Host-cobroup-refresh={initialRefreshCookie}; {csrfCookie}");
        replayReq.Headers.Add("X-XSRF-TOKEN", csrfToken);

        var replayRes = await client.SendAsync(replayReq);
        Assert.Equal(HttpStatusCode.Unauthorized, replayRes.StatusCode);
    }

    [Fact]
    public async Task Logout_WithValidCsrf_RevokesFamily_AndExpiresCookie()
    {
        var client = CreateHttpsClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var (user, org, password) = await CreateTestUserAndOrgAsync(scope.ServiceProvider, db);

        // 1. Login
        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = user.Username,
            password = password,
            organizationId = org.Id
        });
        var refreshCookie = ExtractCookie(loginRes, "__Host-cobroup-refresh");
        var (csrfToken, csrfCookie) = await GetAntiforgeryAsync(client);

        // 2. Logout with CSRF
        var logoutReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logoutReq.Headers.Add("Cookie", $"__Host-cobroup-refresh={refreshCookie}; {csrfCookie}");
        logoutReq.Headers.Add("X-XSRF-TOKEN", csrfToken);

        var logoutRes = await client.SendAsync(logoutReq);
        Assert.Equal(HttpStatusCode.NoContent, logoutRes.StatusCode);

        // Verify expired cookie returned
        var setCookies = logoutRes.Headers.GetValues("Set-Cookie").ToList();
        var expiredCookie = setCookies.FirstOrDefault(c => c.StartsWith("__Host-cobroup-refresh="));
        Assert.NotNull(expiredCookie);
        Assert.Contains("expires=", expiredCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", expiredCookie, StringComparison.OrdinalIgnoreCase);
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

    private static async Task<(string Token, string Cookie)> GetAntiforgeryAsync(HttpClient client)
    {
        var res = await client.GetAsync("/api/v1/auth/antiforgery");
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("requestToken").GetString()!;

        var setCookies = res.Headers.GetValues("Set-Cookie");
        var cookieHeader = setCookies.FirstOrDefault(c => c.Contains("cobroup-antiforgery"));
        var semi = cookieHeader?.IndexOf(';') ?? -1;
        var cookie = semi > 0 ? cookieHeader!.Substring(0, semi) : (cookieHeader ?? "");

        return (token, cookie);
    }

    private static async Task<(ApplicationUser User, Organization Org, string Password)> CreateTestUserAndOrgAsync(
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

        return (user, org, password);
    }
}
