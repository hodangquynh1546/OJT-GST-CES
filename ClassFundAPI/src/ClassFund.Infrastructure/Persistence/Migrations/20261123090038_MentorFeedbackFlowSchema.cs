using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassFund.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MentorFeedbackFlowSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExcludedAt",
                table: "EventParticipant",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RefundedAmount",
                table: "EventParticipant",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ListDeadline",
                table: "Event",
                type: "date",
                nullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DueDate",
                table: "Charge",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.CreateTable(
                name: "PaymentReminder",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FundId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChargeId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SentByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReminderDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentReminder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentReminder_Charge_ChargeId",
                        column: x => x.ChargeId,
                        principalTable: "Charge",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentReminder_Event_EventId",
                        column: x => x.EventId,
                        principalTable: "Event",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentReminder_Fund_FundId",
                        column: x => x.FundId,
                        principalTable: "Fund",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentReminder_User_SentByUserId",
                        column: x => x.SentByUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentReminder_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReminder_ChargeId",
                table: "PaymentReminder",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReminder_EventId",
                table: "PaymentReminder",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReminder_Fund_Event_SentAt",
                table: "PaymentReminder",
                columns: new[] { "FundId", "EventId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReminder_SentByUserId",
                table: "PaymentReminder",
                column: "SentByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_PaymentReminder_AutoPerDay",
                table: "PaymentReminder",
                columns: new[] { "UserId", "EventId", "ReminderDate" },
                unique: true,
                filter: "\"Kind\" = 'Auto' AND \"EventId\" IS NOT NULL");

            // ---- Chuyển đổi dữ liệu cũ sang luồng thu mới (thủ quỹ tích "đã thu", không còn duyệt / yêu cầu rút) ----
            // Không tự xác nhận hay rút tiền: tiền chưa ai kiểm / chưa trả nên chỉ đưa về trạng thái an toàn và báo người liên quan.

            // 1) Thanh toán đang chờ xác nhận: báo người nộp, đánh dấu bị từ chối (giữ lịch sử), nghĩa vụ về Chưa đóng.
            migrationBuilder.Sql(@"
INSERT INTO ""Notification"" (""Id"", ""UserId"", ""FundId"", ""Scope"", ""EventId"", ""Type"", ""Title"", ""Body"", ""ReferenceType"", ""ReferenceId"", ""IsRead"", ""CreatedAt"")
SELECT gen_random_uuid(), p.""PayerUserId"", c.""FundId"",
       CASE WHEN c.""EventId"" IS NULL THEN 'General' ELSE 'Event' END,
       c.""EventId"",
       'PaymentRejected',
       LEFT(CASE WHEN c.""EventId"" IS NULL THEN '[Quỹ chung]' ELSE '[Sự kiện: ' || e.""Name"" || ']' END
            || ' Khoản đóng cần nộp trực tiếp cho thủ quỹ: ' || c.""Title"", 200),
       'Hệ thống chuyển sang cách thu mới: thủ quỹ xác nhận khi nhận tiền. Lần nộp chờ xác nhận trước đó được hủy, vui lòng đưa tiền trực tiếp cho thủ quỹ.',
       'Payment', p.""Id"", FALSE, NOW()
FROM ""Payment"" p
JOIN ""ChargeAssignment"" a ON a.""Id"" = p.""AssignmentId""
JOIN ""Charge"" c ON c.""Id"" = a.""ChargeId""
LEFT JOIN ""Event"" e ON e.""Id"" = c.""EventId""
WHERE p.""Status"" = 'Submitted';");

            migrationBuilder.Sql(@"
UPDATE ""Payment""
SET ""Status"" = 'Rejected',
    ""RejectionReason"" = 'Hệ thống chuyển sang cách thu mới: vui lòng nộp trực tiếp cho thủ quỹ.'
WHERE ""Status"" = 'Submitted';");

            migrationBuilder.Sql(@"
UPDATE ""ChargeAssignment""
SET ""Status"" = 'Unpaid'
WHERE ""Status"" = 'AwaitingConfirmation';");

            // 2) Yêu cầu rút đang chờ: báo người gửi, đánh dấu từ chối (chưa trừ số dư); từ nay thủ quỹ rút số dư trực tiếp.
            migrationBuilder.Sql(@"
INSERT INTO ""Notification"" (""Id"", ""UserId"", ""FundId"", ""Scope"", ""EventId"", ""Type"", ""Title"", ""Body"", ""ReferenceType"", ""ReferenceId"", ""IsRead"", ""CreatedAt"")
SELECT gen_random_uuid(), w.""UserId"", w.""FundId"", 'General', NULL, 'WithdrawalProcessed',
       '[Quỹ chung] Yêu cầu rút tiền đã được hủy',
       'Hệ thống chuyển sang cách rút mới: thủ quỹ rút số dư trực tiếp. Số dư của bạn chưa bị trừ, vui lòng liên hệ thủ quỹ để nhận tiền.',
       'WithdrawalRequest', w.""Id"", FALSE, NOW()
FROM ""WithdrawalRequest"" w
WHERE w.""Status"" = 'Requested';");

            migrationBuilder.Sql(@"
UPDATE ""WithdrawalRequest""
SET ""Status"" = 'Rejected',
    ""HandledAt"" = NOW(),
    ""RejectionReason"" = 'Hệ thống chuyển sang cách rút mới: liên hệ thủ quỹ để rút số dư trực tiếp.'
WHERE ""Status"" = 'Requested';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Phần chuyển đổi dữ liệu cũ (thanh toán chờ → bị từ chối, yêu cầu rút → từ chối) không hoàn lại được.
            migrationBuilder.DropTable(
                name: "PaymentReminder");

            migrationBuilder.DropColumn(
                name: "ExcludedAt",
                table: "EventParticipant");

            migrationBuilder.DropColumn(
                name: "RefundedAmount",
                table: "EventParticipant");

            migrationBuilder.DropColumn(
                name: "ListDeadline",
                table: "Event");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DueDate",
                table: "Charge",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);
        }
    }
}
