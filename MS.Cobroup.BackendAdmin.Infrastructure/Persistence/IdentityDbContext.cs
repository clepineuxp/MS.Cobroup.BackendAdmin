using Microsoft.EntityFrameworkCore;
using MS.Cobroup.BackendAdmin.Domain.Identity;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    public DbSet<BootstrapMarker> BootstrapMarkers => Set<BootstrapMarker>();

    public DbSet<SecurityAuditRecord> SecurityAuditRecords => Set<SecurityAuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(48).IsRequired();
            entity.HasIndex(item => item.Code).IsUnique();
            entity.Property(item => item.Name).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        });

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Username).HasMaxLength(80).IsRequired();
            entity.Property(item => item.NormalizedUsername).HasMaxLength(80).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(item => item.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.HasIndex(item => new { item.OrganizationId, item.NormalizedUsername }).IsUnique();
            entity.HasOne(item => item.Organization)
                .WithMany()
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BootstrapMarker>(entity =>
        {
            entity.ToTable("bootstrap_markers");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasMaxLength(32);
        });

        modelBuilder.Entity<SecurityAuditRecord>(entity =>
        {
            entity.ToTable("security_audit");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EventType).HasMaxLength(96).IsRequired();
            entity.Property(item => item.Actor).HasMaxLength(160).IsRequired();
            entity.Property(item => item.SourceIp).HasMaxLength(64).IsRequired();
            entity.HasIndex(item => item.OccurredAtUtc);
        });
    }
}
