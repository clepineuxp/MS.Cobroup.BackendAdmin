using MS.Cobroup.BackendAdmin.Domain.Authorization;

namespace MS.Cobroup.BackendAdmin.Domain.Identity;

public sealed class OrganizationMembership
{
    private OrganizationMembership()
    {
    }

    private OrganizationMembership(
        Guid userId,
        Guid organizationId,
        UserRole role,
        DateTimeOffset joinedAtUtc,
        DateTimeOffset? lastAccessedAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user is required.", nameof(userId));
        }

        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("An organization is required.", nameof(organizationId));
        }

        UserId = userId;
        OrganizationId = organizationId;
        Role = role;
        IsActive = true;
        JoinedAtUtc = joinedAtUtc;
        LastAccessedAtUtc = lastAccessedAtUtc;
    }

    public Guid UserId { get; private set; }

    public ApplicationUser User { get; private set; } = null!;

    public Guid OrganizationId { get; private set; }

    public Organization Organization { get; private set; } = null!;

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset JoinedAtUtc { get; private set; }

    public DateTimeOffset? LastAccessedAtUtc { get; private set; }

    public static OrganizationMembership Create(
        Guid userId,
        Guid organizationId,
        UserRole role,
        DateTimeOffset joinedAtUtc,
        bool markAsAccessed = false) =>
        new(userId, organizationId, role, joinedAtUtc, markAsAccessed ? joinedAtUtc : null);

    public void MarkAccessed(DateTimeOffset accessedAtUtc)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("An inactive membership cannot be selected.");
        }

        LastAccessedAtUtc = accessedAtUtc;
    }
}
