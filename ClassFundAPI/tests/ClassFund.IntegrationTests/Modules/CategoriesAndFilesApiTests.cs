using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.IntegrationTests.Modules;

/// <summary>M5 – Danh mục, chứng từ, ảnh đại diện (M5-T3).</summary>
[Collection(ApiCollection.Name)]
public class CategoriesAndFilesApiTests(ApiTestHost host)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Categories_crud_with_permissions_FR14()
    {
        var (fund, owner, student, _) = await SetupAsync();
        var asOwner = await host.LoginAsync(owner);
        var asStudent = await host.LoginAsync(student);
        var url = $"/api/v1/funds/{fund.Id}/categories";

        // Học sinh xem được, không thêm được.
        Assert.Equal(HttpStatusCode.OK, (await asStudent.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await asStudent.PostAsJsonAsync(url, new { name = "Văn nghệ", type = "Expense" })).StatusCode);

        var created = await asOwner.PostAsJsonAsync(url, new { name = "Văn nghệ", type = "Expense" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var duplicate = await asOwner.PostAsJsonAsync(url, new { name = "văn nghệ", type = "Expense" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("category.duplicate_name", (await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        Assert.Equal(HttpStatusCode.BadRequest, (await asOwner.PostAsJsonAsync(url, new { name = "", type = "Expense" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await asOwner.PostAsJsonAsync(url, new { name = "A", type = 2 })).StatusCode);

        var updated = await asOwner.PutAsJsonAsync($"{url}/{id}", new { name = "Văn nghệ 20/11", isActive = false });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.False((await updated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await asOwner.PutAsJsonAsync($"{url}/{Guid.NewGuid()}", new { name = "X", isActive = true })).StatusCode);

        var active = await asOwner.GetFromJsonAsync<JsonElement>($"{url}?type=Expense&activeOnly=true");
        Assert.DoesNotContain(active.EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Upload_and_download_follow_kind_permissions_and_AZ05_TC_M5_02()
    {
        var (fund, owner, studentA, studentB) = await SetupAsync();
        var asA = await host.LoginAsync(studentA);
        var url = $"/api/v1/funds/{fund.Id}/attachments";

        // Học sinh không tải hóa đơn chi (Receipt) được.
        Assert.Equal(HttpStatusCode.Forbidden, (await asA.PostAsync(url, ApiTestHost.Multipart(ApiTestHost.Pdf, "hd.pdf", "application/pdf", "Receipt"))).StatusCode);
        // Avatar không tải qua endpoint quỹ.
        Assert.Equal(HttpStatusCode.BadRequest, (await asA.PostAsync(url, ApiTestHost.Multipart(ApiTestHost.Png, "a.png", "image/png", "Avatar"))).StatusCode);

        // Học sinh không còn tự nộp: minh chứng chuyển khoản do thủ quỹ tải lên (Payment.Confirm).
        Assert.Equal(HttpStatusCode.Forbidden, (await asA.PostAsync(url, ApiTestHost.Multipart(ApiTestHost.Png, "bill-cua-A.png", "image/png", "PaymentProof"))).StatusCode);
        var asOwner = await host.LoginAsync(owner);
        var upload = await asOwner.PostAsync(url, ApiTestHost.Multipart(ApiTestHost.Png, "bill-cua-A.png", "image/png", "PaymentProof"));
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var proofId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Gắn minh chứng vào thanh toán của A (việc M6 sẽ làm qua AttachToAsync).
        await AttachToPaymentAsync(fund, owner, studentA, proofId);

        await using (var db = host.CreateDbContext())
        {
            var key = await db.Attachments.Where(item => item.Id == proofId).Select(item => item.StorageKey).SingleAsync();
            Assert.DoesNotContain("bill", key, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(Path.Combine(host.StorageRoot, key)));
        }

        var asB = await host.LoginAsync(studentB);
        Assert.Equal(HttpStatusCode.NotFound, (await asB.GetAsync($"{url}/{proofId}")).StatusCode);

        var own = await asA.GetAsync($"{url}/{proofId}");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal("image/png", own.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ApiTestHost.Png, await own.Content.ReadAsByteArrayAsync());
        Assert.Contains("nosniff", own.Headers.GetValues("X-Content-Type-Options"));

        Assert.Equal(HttpStatusCode.OK, (await asOwner.GetAsync($"{url}/{proofId}")).StatusCode);

        var outsider = await host.LoginAsync(await host.CreateUserAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"{url}/{proofId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Anonymous().GetAsync($"{url}/{proofId}")).StatusCode);
    }

    [Fact]
    public async Task Wrong_type_or_oversized_file_is_422_file_invalid()
    {
        var (fund, owner, _, _) = await SetupAsync();
        var asOwner = await host.LoginAsync(owner);
        var url = $"/api/v1/funds/{fund.Id}/attachments";

        var exe = await asOwner.PostAsync(url, ApiTestHost.Multipart("MZ-not-a-pdf"u8.ToArray(), "hd.pdf", "application/pdf", "Receipt"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, exe.StatusCode);
        Assert.Equal("file.invalid", (await exe.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        byte[] big = [.. ApiTestHost.Pdf, .. new byte[5 * 1024 * 1024]];
        var oversized = await asOwner.PostAsync(url, ApiTestHost.Multipart(big, "hd.pdf", "application/pdf", "Receipt"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, oversized.StatusCode);
    }

    [Fact]
    public async Task Avatar_upload_rules_and_visibility_TC_M5_01()
    {
        var (_, _, studentA, studentB) = await SetupAsync();
        var asA = await host.LoginAsync(studentA);

        var pdf = await asA.PutAsync("/api/v1/auth/me/avatar", ApiTestHost.Multipart(ApiTestHost.Pdf, "a.pdf", "application/pdf"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, pdf.StatusCode);

        byte[] big = [.. ApiTestHost.Png, .. new byte[2 * 1024 * 1024]];
        var oversized = await asA.PutAsync("/api/v1/auth/me/avatar", ApiTestHost.Multipart(big, "a.png", "image/png"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, oversized.StatusCode);

        var ok = await asA.PutAsync("/api/v1/auth/me/avatar", ApiTestHost.Multipart(ApiTestHost.Png, "a.png", "image/png"));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var asB = await host.LoginAsync(studentB);
        Assert.Equal(HttpStatusCode.OK, (await asB.GetAsync($"/api/v1/users/{studentA.Id}/avatar")).StatusCode);
        var outsider = await host.LoginAsync(await host.CreateUserAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/v1/users/{studentA.Id}/avatar")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await asB.GetAsync($"/api/v1/users/{studentB.Id}/avatar")).StatusCode);
    }

    private async Task<(Fund Fund, User Owner, User StudentA, User StudentB)> SetupAsync()
    {
        var owner = await host.CreateUserAsync();
        var fund = await host.CreateFundAsync(owner);
        var studentA = await host.CreateUserAsync(UserOrigin.Provisioned);
        var studentB = await host.CreateUserAsync(UserOrigin.Provisioned);
        await host.AddMemberAsync(fund, studentA);
        await host.AddMemberAsync(fund, studentB);
        return (fund, owner, studentA, studentB);
    }

    private async Task AttachToPaymentAsync(Fund fund, User owner, User payer, Guid attachmentId)
    {
        await using var db = host.CreateDbContext();
        var charge = new Charge(fund.Id, null, "Quỹ tháng 11", null, 50_000, new DateOnly(2026, 11, 30), ChargeKind.Regular, owner.Id, Now);
        var assignment = new ChargeAssignment(charge.Id, payer.Id, 50_000);
        var payment = new Payment(assignment.Id, payer.Id, 50_000, PaymentMethod.BankTransfer, Now);
        db.Charges.Add(charge);
        db.ChargeAssignments.Add(assignment);
        db.Payments.Add(payment);
        var attachment = await db.Attachments.SingleAsync(item => item.Id == attachmentId);
        attachment.OwnerId = payment.Id;
        await db.SaveChangesAsync();
    }
}
