using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nastart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixEntitiesCommittedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "commited_at",
                table: "ingredient_price_histories",
                newName: "committed_at");

            migrationBuilder.RenameIndex(
                name: "ix_ingredient_price_histories_ingredient_id_commited_at",
                table: "ingredient_price_histories",
                newName: "ix_ingredient_price_histories_ingredient_id_committed_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "committed_at",
                table: "ingredient_price_histories",
                newName: "commited_at");

            migrationBuilder.RenameIndex(
                name: "ix_ingredient_price_histories_ingredient_id_committed_at",
                table: "ingredient_price_histories",
                newName: "ix_ingredient_price_histories_ingredient_id_commited_at");
        }
    }
}
