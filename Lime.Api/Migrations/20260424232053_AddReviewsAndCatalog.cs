using System;
using System.Collections.Generic;
using Lime.Api.Models;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lime.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewsAndCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "albums",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    spotify_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    cover_url = table.Column<string>(type: "text", nullable: true),
                    release_date = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    artists = table.Column<List<ArtistRef>>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_albums", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tracks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    spotify_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    album_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    track_number = table.Column<int>(type: "integer", nullable: true),
                    preview_url = table.Column<string>(type: "text", nullable: true),
                    artists = table.Column<List<ArtistRef>>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tracks", x => x.id);
                    table.ForeignKey(
                        name: "fk_tracks_albums_album_id",
                        column: x => x.album_id,
                        principalTable: "albums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    track_id = table.Column<Guid>(type: "uuid", nullable: true),
                    album_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rating = table.Column<decimal>(type: "numeric(3,1)", nullable: false),
                    body = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reviews", x => x.id);
                    table.CheckConstraint("ck_reviews_exactly_one_target", "(track_id IS NOT NULL)::int + (album_id IS NOT NULL)::int = 1");
                    table.ForeignKey(
                        name: "fk_reviews_albums_album_id",
                        column: x => x.album_id,
                        principalTable: "albums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reviews_tracks_track_id",
                        column: x => x.track_id,
                        principalTable: "tracks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reviews_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "review_reactions",
                columns: table => new
                {
                    review_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<short>(type: "smallint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_reactions", x => new { x.review_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_review_reactions_reviews_review_id",
                        column: x => x.review_id,
                        principalTable: "reviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_review_reactions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_albums_spotify_id",
                table: "albums",
                column: "spotify_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_reactions_user_id",
                table: "review_reactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_album_id",
                table: "reviews",
                column: "album_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_created_at",
                table: "reviews",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_track_id",
                table: "reviews",
                column: "track_id");

            migrationBuilder.CreateIndex(
                name: "ux_reviews_user_album_alive",
                table: "reviews",
                columns: new[] { "user_id", "album_id" },
                unique: true,
                filter: "album_id IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_reviews_user_track_alive",
                table: "reviews",
                columns: new[] { "user_id", "track_id" },
                unique: true,
                filter: "track_id IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_tracks_album_id",
                table: "tracks",
                column: "album_id");

            migrationBuilder.CreateIndex(
                name: "ix_tracks_spotify_id",
                table: "tracks",
                column: "spotify_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "review_reactions");

            migrationBuilder.DropTable(
                name: "reviews");

            migrationBuilder.DropTable(
                name: "tracks");

            migrationBuilder.DropTable(
                name: "albums");
        }
    }
}
