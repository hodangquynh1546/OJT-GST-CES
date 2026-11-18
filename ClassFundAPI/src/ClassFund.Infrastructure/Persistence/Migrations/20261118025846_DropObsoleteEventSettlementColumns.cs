using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassFund.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropObsoleteEventSettlementColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupplementaryChargeId",
                table: "Settlement");

            migrationBuilder.RenameColumn(
                name: "ListLockedAt",
                table: "Event",
                newName: "CompletedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CompletedAt",
                table: "Event",
                newName: "ListLockedAt");

            migrationBuilder.AddColumn<Guid>(
                name: "SupplementaryChargeId",
                table: "Settlement",
                type: "uuid",
                nullable: true);
        }
    }
}
