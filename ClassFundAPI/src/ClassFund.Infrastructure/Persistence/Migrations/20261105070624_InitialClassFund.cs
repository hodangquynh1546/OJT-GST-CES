using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClassFund.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialClassFund : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Username = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SecurityStamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                    table.CheckConstraint("CK_User_Kind", " \"Kind\" IN ('Teacher', 'Student', 'Parent') ");
                    table.CheckConstraint("CK_User_LoginName", " \"NormalizedEmail\" IS NOT NULL OR \"Username\" IS NOT NULL ");
                    table.CheckConstraint("CK_User_Status", " \"Status\" IN ('Active', 'Disabled') ");
                });

            migrationBuilder.CreateTable(
                name: "AuditLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EntityId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OldValues = table.Column<string>(type: "jsonb", nullable: true),
                    NewValues = table.Column<string>(type: "jsonb", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IpAddress = table.Column<IPAddress>(type: "inet", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                    table.CheckConstraint("CK_AuditLog_Category", " \"Category\" IN ('Change', 'Security') ");
                    table.ForeignKey(
                        name: "FK_AuditLog_User_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Fund",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fund", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fund_User_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Category",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FundId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Category", x => x.Id);
                    table.UniqueConstraint("AK_Category_Id_FundId_Type", x => new { x.Id, x.FundId, x.Type });
                    table.CheckConstraint("CK_Category_Type", " \"Type\" IN ('Income', 'Expense') ");
                    table.ForeignKey(
                        name: "FK_Category_Fund_FundId",
                        column: x => x.FundId,
                        principalTable: "Fund",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Student",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FundId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Student", x => x.Id);
                    table.UniqueConstraint("AK_Student_Id_FundId", x => new { x.Id, x.FundId });
                    table.ForeignKey(
                        name: "FK_Student_Fund_FundId",
                        column: x => x.FundId,
                        principalTable: "Fund",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Student_User_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Student_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FundTransaction",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FundId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundTransaction", x => x.Id);
                    table.CheckConstraint("CK_FundTransaction_AmountPositive", " \"Amount\" > 0 ");
                    table.CheckConstraint("CK_FundTransaction_CancelledTrace", " \"IsCancelled\" = (\"CancelledAt\" IS NOT NULL) ");
                    table.CheckConstraint("CK_FundTransaction_Type", " \"Type\" IN ('Income', 'Expense') ");
                    table.ForeignKey(
                        name: "FK_FundTransaction_Category_SameFundAndType",
                        columns: x => new { x.CategoryId, x.FundId, x.Type },
                        principalTable: "Category",
                        principalColumns: new[] { "Id", "FundId", "Type" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FundTransaction_Fund_FundId",
                        column: x => x.FundId,
                        principalTable: "Fund",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FundTransaction_User_CancelledBy",
                        column: x => x.CancelledBy,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FundTransaction_User_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FundTransaction_User_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParentLink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RemovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentLink", x => x.Id);
                    table.CheckConstraint("CK_ParentLink_RemovedTrace", " \"IsActive\" = (\"RemovedAt\" IS NULL) ");
                    table.ForeignKey(
                        name: "FK_ParentLink_Student_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Student",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParentLink_User_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParentLink_User_ParentUserId",
                        column: x => x.ParentUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParentLink_User_RemovedBy",
                        column: x => x.RemovedBy,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TreasurerAssignment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FundId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RevokedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreasurerAssignment", x => x.Id);
                    table.CheckConstraint("CK_TreasurerAssignment_RevokedTrace", " (\"RevokedAt\" IS NULL) = (\"RevokedBy\" IS NULL) ");
                    table.ForeignKey(
                        name: "FK_TreasurerAssignment_Fund_FundId",
                        column: x => x.FundId,
                        principalTable: "Fund",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreasurerAssignment_Student_SameFund",
                        columns: x => new { x.StudentId, x.FundId },
                        principalTable: "Student",
                        principalColumns: new[] { "Id", "FundId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreasurerAssignment_User_AssignedBy",
                        column: x => x.AssignedBy,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreasurerAssignment_User_RevokedBy",
                        column: x => x.RevokedBy,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_ActorUserId",
                table: "AuditLog",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Category_FundId",
                table: "Category",
                column: "FundId");

            migrationBuilder.CreateIndex(
                name: "IX_Fund_OwnerUserId",
                table: "Fund",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FundTransaction_CancelledBy",
                table: "FundTransaction",
                column: "CancelledBy");

            migrationBuilder.CreateIndex(
                name: "IX_FundTransaction_CategoryId_FundId_Type",
                table: "FundTransaction",
                columns: new[] { "CategoryId", "FundId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_FundTransaction_CreatedBy",
                table: "FundTransaction",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_FundTransaction_Fund_CreatedAt",
                table: "FundTransaction",
                columns: new[] { "FundId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FundTransaction_Fund_OccurredOn",
                table: "FundTransaction",
                columns: new[] { "FundId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_FundTransaction_UpdatedBy",
                table: "FundTransaction",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "UQ_FundTransaction_FundId_IdempotencyKey",
                table: "FundTransaction",
                columns: new[] { "FundId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParentLink_CreatedBy",
                table: "ParentLink",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ParentLink_ParentUserId",
                table: "ParentLink",
                column: "ParentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentLink_RemovedBy",
                table: "ParentLink",
                column: "RemovedBy");

            migrationBuilder.CreateIndex(
                name: "UQ_ParentLink_Student_Active",
                table: "ParentLink",
                column: "StudentId",
                unique: true,
                filter: "\"IsActive\"");

            migrationBuilder.CreateIndex(
                name: "IX_Student_CreatedBy",
                table: "Student",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Student_FundId",
                table: "Student",
                column: "FundId");

            migrationBuilder.CreateIndex(
                name: "UQ_Student_UserId",
                table: "Student",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TreasurerAssignment_AssignedBy",
                table: "TreasurerAssignment",
                column: "AssignedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TreasurerAssignment_FundId",
                table: "TreasurerAssignment",
                column: "FundId");

            migrationBuilder.CreateIndex(
                name: "IX_TreasurerAssignment_RevokedBy",
                table: "TreasurerAssignment",
                column: "RevokedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TreasurerAssignment_StudentId_FundId",
                table: "TreasurerAssignment",
                columns: new[] { "StudentId", "FundId" });

            migrationBuilder.CreateIndex(
                name: "UQ_TreasurerAssignment_Student_Active",
                table: "TreasurerAssignment",
                column: "StudentId",
                unique: true,
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_User_NormalizedEmail",
                table: "User",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_User_Username",
                table: "User",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLog");

            migrationBuilder.DropTable(
                name: "FundTransaction");

            migrationBuilder.DropTable(
                name: "ParentLink");

            migrationBuilder.DropTable(
                name: "TreasurerAssignment");

            migrationBuilder.DropTable(
                name: "Category");

            migrationBuilder.DropTable(
                name: "Student");

            migrationBuilder.DropTable(
                name: "Fund");

            migrationBuilder.DropTable(
                name: "User");
        }
    }
}
