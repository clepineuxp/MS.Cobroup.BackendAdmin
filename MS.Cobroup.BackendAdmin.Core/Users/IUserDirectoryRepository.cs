using MS.Cobroup.BackendAdmin.Domain.Authorization;

namespace MS.Cobroup.BackendAdmin.Core.Users;

public interface IUserDirectoryRepository
{
    Task<IReadOnlyCollection<AssignableUser>> ListAssignableAsync(
        Guid organizationId,
        CancellationToken cancellationToken);
}

public sealed record AssignableUser(
    Guid Id,
    string Username,
    string DisplayName,
    UserRole Role);
