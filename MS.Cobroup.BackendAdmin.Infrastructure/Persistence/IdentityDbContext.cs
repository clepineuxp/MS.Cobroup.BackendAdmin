using Microsoft.EntityFrameworkCore;
using MS.Cobroup.BackendAdmin.Domain.Identity;
using MS.Cobroup.BackendAdmin.Domain.Routes;
using MS.Cobroup.BackendAdmin.Domain.Configuration;
using MS.Cobroup.BackendAdmin.Domain.LoanTemplates;
using MS.Cobroup.BackendAdmin.Domain.PaymentMethods;

namespace MS.Cobroup.BackendAdmin.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();

    public DbSet<BootstrapMarker> BootstrapMarkers => Set<BootstrapMarker>();

    public DbSet<SecurityAuditRecord> SecurityAuditRecords => Set<SecurityAuditRecord>();

    public DbSet<CollectionRoute> Routes => Set<CollectionRoute>();

    public DbSet<RouteCollectorAssignment> RouteCollectorAssignments => Set<RouteCollectorAssignment>();

    public DbSet<AdministrativeAuditRecord> AdministrativeAuditRecords => Set<AdministrativeAuditRecord>();

    public DbSet<OrganizationSettings> OrganizationSettings => Set<OrganizationSettings>();

    public DbSet<LoanTemplate> LoanTemplates => Set<LoanTemplate>();

    public DbSet<LoanTemplateVersion> LoanTemplateVersions => Set<LoanTemplateVersion>();

    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

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
            entity.Property(item => item.PhoneNumber).HasMaxLength(24);
            entity.HasIndex(item => item.NormalizedUsername).IsUnique();
        });

        modelBuilder.Entity<OrganizationMembership>(entity =>
        {
            entity.ToTable("organization_memberships");
            entity.HasKey(item => new { item.UserId, item.OrganizationId });
            entity.Property(item => item.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.HasIndex(item => new { item.UserId, item.LastAccessedAtUtc });
            entity.HasIndex(item => item.OrganizationId);
            entity.HasIndex(item => new { item.OrganizationId, item.IsOwner }).IsUnique().HasFilter("\"IsOwner\" = TRUE");
            entity.HasOne(item => item.User)
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<CollectionRoute>(entity =>
        {
            entity.ToTable("routes");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.NormalizedName).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
            entity.Property(item => item.MaxActiveCustomersMode).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(item => item.MaxActiveLoansMode).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(item => item.InitialBalance).HasPrecision(18, 2).IsRequired();
            entity.HasIndex(item => new { item.OrganizationId, item.NormalizedName }).IsUnique();
            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.SupervisorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(item => item.CollectorAssignments)
                .WithOne(item => item.Route)
                .HasForeignKey(item => item.RouteId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(item => item.CollectorAssignments)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<OrganizationSettings>(entity =>
        {
            entity.ToTable("organization_settings");
            entity.HasKey(item => item.OrganizationId);
            entity.Property(item => item.TimeZoneId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.HasOne<Organization>()
                .WithOne()
                .HasForeignKey<OrganizationSettings>(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RouteCollectorAssignment>(entity =>
        {
            entity.ToTable("route_collector_assignments");
            entity.HasKey(item => new { item.RouteId, item.CollectorUserId });
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.CollectorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.AssignedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdministrativeAuditRecord>(entity =>
        {
            entity.ToTable("administrative_audit");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EventType).HasMaxLength(96).IsRequired();
            entity.Property(item => item.EntityType).HasMaxLength(64).IsRequired();
            entity.Property(item => item.DetailsJson).HasMaxLength(4000);
            entity.HasIndex(item => new { item.OrganizationId, item.OccurredAtUtc });
            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LoanTemplate>(entity =>
        {
            entity.ToTable("loan_templates");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.NormalizedName).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.Property(item => item.Classification).HasConversion<string>().HasMaxLength(24).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(item => item.MaxPrincipalAmount).HasPrecision(18, 4);
            entity.Property(item => item.MinPrincipalAmount).HasPrecision(18, 4);
            entity.Property(item => item.RouteIdsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.AllowedPercentagesJson).HasColumnType("jsonb").IsRequired();
            entity.Ignore(item => item.AllowedPercentages);
            entity.Ignore(item => item.MinPercentage);
            entity.Ignore(item => item.MaxPercentage);
            entity.Ignore(item => item.RouteIds);
            entity.Property<uint>("xmin").IsRowVersion();
            entity.HasIndex(item => new { item.OrganizationId, item.NormalizedName }).IsUnique();
            entity.HasIndex(item => new { item.OrganizationId, item.Status, item.Classification });
            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(item => item.Versions)
                .WithOne()
                .HasForeignKey(item => item.LoanTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Navigation(item => item.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<LoanTemplateVersion>(entity =>
        {
            entity.ToTable("loan_template_versions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.Property(item => item.Classification).HasConversion<string>().HasMaxLength(24).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(item => item.MaxPrincipalAmount).HasPrecision(18, 4);
            entity.Property(item => item.MinPrincipalAmount).HasPrecision(18, 4);
            entity.Property(item => item.RouteIdsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.AllowedPercentagesJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(item => new { item.LoanTemplateId, item.Version }).IsUnique();
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.RecordedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.ToTable("payment_methods");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(50).IsRequired();
            entity.Property(item => item.NormalizedName).HasMaxLength(50).IsRequired();
            entity.HasIndex(item => new { item.OrganizationId, item.NormalizedName }).IsUnique();
            entity.HasIndex(item => new { item.OrganizationId, item.DisplayOrder });
            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(item => item.SigningKeyId).HasMaxLength(64).IsRequired();
            entity.Property(item => item.ReplacedByTokenHash).HasMaxLength(64);
            entity.Property(item => item.RevocationReason).HasMaxLength(256);
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.HasIndex(item => item.FamilyId);
            entity.HasIndex(item => item.SigningKeyId);
            entity.HasIndex(item => new { item.UserId, item.ExpiresAtUtc });
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
