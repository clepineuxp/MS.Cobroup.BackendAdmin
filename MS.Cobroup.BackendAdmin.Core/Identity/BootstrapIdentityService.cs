using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.Authorization;

namespace MS.Cobroup.BackendAdmin.Core.Identity;

public sealed class BootstrapIdentityService(IIdentityRepository repository, IPasswordCredentialService passwords)
{
    public async Task<BootstrapIdentityResult> ExecuteAsync(
        BootstrapIdentityCommand command,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var organization = Organization.Create(command.OrganizationCode, command.OrganizationName, now);
        var users = new[]
        {
            CreateUser(command.Administrator, now),
            CreateUser(command.Supervisor, now),
            CreateUser(command.Collector, now)
        };
        if (users.Select(user => user.NormalizedUsername).Distinct(StringComparer.Ordinal).Count() != users.Length)
        {
            throw new ArgumentException("Bootstrap usernames must be globally unique.", nameof(command));
        }

        var memberships = new[]
        {
            OrganizationMembership.Create(users[0].Id, organization.Id, UserRole.Admin, now, markAsAccessed: true),
            OrganizationMembership.Create(users[1].Id, organization.Id, UserRole.Supervisor, now, markAsAccessed: true),
            OrganizationMembership.Create(users[2].Id, organization.Id, UserRole.Collector, now, markAsAccessed: true)
        };

        var audit = SecurityAuditRecord.BootstrapCompleted("platform-bootstrap", command.SourceIp, now);

        var created = await repository.TryBootstrapAsync(organization, users, memberships, audit, cancellationToken);
        return created
            ? new BootstrapIdentityResult(true, organization.Id, users.Select(user => user.Id).ToArray())
            : new BootstrapIdentityResult(false, null, []);

        ApplicationUser CreateUser(
            BootstrapUserCommand user,
            DateTimeOffset createdAtUtc) =>
            ApplicationUser.Create(
                user.Username,
                user.DisplayName,
                passwords.Hash(user.Password),
                createdAtUtc);
    }
}

public sealed record BootstrapIdentityCommand(
    string OrganizationCode,
    string OrganizationName,
    BootstrapUserCommand Administrator,
    BootstrapUserCommand Supervisor,
    BootstrapUserCommand Collector,
    string SourceIp);

public sealed record BootstrapUserCommand(string Username, string DisplayName, string Password);

public sealed record BootstrapIdentityResult(bool Created, Guid? OrganizationId, IReadOnlyCollection<Guid> UserIds);
