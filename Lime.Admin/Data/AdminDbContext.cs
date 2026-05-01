namespace Lime.Admin.Data;

using Lime.Admin.Models;
using Microsoft.EntityFrameworkCore;

public class AdminDbContext : DbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options) { }

    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AdminSession> AdminSessions => Set<AdminSession>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        mb.Entity<AdminUser>(e =>
        {
            e.ToTable("admin_users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(64).IsRequired();
            e.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(512).IsRequired();
            e.Property(x => x.Role)
                .HasColumnName("role")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(x => x.FailedLoginAttempts).HasColumnName("failed_login_attempts").HasDefaultValue(0);
            e.Property(x => x.LockedUntil).HasColumnName("locked_until");
            e.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("ux_admin_users_email");
            e.HasIndex(x => x.Role).HasDatabaseName("ix_admin_users_role");
        });

        mb.Entity<AdminSession>(e =>
        {
            e.ToTable("admin_sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.AdminUserId).HasColumnName("admin_user_id");
            e.Property(x => x.SessionTokenHash).HasColumnName("session_token_hash").HasMaxLength(128).IsRequired();
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            e.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
            e.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(512);

            e.HasOne(x => x.AdminUser)
                .WithMany(x => x.Sessions)
                .HasForeignKey(x => x.AdminUserId)
                .HasConstraintName("fk_admin_sessions_admin_users_admin_user_id")
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.SessionTokenHash).IsUnique().HasDatabaseName("ux_admin_sessions_session_token_hash");
            e.HasIndex(x => x.AdminUserId).HasDatabaseName("ix_admin_sessions_admin_user_id");
            e.HasIndex(x => x.ExpiresAt).HasDatabaseName("ix_admin_sessions_expires_at");
        });

        mb.Entity<AdminAuditLog>(e =>
        {
            e.ToTable("admin_audit_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.AdminUserId).HasColumnName("admin_user_id");
            e.Property(x => x.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
            e.Property(x => x.TargetType).HasColumnName("target_type").HasMaxLength(100);
            e.Property(x => x.TargetId).HasColumnName("target_id").HasMaxLength(128);
            e.Property(x => x.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            e.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
            e.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(512);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            e.HasOne(x => x.AdminUser)
                .WithMany(x => x.AuditLogs)
                .HasForeignKey(x => x.AdminUserId)
                .HasConstraintName("fk_admin_audit_logs_admin_users_admin_user_id")
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.AdminUserId).HasDatabaseName("ix_admin_audit_logs_admin_user_id");
            e.HasIndex(x => x.Action).HasDatabaseName("ix_admin_audit_logs_action");
            e.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_admin_audit_logs_created_at");
            e.HasIndex(x => new { x.TargetType, x.TargetId }).HasDatabaseName("ix_admin_audit_logs_target");
        });
    }
}
