using MS.Cobroup.BackendAdmin.Domain.Identity;

namespace MS.Cobroup.BackendAdmin.Core.Identity;

public sealed class OrganizationSessionService(IIdentityRepository repository)
{
    public async Task<IReadOnlyCollection<OrganizationMembershipSummary>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var memberships = await repository.ListActiveMembershipsAsync(userId, cancellationToken);
        return memberships
            .OrderByDescending(item => item.LastAccessedAtUtc)
            .ThenBy(item => item.Organization.Name)
            .Select(item => new OrganizationMembershipSummary(
                item.OrganizationId,
                item.Organization.Code,
                item.Organization.Name,
                item.Role,
                item.LastAccessedAtUtc))
            .ToArray();
    }

    public Task<OrganizationMembership?> SelectAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken) =>
        repository.SelectMembershipAsync(userId, organizationId, DateTimeOffset.UtcNow, cancellationToken);
}

public sealed record OrganizationMembershipSummary(
    Guid OrganizationId,
    string OrganizationCode,
    string OrganizationName,
    Domain.Authorization.UserRole Role,
    DateTimeOffset? LastAccessedAtUtc);
