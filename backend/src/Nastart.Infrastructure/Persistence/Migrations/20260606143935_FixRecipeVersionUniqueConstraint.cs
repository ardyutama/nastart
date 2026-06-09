using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nastart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixRecipeVersionUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_recipes_user_id_name",
                table: "recipes");

            migrationBuilder.CreateIndex(
                name: "ix_recipes_version_group_id_version_label",
                table: "recipes",
                columns: new[] { "version_group_id", "version_label" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_recipes_version_group_id_version_label",
                table: "recipes");

            migrationBuilder.CreateIndex(
                name: "ix_recipes_user_id_name",
                table: "recipes",
                columns: new[] { "user_id", "name" },
                unique: true);
        }
    }
}
