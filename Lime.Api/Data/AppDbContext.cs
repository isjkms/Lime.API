using Lime.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Lime.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserOAuthAccount> UserOAuthAccounts => Set<UserOAuthAccount>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        mb.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(320);
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(64).IsRequired();
            e.Property(x => x.AvatarUrl).HasColumnName("avatar_url");
            e.Property(x => x.Bio).HasColumnName("bio");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            e.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
            e.Property(x => x.DeletedAt).HasColumnName("deleted_at");

            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("ix_users_email");
        });

        mb.Entity<UserOAuthAccount>(e =>
        {
            e.ToTable("user_oauth_accounts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(32).IsRequired();
            e.Property(x => x.ProviderUserId).HasColumnName("provider_user_id").HasMaxLength(255).IsRequired();
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(320);
            e.Property(x => x.LinkedAt).HasColumnName("linked_at").HasDefaultValueSql("now()");

            e.HasOne(x => x.User)
                .WithMany(u => u.OAuthAccounts)
                .HasForeignKey(x => x.UserId)
                .HasConstraintName("fk_user_oauth_accounts_users_user_id")
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.Provider, x.ProviderUserId })
                .IsUnique()
                .HasDatabaseName("ix_user_oauth_accounts_provider_provider_user_id");

            e.HasIndex(x => x.UserId).HasDatabaseName("ix_user_oauth_accounts_user_id");
        });

        mb.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            e.Property(x => x.ReplacedById).HasColumnName("replaced_by_id");

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .HasConstraintName("fk_refresh_tokens_users_user_id")
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("ix_refresh_tokens_token_hash");
            e.HasIndex(x => x.UserId).HasDatabaseName("ix_refresh_tokens_user_id");
            e.HasIndex(x => x.ExpiresAt).HasDatabaseName("ix_refresh_tokens_expires_at");
        });
    }
}
