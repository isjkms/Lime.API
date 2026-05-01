using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lime.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserConsents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_consents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_kind = table.Column<short>(type: "smallint", nullable: false),
                    doc_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    agreed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ip_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_consents", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_consents_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_consents_user_doc_version",
                table: "user_consents",
                columns: new[] { "user_id", "doc_kind", "doc_version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_consents");
        }
    }
}
