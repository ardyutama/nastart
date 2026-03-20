using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nastart.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseItemsInIngredient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_purchase_items_ingredients_IngredientId1",
                table: "purchase_items");

            migrationBuilder.DropIndex(
                name: "IX_purchase_items_IngredientId1",
                table: "purchase_items");

            migrationBuilder.DropColumn(
                name: "IngredientId1",
                table: "purchase_items");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IngredientId1",
                table: "purchase_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_items_IngredientId1",
                table: "purchase_items",
                column: "IngredientId1");

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_items_ingredients_IngredientId1",
                table: "purchase_items",
                column: "IngredientId1",
                principalTable: "ingredients",
                principalColumn: "Id");
        }
    }
}
