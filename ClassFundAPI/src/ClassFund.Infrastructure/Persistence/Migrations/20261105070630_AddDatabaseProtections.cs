using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassFund.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Phần EF không sinh được: trigger TR_NoDelete, TR_AppendOnly và index theo lower("Name").
    /// </summary>
    public partial class AddDatabaseProtections : Migration
    {
        private static readonly string[] NoDeleteTables =
        [
            "User", "Fund", "Student", "ParentLink", "TreasurerAssignment", "Category", "FundTransaction",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UQ_Category_Fund_Type_Name"
                    ON "Category" ("FundId", "Type", lower("Name"));
                """);

            migrationBuilder.Sql("""
                CREATE FUNCTION "fn_block_delete"() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'Không được xóa dữ liệu của bảng %', TG_TABLE_NAME USING ERRCODE = 'P0001';
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql("""
                CREATE FUNCTION "fn_block_audit_change"() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'Nhật ký chỉ được ghi thêm' USING ERRCODE = 'P0001';
                END;
                $$ LANGUAGE plpgsql;
                """);

            foreach (var table in NoDeleteTables)
            {
                migrationBuilder.Sql($"""
                    CREATE TRIGGER "TR_NoDelete" BEFORE DELETE ON "{table}"
                        FOR EACH ROW EXECUTE FUNCTION "fn_block_delete"();
                    """);
            }

            migrationBuilder.Sql("""
                CREATE TRIGGER "TR_AppendOnly" BEFORE UPDATE OR DELETE ON "AuditLog"
                    FOR EACH ROW EXECUTE FUNCTION "fn_block_audit_change"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TRIGGER "TR_AppendOnly" ON "AuditLog";""");

            foreach (var table in NoDeleteTables)
            {
                migrationBuilder.Sql($"""DROP TRIGGER "TR_NoDelete" ON "{table}";""");
            }

            migrationBuilder.Sql("""DROP FUNCTION "fn_block_audit_change"();""");
            migrationBuilder.Sql("""DROP FUNCTION "fn_block_delete"();""");
            migrationBuilder.Sql("""DROP INDEX "UQ_Category_Fund_Type_Name";""");
        }
    }
}
