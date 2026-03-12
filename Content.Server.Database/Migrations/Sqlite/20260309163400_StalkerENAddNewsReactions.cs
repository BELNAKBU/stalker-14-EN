using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class StalkerENAddNewsReactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stalker_news_reactions",
                columns: table => new
                {
                    stalker_news_reactions_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    target_type = table.Column<int>(type: "INTEGER", nullable: false),
                    target_id = table.Column<int>(type: "INTEGER", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    reaction_id = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stalker_news_reactions", x => x.stalker_news_reactions_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stalker_news_reactions_target_type_target_id",
                table: "stalker_news_reactions",
                columns: new[] { "target_type", "target_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stalker_news_reactions_target_type_target_id_user_id_reaction_id",
                table: "stalker_news_reactions",
                columns: new[] { "target_type", "target_id", "user_id", "reaction_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stalker_news_reactions");
        }
    }
}
