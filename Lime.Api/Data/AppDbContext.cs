using Lime.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Lime.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserOAuthAccount> UserOAuthAccounts => Set<UserOAuthAccount>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserSpotifyLink> UserSpotifyLinks => Set<UserSpotifyLink>();
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReviewReaction> ReviewReactions => Set<ReviewReaction>();
    public DbSet<Follow> Follows => Set<Follow>();

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

        mb.Entity<UserSpotifyLink>(e =>
        {
            e.ToTable("user_spotify_links");
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.SpotifyUserId).HasColumnName("spotify_user_id").HasMaxLength(128);
            e.Property(x => x.RefreshToken).HasColumnName("refresh_token").IsRequired();
            e.Property(x => x.AccessToken).HasColumnName("access_token");
            e.Property(x => x.AccessExpiresAt).HasColumnName("access_expires_at");
            e.Property(x => x.Scope).HasColumnName("scope");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

            e.HasOne(x => x.User)
                .WithOne()
                .HasForeignKey<UserSpotifyLink>(x => x.UserId)
                .HasConstraintName("fk_user_spotify_links_users_user_id")
                .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Album>(e =>
        {
            e.ToTable("albums");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.SpotifyId).HasColumnName("spotify_id").HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(512).IsRequired();
            e.Property(x => x.CoverUrl).HasColumnName("cover_url");
            e.Property(x => x.ReleaseDate).HasColumnName("release_date").HasMaxLength(16);
            e.Property(x => x.Artists).HasColumnName("artists").HasColumnType("jsonb");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

            e.HasIndex(x => x.SpotifyId).IsUnique().HasDatabaseName("ix_albums_spotify_id");
        });

        mb.Entity<Track>(e =>
        {
            e.ToTable("tracks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.SpotifyId).HasColumnName("spotify_id").HasMaxLength(64).IsRequired();
            e.Property(x => x.AlbumId).HasColumnName("album_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(512).IsRequired();
            e.Property(x => x.DurationMs).HasColumnName("duration_ms");
            e.Property(x => x.TrackNumber).HasColumnName("track_number");
            e.Property(x => x.PreviewUrl).HasColumnName("preview_url");
            e.Property(x => x.Artists).HasColumnName("artists").HasColumnType("jsonb");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

            e.HasIndex(x => x.SpotifyId).IsUnique().HasDatabaseName("ix_tracks_spotify_id");
            e.HasIndex(x => x.AlbumId).HasDatabaseName("ix_tracks_album_id");

            e.HasOne(x => x.Album)
                .WithMany(a => a.Tracks)
                .HasForeignKey(x => x.AlbumId)
                .HasConstraintName("fk_tracks_albums_album_id")
                .OnDelete(DeleteBehavior.SetNull);
        });

        mb.Entity<Review>(e =>
        {
            e.ToTable("reviews", t => t.HasCheckConstraint(
                "ck_reviews_exactly_one_target",
                "(track_id IS NOT NULL)::int + (album_id IS NOT NULL)::int = 1"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.TrackId).HasColumnName("track_id");
            e.Property(x => x.AlbumId).HasColumnName("album_id");
            e.Property(x => x.Rating).HasColumnName("rating").HasColumnType("numeric(3,1)");
            e.Property(x => x.Body).HasColumnName("body").HasMaxLength(140).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            e.Property(x => x.DeletedAt).HasColumnName("deleted_at");

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .HasConstraintName("fk_reviews_users_user_id")
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Track)
                .WithMany()
                .HasForeignKey(x => x.TrackId)
                .HasConstraintName("fk_reviews_tracks_track_id")
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Album)
                .WithMany()
                .HasForeignKey(x => x.AlbumId)
                .HasConstraintName("fk_reviews_albums_album_id")
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.UserId, x.TrackId })
                .IsUnique()
                .HasFilter("track_id IS NOT NULL AND deleted_at IS NULL")
                .HasDatabaseName("ux_reviews_user_track_alive");
            e.HasIndex(x => new { x.UserId, x.AlbumId })
                .IsUnique()
                .HasFilter("album_id IS NOT NULL AND deleted_at IS NULL")
                .HasDatabaseName("ux_reviews_user_album_alive");
            e.HasIndex(x => x.TrackId).HasDatabaseName("ix_reviews_track_id");
            e.HasIndex(x => x.AlbumId).HasDatabaseName("ix_reviews_album_id");
            e.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_reviews_created_at");
        });

        mb.Entity<ReviewReaction>(e =>
        {
            e.ToTable("review_reactions");
            e.HasKey(x => new { x.ReviewId, x.UserId });
            e.Property(x => x.ReviewId).HasColumnName("review_id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Kind).HasColumnName("kind").HasColumnType("smallint");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            e.HasOne(x => x.Review)
                .WithMany(r => r.Reactions)
                .HasForeignKey(x => x.ReviewId)
                .HasConstraintName("fk_review_reactions_reviews_review_id")
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .HasConstraintName("fk_review_reactions_users_user_id")
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.UserId).HasDatabaseName("ix_review_reactions_user_id");
        });

        mb.Entity<Follow>(e =>
        {
            e.ToTable("follows", t => t.HasCheckConstraint(
                "ck_follows_no_self", "follower_id <> followee_id"));
            e.HasKey(x => new { x.FollowerId, x.FolloweeId });
            e.Property(x => x.FollowerId).HasColumnName("follower_id");
            e.Property(x => x.FolloweeId).HasColumnName("followee_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            e.HasOne(x => x.Follower)
                .WithMany()
                .HasForeignKey(x => x.FollowerId)
                .HasConstraintName("fk_follows_users_follower_id")
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Followee)
                .WithMany()
                .HasForeignKey(x => x.FolloweeId)
                .HasConstraintName("fk_follows_users_followee_id")
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.FolloweeId).HasDatabaseName("ix_follows_followee_id");
        });
    }
}
