using MS.Cobroup.BackendAdmin.Core.Identity;
using MS.Cobroup.BackendAdmin.Core.Routes;
using MS.Cobroup.BackendAdmin.Domain.Authorization;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.Routes;

namespace MS.Cobroup.BackendAdmin.Core.Users;

public sealed class UserManagementService(IUserManagementRepository repository, IPasswordCredentialService passwords)
{
    private Task<bool> CanManageAsync(TenantActor actor, CancellationToken cancellationToken) =>
        actor.Role == UserRole.Admin && actor.UserId != Guid.Empty && actor.OrganizationId != Guid.Empty
            ? repository.IsActiveAdminAsync(actor.OrganizationId, actor.UserId, cancellationToken)
            : Task.FromResult(false);

    public async Task<UserManagementResult> ListAsync(TenantActor actor, CancellationToken cancellationToken)
    {
        if (!await CanManageAsync(actor, cancellationToken))
            return new(UserManagementOutcome.Forbidden);
        return new(UserManagementOutcome.Success, await repository.ListAsync(actor.OrganizationId, cancellationToken));
    }

    public async Task<UserManagementResult> CreateAsync(TenantActor actor, CreateOrganizationUserInput input, CancellationToken cancellationToken)
    {
        if (!await CanManageAsync(actor, cancellationToken))
            return new(UserManagementOutcome.Forbidden);
        if (input.Role is not (UserRole.Supervisor or UserRole.Collector))
            return new(UserManagementOutcome.Invalid, Error: "Solo puedes crear supervisores y colaboradores (cobradores).");

        try
        {
            UserCredentialValidation.Validate(input.Username, input.DisplayName, input.Password, input.PhoneNumber);
        }
        catch (ArgumentException exception)
        {
            return new(UserManagementOutcome.Invalid, Error: exception.Message);
        }

        var now = DateTimeOffset.UtcNow;
        var user = ApplicationUser.Create(input.Username, input.DisplayName, passwords.Hash(input.Password), now, input.PhoneNumber);
        var membership = OrganizationMembership.Create(user.Id, actor.OrganizationId, input.Role, now, markAsAccessed: true);
        if (input.CanReopenDay)
        {
            membership.SetCanReopenDay(true);
        }
        var audit = AdministrativeAuditRecord.UserCreated(actor.OrganizationId, actor.UserId, user.Id, now);
        var created = await repository.TryCreateAsync(user, membership, audit, cancellationToken);
        return created
            ? new(UserManagementOutcome.Success, User: new(user.Id, user.Username, user.DisplayName, input.Role, true, false, now, user.PhoneNumber, membership.CanReopenDay))
            : new(UserManagementOutcome.Conflict, Error: "El nombre de usuario no está disponible. Elige otro.");
    }
}

public sealed record CreateOrganizationUserInput(string Username, string DisplayName, string Password, UserRole Role, string PhoneNumber, bool CanReopenDay = false);
public enum UserManagementOutcome { Success, Forbidden, Invalid, Conflict }
public sealed record UserManagementResult(UserManagementOutcome Outcome,
    IReadOnlyCollection<OrganizationUser>? Users = null, OrganizationUser? User = null, string? Error = null);
