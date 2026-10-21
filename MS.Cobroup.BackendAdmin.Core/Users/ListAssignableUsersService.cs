using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;

namespace MS.Cobroup.BackendAdmin.Core.Users;

public sealed class ListAssignableUsersService(IUserDirectoryRepository repository)
{
    public async Task<ListAssignableUsersResult> ExecuteAsync(
        TenantActor actor,
        CancellationToken cancellationToken)
    {
        if (actor.Role is not UserRole.Admin)
        {
            return ListAssignableUsersResult.Forbidden();
        }

        var users = await repository.ListAssignableAsync(actor.OrganizationId, cancellationToken);
        return ListAssignableUsersResult.Success(users);
    }
}

public enum ListAssignableUsersOutcome
{
    Success,
    Forbidden
}

public sealed record ListAssignableUsersResult(
    ListAssignableUsersOutcome Outcome,
    IReadOnlyCollection<AssignableUser> Users)
{
    public static ListAssignableUsersResult Success(IReadOnlyCollection<AssignableUser> users) =>
        new(ListAssignableUsersOutcome.Success, users);

    public static ListAssignableUsersResult Forbidden() =>
        new(ListAssignableUsersOutcome.Forbidden, []);
}
