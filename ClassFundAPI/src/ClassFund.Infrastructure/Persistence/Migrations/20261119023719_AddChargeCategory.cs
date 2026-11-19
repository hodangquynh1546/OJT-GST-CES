using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassFund.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChargeCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "Charge",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Charge_CategoryId",
                table: "Charge",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Charge_Category_CategoryId",
                table: "Charge",
                column: "CategoryId",
                principalTable: "Category",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Charge_Category_CategoryId",
                table: "Charge");

            migrationBuilder.DropIndex(
                name: "IX_Charge_CategoryId",
                table: "Charge");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Charge");
        }
    }
}
