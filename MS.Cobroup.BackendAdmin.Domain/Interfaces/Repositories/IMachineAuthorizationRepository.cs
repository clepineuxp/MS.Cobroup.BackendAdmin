using MS.Cobroup.BackendAdmin.Domain.MachineAuth;

namespace MS.Cobroup.BackendAdmin.Domain.Interfaces.Repositories;

public interface IMachineAuthorizationRepository
{
    Task<MachineAuthorization?> FindActiveAsync(Guid organizationId, Guid routeId, Guid userId, string machineId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<MachineAuthorizationRequest?> FindPendingAsync(Guid organizationId, Guid routeId, Guid userId, string machineId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<MachineAuthorizationRequest?> FindRequestAsync(Guid organizationId, Guid requestId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<MachineAuthorizationRequest>> ListPendingAsync(Guid organizationId, Guid? routeId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<MachineAuthorization>> ListAuthorizationsAsync(Guid organizationId, Guid routeId, CancellationToken cancellationToken);
    Task AddRequestAsync(MachineAuthorizationRequest request, CancellationToken cancellationToken);
    Task AddAuthorizationAsync(MachineAuthorization authorization, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
