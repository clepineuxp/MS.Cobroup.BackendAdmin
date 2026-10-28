using Microsoft.EntityFrameworkCore;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Repositories;
using MS.Cobroup.BackendAdmin.Domain.MachineAuth;
using MS.Cobroup.BackendAdmin.Infrastructure.Persistence;

namespace MS.Cobroup.BackendAdmin.Infrastructure.MachineAuth;

public sealed class EfMachineAuthorizationRepository(IdentityDbContext db) : IMachineAuthorizationRepository
{
    public Task<MachineAuthorization?> FindActiveAsync(Guid organizationId, Guid routeId, Guid userId, string machineId, DateTimeOffset now, CancellationToken ct) =>
        db.MachineAuthorizations.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.RouteId == routeId && x.UserId == userId && x.MachineId == machineId && x.Status == MachineAuthorizationStatus.Active && (x.IsIndefinite || x.AuthorizedUntilUtc > now), ct);
    public Task<MachineAuthorizationRequest?> FindRequestAsync(Guid organizationId, Guid requestId, CancellationToken ct) => db.MachineAuthorizationRequests.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == requestId, ct);
    public async Task<IReadOnlyCollection<MachineAuthorizationRequest>> ListPendingAsync(Guid organizationId, Guid? routeId, CancellationToken ct) => await db.MachineAuthorizationRequests.Where(x => x.OrganizationId == organizationId && (!routeId.HasValue || x.RouteId == routeId.Value)).OrderByDescending(x => x.CreatedAtUtc).ToArrayAsync(ct);
    public async Task<IReadOnlyCollection<MachineAuthorization>> ListAuthorizationsAsync(Guid organizationId, Guid routeId, CancellationToken ct) => await db.MachineAuthorizations.Where(x => x.OrganizationId == organizationId && (routeId == Guid.Empty || x.RouteId == routeId)).OrderByDescending(x => x.CreatedAtUtc).ToArrayAsync(ct);
    public Task AddRequestAsync(MachineAuthorizationRequest request, CancellationToken ct) => db.MachineAuthorizationRequests.AddAsync(request, ct).AsTask();
    public Task AddAuthorizationAsync(MachineAuthorization authorization, CancellationToken ct) => db.MachineAuthorizations.AddAsync(authorization, ct).AsTask();
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
