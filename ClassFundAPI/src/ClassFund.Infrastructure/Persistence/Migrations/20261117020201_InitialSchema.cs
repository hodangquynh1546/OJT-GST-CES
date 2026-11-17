using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassFund.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FundId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EntityId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    BeforeJson = table.Column<string>(type: "jsonb", maxLength: 500, nullable: true),
                    AfterJson = table.Column<string>(type: "jsonb", maxLength: 500, nullable: true),
                    IsSelfAction = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IpAddress = table.Column<IPAddress>(type: "inet", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                    table.CheckConstraint("CK_AuditLog_Scope", "(\"Scope\" = 'Event') = (\"EventId\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Origin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SecurityStamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AvatarAttachmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Fund",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolYear = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fund", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fund_User_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FundId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notification", x => x.Id);
                    table.CheckConstraint("CK_Notification_Scope", "(\"Scope\" = 'Event') = (\"EventId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Notification_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Attachment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FundId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachment", x => x.Id);
                    table.CheckConstraint("CK_Attachment_FundRequired", "\"FundId\" IS NOT NULL OR \"Kind\" = 'Avatar'");
                    table.ForeignKey(
                        name: "FK_Attachment_Fund_FundId",
                        column: x => x.FundId,
                        principalTable: "Fund",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BalanceTransaction",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FundId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    BalanceAfter = table.Column<long>(type: "bigint", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BalanceTransaction", x => x.Id);
                    table.CheckConstraint("CK_BalanceTransaction_AfterNonNegative", "\"BalanceAfter\" >= 0");
                    table.ForeignKey(
                        name: "FK_BalanceTransaction_Fund_FundId",
                        column: x => x.FundId,
                        principalTable: "Fund",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BalanceTransaction_User_UserId",
                        column: x => x.UserId,
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
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Category", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Category_Fund_FundId",
                        column: x => x.FundId,
                        principalTable: "Fund",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Event",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FundId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EventDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ListLockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Event", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Event_Fund_FundId",
                        column: x => x.FundId,
                        principalTable: "Fund",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FundMember",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FundId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundMember", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FundMember_Fund_FundId",
                        column: x => x.FundId,
                        principalTable: "Fund",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FundMember_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PersonalBalance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FundId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Balance = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalBalance", x => x.Id);
                    table.CheckConstraint("CK_PersonalBalance_NonNegative", "\"Balance\" >= 0");
                    table.ForeignKey(
                        name: "FK_PersonalBalance_Fund_FundId",
                        column: x => x.FundId,
                        principalTable: "Fund",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalBalance_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WithdrawalRequest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FundId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HandledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    HandledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WithdrawalRequest", x => x.Id);
                    table.CheckConstraint("CK_WithdrawalRequest_Amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_WithdrawalRequest_Fund_FundId",
                        column: x => x.FundId,
                        principalTable: "Fund",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WithdrawalRequest_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Charge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FundId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AmountPerPerson = table.Column<long>(type: "bigint", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Charge", x => x.Id);
                    table.CheckConstraint("CK_Charge_Amount", "\"AmountPerPerson\" > 0");
                    table.ForeignKey(
                        name: "FK_Charge_Event_EventId",
                        column: x => x.EventId,
                        principalTable: "Event",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Charge_Fund_FundId",
                        column: x => x.FundId,
                        principalTable: "Fund",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventParticipant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Response = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsFinal = table.Column<bool>(type: "boolean", nullable: false),
                    ExclusionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventParticipant", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventParticipant_Event_EventId",
                        column: x => x.EventId,
                        principalTable: "Event",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventParticipant_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LedgerEntry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FundId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransferCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsSelfDeclared = table.Column<bool>(type: "boolean", nullable: false),
                    ReversesEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerEntry", x => x.Id);
                    table.CheckConstraint("CK_LedgerEntry_Amount", "\"Amount\" > 0");
                    table.CheckConstraint("CK_LedgerEntry_NotSelfReversal", "\"ReversesEntryId\" IS NULL OR \"ReversesEntryId\" <> \"Id\"");
                    table.ForeignKey(
                        name: "FK_LedgerEntry_Category_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Category",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LedgerEntry_Event_EventId",
                        column: x => x.EventId,
                        principalTable: "Event",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LedgerEntry_Fund_FundId",
                        column: x => x.FundId,
                        principalTable: "Fund",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LedgerEntry_LedgerEntry_ReversesEntryId",
                        column: x => x.ReversesEntryId,
                        principalTable: "LedgerEntry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LedgerEntry_User_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Settlement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FundId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParticipantCount = table.Column<int>(type: "integer", nullable: false),
                    TotalCollected = table.Column<long>(type: "bigint", nullable: false),
                    TotalSpent = table.Column<long>(type: "bigint", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SurplusMethod = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    AllocationRule = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SupplementaryChargeId = table.Column<Guid>(type: "uuid", nullable: true),
                    SettledByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SettledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settlement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Settlement_Event_EventId",
                        column: x => x.EventId,
                        principalTable: "Event",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Settlement_Fund_FundId",
                        column: x => x.FundId,
                        principalTable: "Fund",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChargeAssignment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargeId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountDue = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargeAssignment", x => x.Id);
                    table.CheckConstraint("CK_ChargeAssignment_Amount", "\"AmountDue\" >= 0");
                    table.ForeignKey(
                        name: "FK_ChargeAssignment_Charge_ChargeId",
                        column: x => x.ChargeId,
                        principalTable: "Charge",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChargeAssignment_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SettlementLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Contributed = table.Column<long>(type: "bigint", nullable: false),
                    RefundAmount = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ConfirmedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementLine", x => x.Id);
                    table.CheckConstraint("CK_SettlementLine_Amount", "\"RefundAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_SettlementLine_Settlement_SettlementId",
                        column: x => x.SettlementId,
                        principalTable: "Settlement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SettlementLine_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TransferCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConfirmationMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payment", x => x.Id);
                    table.CheckConstraint("CK_Payment_Amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_Payment_ChargeAssignment_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "ChargeAssignment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payment_User_PayerUserId",
                        column: x => x.PayerUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attachment_FundId",
                table: "Attachment",
                column: "FundId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachment_OwnerId",
                table: "Attachment",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "UX_Attachment_StorageKey",
                table: "Attachment",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Fund_At",
                table: "AuditLog",
                columns: new[] { "FundId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Fund_Scope_Event",
                table: "AuditLog",
                columns: new[] { "FundId", "Scope", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_BalanceTransaction_Fund_User_At",
                table: "BalanceTransaction",
                columns: new[] { "FundId", "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BalanceTransaction_UserId",
                table: "BalanceTransaction",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_Category_Fund_Type_Name",
                table: "Category",
                columns: new[] { "FundId", "Type", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Charge_EventId",
                table: "Charge",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_Charge_Fund_Event_Status",
                table: "Charge",
                columns: new[] { "FundId", "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ChargeAssignment_UserId",
                table: "ChargeAssignment",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_ChargeAssignment_Charge_User",
                table: "ChargeAssignment",
                columns: new[] { "ChargeId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Event_Fund_Status",
                table: "Event",
                columns: new[] { "FundId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EventParticipant_UserId",
                table: "EventParticipant",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_EventParticipant_Event_User",
                table: "EventParticipant",
                columns: new[] { "EventId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fund_CreatedByUserId",
                table: "Fund",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FundMember_UserId",
                table: "FundMember",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_FundMember_Fund_User",
                table: "FundMember",
                columns: new[] { "FundId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_FundMember_OneOwnerPerFund",
                table: "FundMember",
                column: "FundId",
                unique: true,
                filter: "\"Role\" = 'Owner' AND \"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntry_CategoryId",
                table: "LedgerEntry",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntry_EventId",
                table: "LedgerEntry",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntry_Fund_Event_Date",
                table: "LedgerEntry",
                columns: new[] { "FundId", "EventId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntry_PerformedByUserId",
                table: "LedgerEntry",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_LedgerEntry_PaymentId",
                table: "LedgerEntry",
                column: "PaymentId",
                unique: true,
                filter: "\"PaymentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_LedgerEntry_ReversesEntryId",
                table: "LedgerEntry",
                column: "ReversesEntryId",
                unique: true,
                filter: "\"ReversesEntryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_User_Read_At",
                table: "Notification",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Payment_Payer_SubmittedAt",
                table: "Payment",
                columns: new[] { "PayerUserId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_Payment_OneActivePerAssignment",
                table: "Payment",
                column: "AssignmentId",
                unique: true,
                filter: "\"Status\" <> 'Rejected'");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalBalance_UserId",
                table: "PersonalBalance",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_PersonalBalance_Fund_User",
                table: "PersonalBalance",
                columns: new[] { "FundId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Settlement_EventId",
                table: "Settlement",
                column: "EventId",
                unique: true,
                filter: "\"EventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Settlement_OneYearEndPerFund",
                table: "Settlement",
                column: "FundId",
                unique: true,
                filter: "\"EventId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementLine_UserId",
                table: "SettlementLine",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_SettlementLine_Settlement_User",
                table: "SettlementLine",
                columns: new[] { "SettlementId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_User_Email",
                table: "User",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequest_Fund_Status",
                table: "WithdrawalRequest",
                columns: new[] { "FundId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequest_UserId",
                table: "WithdrawalRequest",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_WithdrawalRequest_OnePendingPerUser",
                table: "WithdrawalRequest",
                columns: new[] { "FundId", "UserId" },
                unique: true,
                filter: "\"Status\" = 'Requested'");

            // ---- Viết tay (docs/database/database-design.md mục 4): bảng append-only (BR-08, BR-25, BR-33, NFR-06) ----
            // UPDATE/DELETE/TRUNCATE bị chặn ở mức DB, kể cả khi code có lỗi. app_user không có quyền DDL nên không tắt được trigger.
            migrationBuilder.Sql("""
                CREATE FUNCTION "fn_append_only"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Bảng % chỉ được ghi thêm (append-only): không cho phép %.', TG_TABLE_NAME, TG_OP
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;
                """);

            foreach (var table in AppendOnlyTables)
            {
                migrationBuilder.Sql($"""
                    CREATE TRIGGER "TR_{table}_AppendOnly"
                        BEFORE UPDATE OR DELETE ON "{table}"
                        FOR EACH ROW EXECUTE FUNCTION "fn_append_only"();
                    CREATE TRIGGER "TR_{table}_NoTruncate"
                        BEFORE TRUNCATE ON "{table}"
                        FOR EACH STATEMENT EXECUTE FUNCTION "fn_append_only"();
                    """);
            }
        }

        /// <summary>Bảng chỉ ghi thêm: sửa sai bằng dòng mới (bút toán đảo / điều chỉnh), không UPDATE/DELETE.</summary>
        public static readonly string[] AppendOnlyTables = ["AuditLog", "LedgerEntry", "BalanceTransaction"];

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in AppendOnlyTables)
            {
                migrationBuilder.Sql($"""
                    DROP TRIGGER IF EXISTS "TR_{table}_AppendOnly" ON "{table}";
                    DROP TRIGGER IF EXISTS "TR_{table}_NoTruncate" ON "{table}";
                    """);
            }

            migrationBuilder.Sql("""DROP FUNCTION IF EXISTS "fn_append_only"();""");

            migrationBuilder.DropTable(
                name: "Attachment");

            migrationBuilder.DropTable(
                name: "AuditLog");

            migrationBuilder.DropTable(
                name: "BalanceTransaction");

            migrationBuilder.DropTable(
                name: "EventParticipant");

            migrationBuilder.DropTable(
                name: "FundMember");

            migrationBuilder.DropTable(
                name: "LedgerEntry");

            migrationBuilder.DropTable(
                name: "Notification");

            migrationBuilder.DropTable(
                name: "Payment");

            migrationBuilder.DropTable(
                name: "PersonalBalance");

            migrationBuilder.DropTable(
                name: "SettlementLine");

            migrationBuilder.DropTable(
                name: "WithdrawalRequest");

            migrationBuilder.DropTable(
                name: "Category");

            migrationBuilder.DropTable(
                name: "ChargeAssignment");

            migrationBuilder.DropTable(
                name: "Settlement");

            migrationBuilder.DropTable(
                name: "Charge");

            migrationBuilder.DropTable(
                name: "Event");

            migrationBuilder.DropTable(
                name: "Fund");

            migrationBuilder.DropTable(
                name: "User");
        }
    }
}
