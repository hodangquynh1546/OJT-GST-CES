using Microsoft.EntityFrameworkCore;
using Npgsql;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Infrastructure.Persistence;

namespace ClassFund.IntegrationTests.Foundation;

/// <summary>
/// M0-02: migration InitialSchema áp được lên PostgreSQL và các ràng buộc viết trong migration hoạt động
/// (docs/database/database-design.md mục 4).
/// </summary>
public class InitialSchemaTests(PostgresFixture database) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Model_matches_migrations_and_nothing_is_pending()
    {
        await using var db = database.CreateContext();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges(), "Model đổi nhưng chưa có migration mới.");
    }

    [Fact]
    public async Task Obsolete_columns_are_gone_and_event_has_completed_at()
    {
        await using var db = database.CreateContext();
        var columns = await db.Database
            .SqlQueryRaw<string>(
                "SELECT table_name || '.' || column_name AS \"Value\" FROM information_schema.columns WHERE table_schema = 'public' AND table_name IN ('Event', 'Settlement')")
            .ToListAsync();

        Assert.Contains("Event.CompletedAt", columns);
        Assert.DoesNotContain("Event.ListLockedAt", columns);
        Assert.DoesNotContain("Settlement.SupplementaryChargeId", columns);
    }

    [Theory]
    [InlineData("AuditLog")]
    [InlineData("LedgerEntry")]
    [InlineData("BalanceTransaction")]
    public async Task Append_only_tables_reject_update_delete_and_truncate(string table)
    {
        await using var db = database.CreateContext();
        var (fund, user) = await SeedFundAsync(db);
        await InsertRowAsync(db, table, fund, user);

        foreach (var sql in new[]
        {
            $"UPDATE \"{table}\" SET \"Id\" = \"Id\"",
            $"DELETE FROM \"{table}\"",
            $"TRUNCATE \"{table}\" CASCADE",
        })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql));
            Assert.Equal(PostgresErrorCodes.RestrictViolation, error.SqlState);
        }
    }

    [Fact]
    public async Task Audit_scope_must_match_event_id()
    {
        await using var db = database.CreateContext();
        db.AuditLogs.Add(new AuditLog(Now, null, null, FinancialScope.Event, eventId: null, "x.test", null, null));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("CK_AuditLog_Scope", ((PostgresException)error.InnerException!).ConstraintName);
    }

    [Fact]
    public async Task Fund_has_only_one_active_owner()
    {
        await using var db = database.CreateContext();
        var (fund, _) = await SeedFundAsync(db);
        var other = NewUser();
        db.Users.Add(other);
        db.FundMembers.Add(new FundMember(fund.Id, other.Id, MemberRole.Owner, Now));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("UX_FundMember_OneOwnerPerFund", ((PostgresException)error.InnerException!).ConstraintName);
    }

    [Fact]
    public async Task Only_one_pending_withdrawal_per_member()
    {
        await using var db = database.CreateContext();
        var (fund, user) = await SeedFundAsync(db);
        db.WithdrawalRequests.Add(new WithdrawalRequest(fund.Id, user.Id, 10_000, Now));
        await db.SaveChangesAsync();
        db.WithdrawalRequests.Add(new WithdrawalRequest(fund.Id, user.Id, 10_000, Now));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("UX_WithdrawalRequest_OnePendingPerUser", ((PostgresException)error.InnerException!).ConstraintName);
    }

    [Fact]
    public async Task Year_end_settlement_happens_once_per_fund()
    {
        await using var db = database.CreateContext();
        var (fund, user) = await SeedFundAsync(db);
        db.Settlements.Add(new Settlement(fund.Id, null, 1, 100, 50, user.Id, Now) { Outcome = SettlementOutcome.Surplus });
        await db.SaveChangesAsync();
        db.Settlements.Add(new Settlement(fund.Id, null, 1, 100, 50, user.Id, Now) { Outcome = SettlementOutcome.Surplus });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("UX_Settlement_OneYearEndPerFund", ((PostgresException)error.InnerException!).ConstraintName);
    }

    [Fact]
    public async Task Personal_balance_detects_concurrent_updates()
    {
        Guid balanceId;
        await using (var setup = database.CreateContext())
        {
            var (fund, user) = await SeedFundAsync(setup);
            var balance = new PersonalBalance(fund.Id, user.Id);
            setup.PersonalBalances.Add(balance);
            await setup.SaveChangesAsync();
            balanceId = balance.Id;
        }

        await using var first = database.CreateContext();
        await using var second = database.CreateContext();
        var a = await first.PersonalBalances.SingleAsync(item => item.Id == balanceId);
        var b = await second.PersonalBalances.SingleAsync(item => item.Id == balanceId);
        a.Balance += 10_000;
        await first.SaveChangesAsync();
        b.Balance += 20_000;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    private static User NewUser() =>
        new($"u{Guid.NewGuid():N}@test.local", "Test", "hash", UserOrigin.SelfRegistered, false, Now);

    private static async Task<(Fund Fund, User User)> SeedFundAsync(AppDbContext db)
    {
        var user = NewUser();
        var fund = new Fund("Quỹ test", user.Id, "2026-2027", Now);
        db.Users.Add(user);
        db.Funds.Add(fund);
        db.FundMembers.Add(new FundMember(fund.Id, user.Id, MemberRole.Owner, Now));
        await db.SaveChangesAsync();
        return (fund, user);
    }

    private static async Task InsertRowAsync(AppDbContext db, string table, Fund fund, User user)
    {
        switch (table)
        {
            case "AuditLog":
                db.AuditLogs.Add(new AuditLog(Now, fund.Id, user.Id, FinancialScope.General, null, "x.test", null, null));
                break;
            case "LedgerEntry":
                db.LedgerEntries.Add(new LedgerEntry(fund.Id, null, LedgerType.Income, LedgerSource.Manual, null, "Thu", null, 1_000, new DateOnly(2026, 11, 17), user.Id, Now));
                break;
            case "BalanceTransaction":
                db.BalanceTransactions.Add(new BalanceTransaction(fund.Id, user.Id, BalanceTransactionType.Adjustment, 1_000, 1_000, null, user.Id, Now));
                break;
        }

        await db.SaveChangesAsync();
    }
}
