using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MS.Cobroup.BackendAdmin.Core.Identity;

namespace MS.Cobroup.BackendAdmin.Api.Controllers;

[ApiController]
[Route("api/v1/bootstrap")]
public sealed class BootstrapController(
    BootstrapIdentityService bootstrap,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("identity-write")]
    public async Task<IActionResult> Create(
        BootstrapRequest request,
        CancellationToken cancellationToken)
    {
        var configuredKey = configuration["Bootstrap:Key"];
        var suppliedKey = Request.Headers["X-Bootstrap-Key"].ToString();
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return Problem("Bootstrap is disabled until Bootstrap:Key is supplied securely.", statusCode: 503);
        }

        if (!FixedTimeEquals(configuredKey, suppliedKey))
        {
            return Unauthorized();
        }

        var result = await bootstrap.ExecuteAsync(
            new BootstrapIdentityCommand(
                request.OrganizationCode,
                request.OrganizationName,
                new BootstrapUserCommand(
                    request.Administrator.Username,
                    request.Administrator.DisplayName,
                    request.Administrator.Password),
                new BootstrapUserCommand(
                    request.Supervisor.Username,
                    request.Supervisor.DisplayName,
                    request.Supervisor.Password),
                new BootstrapUserCommand(
                    request.Collector.Username,
                    request.Collector.DisplayName,
                    request.Collector.Password),
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            cancellationToken);

        return result.Created
            ? Created("/api/v1/auth/session", new { result.OrganizationId, result.UserIds })
            : Conflict(new { message = "Platform bootstrap has already been completed." });
    }

    private static bool FixedTimeEquals(string expected, string supplied)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        return expectedBytes.Length == suppliedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}

public sealed record BootstrapRequest(
    string OrganizationCode,
    string OrganizationName,
    BootstrapUserRequest Administrator,
    BootstrapUserRequest Supervisor,
    BootstrapUserRequest Collector);

public sealed record BootstrapUserRequest(string Username, string DisplayName, string Password);
