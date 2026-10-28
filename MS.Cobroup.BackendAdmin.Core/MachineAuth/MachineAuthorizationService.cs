using System.Security.Cryptography;
using System.Text;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Repositories;
using MS.Cobroup.BackendAdmin.Domain.MachineAuth;
using MS.Cobroup.BackendAdmin.Core.Routes;

namespace MS.Cobroup.BackendAdmin.Core.MachineAuth;

public sealed class MachineAuthorizationService(IMachineAuthorizationRepository repository, IRouteManagementRepository routes, IUserManagementRepository users)
{
    public async Task<OtpRequestResult> RequestOtpAsync(TenantActor actor, Guid routeId, string machineId, bool regenerate, CancellationToken cancellationToken)
    {
        if (actor.Role is UserRole.Admin || routeId == Guid.Empty) return OtpRequestResult.Forbidden();
        if (string.IsNullOrWhiteSpace(machineId)) throw new ArgumentException("La máquina es obligatoria.", nameof(machineId));
        var accessible = await routes.ListAccessibleAsync(actor.OrganizationId, actor.UserId, actor.Role, cancellationToken);
        if (accessible.All(x => x.Id != routeId)) return OtpRequestResult.Forbidden();
        var now = DateTimeOffset.UtcNow;
        var normalizedMachineId = machineId.Trim();
        var current = await repository.FindPendingAsync(actor.OrganizationId, routeId, actor.UserId, normalizedMachineId, now, cancellationToken);
        if (current is not null && !regenerate) return OtpRequestResult.Existing(current.Id, current.ExpiresAtUtc);
        current?.Cancel(actor.UserId, now);
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var request = new MachineAuthorizationRequest(actor.OrganizationId, routeId, actor.UserId, normalizedMachineId, Hash(otp), now, now.AddMinutes(120));
        await repository.AddRequestAsync(request, cancellationToken); await repository.SaveChangesAsync(cancellationToken);
        return OtpRequestResult.Created(request.Id, otp, request.ExpiresAtUtc);
    }

    public async Task<MachineAuthorizationCheckResult> CheckAsync(Guid organizationId, Guid routeId, Guid userId, string machineId, CancellationToken cancellationToken)
    {
        var item = await repository.FindActiveAsync(organizationId, routeId, userId, machineId, DateTimeOffset.UtcNow, cancellationToken);
        return item is not null ? MachineAuthorizationCheckResult.Grant(item.AuthorizedUntilUtc, item.IsIndefinite) : MachineAuthorizationCheckResult.Denied();
    }

    public async Task<IReadOnlyCollection<PendingOtpDetails>> ListPendingAsync(TenantActor actor, Guid? routeId, CancellationToken cancellationToken)
    {
        if (actor.Role is not UserRole.Admin) return [];
        var items = await repository.ListPendingAsync(actor.OrganizationId, routeId, cancellationToken);
        var names = (await users.ListAsync(actor.OrganizationId, cancellationToken)).ToDictionary(x => x.Id, x => x.DisplayName);
        return items.Where(i => i.IsValid(DateTimeOffset.UtcNow)).Select(i => new PendingOtpDetails(i.Id, i.RouteId, i.UserId, names.GetValueOrDefault(i.UserId), i.MachineId, i.CreatedAtUtc, i.ExpiresAtUtc)).ToArray();
    }

    public async Task<IReadOnlyCollection<MachineAuthorizationDetails>> ListAsync(TenantActor actor, Guid routeId, CancellationToken cancellationToken)
    {
        if (actor.Role is not UserRole.Admin) return [];
        var names = (await users.ListAsync(actor.OrganizationId, cancellationToken)).ToDictionary(x => x.Id, x => x.DisplayName);
        return (await repository.ListAuthorizationsAsync(actor.OrganizationId, routeId, cancellationToken)).Select(i => new MachineAuthorizationDetails(i.Id, i.RouteId, i.UserId, names.GetValueOrDefault(i.UserId), i.MachineId, i.CreatedAtUtc, i.AuthorizedUntilUtc, i.IsIndefinite, i.Status.ToString())).ToArray();
    }

    public async Task<MachineAuthorizationDetails?> ApproveAsync(TenantActor actor, Guid requestId, string otp, int? days, bool indefinite, CancellationToken cancellationToken)
    {
        if (actor.Role is not UserRole.Admin || (!indefinite && (days is null or <= 0 or > 3650))) return null;
        var request = await repository.FindRequestAsync(actor.OrganizationId, requestId, cancellationToken);
        if (request is null || !request.IsValid(DateTimeOffset.UtcNow) || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(request.OtpHash), Convert.FromHexString(Hash(otp)))) return null;
        var now = DateTimeOffset.UtcNow; var authorization = new MachineAuthorization(actor.OrganizationId, request.RouteId, request.UserId, request.MachineId, now, indefinite ? null : now.AddDays(days!.Value), indefinite);
        request.Approve(actor.UserId, now); await repository.AddAuthorizationAsync(authorization, cancellationToken); await repository.SaveChangesAsync(cancellationToken);
        return new MachineAuthorizationDetails(authorization.Id, authorization.RouteId, authorization.UserId, null, authorization.MachineId, authorization.CreatedAtUtc, authorization.AuthorizedUntilUtc, authorization.IsIndefinite, authorization.Status.ToString());
    }

    public async Task<bool> RevokeAsync(TenantActor actor, Guid id, CancellationToken cancellationToken)
    {
        if (actor.Role is not UserRole.Admin) return false;
        var items = await repository.ListAuthorizationsAsync(actor.OrganizationId, Guid.Empty, cancellationToken);
        var item = items.FirstOrDefault(x => x.Id == id); if (item is null) return false;
        item.Revoke(actor.UserId, DateTimeOffset.UtcNow); await repository.SaveChangesAsync(cancellationToken); return true;
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed record OtpRequestResult(bool Success, Guid? RequestId, string? Otp, DateTimeOffset? ExpiresAtUtc, string? Error, bool Reused)
{
    public static OtpRequestResult Forbidden() => new(false, null, null, null, "No autorizado.", false);
    public static OtpRequestResult Created(Guid requestId, string otp, DateTimeOffset expiresAtUtc) => new(true, requestId, otp, expiresAtUtc, null, false);
    public static OtpRequestResult Existing(Guid requestId, DateTimeOffset expiresAtUtc) => new(true, requestId, null, expiresAtUtc, null, true);
}
public sealed record MachineAuthorizationCheckResult(bool Allowed, DateTimeOffset? AuthorizedUntilUtc, bool Indefinite)
{ public static MachineAuthorizationCheckResult Denied() => new(false, null, false); public static MachineAuthorizationCheckResult Grant(DateTimeOffset? until, bool indefinite) => new(true, until, indefinite); }
public sealed record PendingOtpDetails(Guid RequestId, Guid RouteId, Guid UserId, string? UserDisplayName, string MachineId, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc);
public sealed record MachineAuthorizationDetails(Guid Id, Guid RouteId, Guid UserId, string? UserDisplayName, string MachineId, DateTimeOffset CreatedAtUtc, DateTimeOffset? AuthorizedUntilUtc, bool Indefinite, string Status);
