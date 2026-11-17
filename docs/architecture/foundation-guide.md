# Hướng dẫn làm một module (FE → BE → DB → Test)

> Mỗi task trong `docs/implementation/module-plan.md` đi trọn luồng theo các bước dưới. Quy tắc code chi tiết: `coding-standards.md`.

## 0. Trước khi bắt đầu

1. Đọc FR / BR / OQ liên quan (mã ghi trong task). OQ chưa chốt → hỏi, không tự quyết.
2. `git checkout main && git pull` → `git checkout -b feature/<mã-module>-<tên-ngắn>` (ví dụ `feature/m6-payments`).
3. Nếu cần đổi schema: làm theo `docs/implementation/neon-database-guide.md` mục 4 **trước** khi code nghiệp vụ.

## 1. Backend

### 1.1. Application – `src/ClassFund.Application/Features/<Module>/`

| File | Nội dung |
|---|---|
| `I<Module>Service.cs` | Interface, mỗi use case một method `...Async(..., CancellationToken)` |
| `<Module>Service.cs` | Cài đặt (tự đăng ký DI vì tên kết thúc `Service`) |
| `<Module>Models.cs` | DTO request/response (record), không trả entity ra ngoài |
| `<Module>Errors.cs` | Hằng mã lỗi `"<module>.<lỗi>"` + thông báo tiếng Việt |
| `<Module>AuditActions.cs` | Hằng mã audit `"<module>.<hành động>"` |

Mẫu một thao tác ghi:

```csharp
public Task<PaymentDto> ConfirmAsync(Guid fundId, Guid paymentId, CancellationToken ct) =>
    db.InTransactionAsync(async token =>
    {
        var actorId = currentUser.UserId!.Value;
        var payment = await db.LockAsync<Payment>(paymentId, token) ?? throw new NotFoundException();
        // ... kiểm tra payment thuộc fundId (không thì NotFound), trạng thái (ConflictException), rule Domain
        var access = await accessService.GetFundAccessAsync(actorId, fundId, token);
        var decision = PaymentConfirmationPolicy.Decide(actorId, payment.PayerUserId, access.Permissions.Contains(Permissions.PaymentConfirm));
        // ... cập nhật entity, db.Add(new LedgerEntry(...))
        audit.Record(new AuditEntry(PaymentsAuditActions.Confirm, fundId, actorId, scope, eventId, nameof(Payment), payment.Id,
            Before: new { status = "Submitted" }, After: new { status = "Confirmed", payment.Amount, payment.Method },
            IsSelfAction: decision.Mode == ConfirmationMode.SelfDeclared));
        notifier.Send([payment.PayerUserId], new NotificationMessage(...));
        await db.SaveChangesAsync(token);
        return ToDto(payment);
    }, ct);
```

Bắt buộc:

- Kiểm tra entity thuộc `fundId` của route (chống truy cập chéo quỹ) → không thuộc thì `NotFoundException`.
- Lỗi nghiệp vụ: `BusinessRuleException` (422), xung đột trạng thái: `ConflictException` (409), không tồn tại / không thuộc phạm vi: `NotFoundException` (404), quyền theo dữ liệu: `ForbiddenAccessException` (403).
- Ghi nhiều bảng → `InTransactionAsync`; trong đó **chỉ** thao tác DB (có thể chạy lại khi lỗi tạm thời).
- Bắt `UniqueConstraintException` khi có ràng buộc unique có thể đua (ví dụ email) → đổi thành `ConflictException`.
- Thời gian lấy từ `TimeProvider` (inject), ngày nghiệp vụ dùng `VietnamTime.DateOf`.

### 1.2. Api – `src/ClassFund.Api/Features/<Module>/`

```csharp
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/payments")]
public sealed class PaymentsController(IPaymentsService service) : ApiControllerBase
{
    [HttpPost("{paymentId:guid}/confirm")]
    [RequirePermission(Permissions.PaymentConfirm)]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    public Task<PaymentDto> Confirm(Guid fundId, Guid paymentId, CancellationToken ct) => service.ConfirmAsync(fundId, paymentId, ct);
}
```

- Request + `AbstractValidator<T>` trong `<Module>Requests.cs` (validator tự đăng ký).
- Không logic nghiệp vụ trong controller.

### 1.3. Database

- Thêm/sửa entity ở `Domain/Entities`, cấu hình ở `Infrastructure/Persistence/Configurations` (cập nhật `database-design.md`).
- Migration theo quy trình Neon. Một PR = model + migration + tài liệu.

## 2. Frontend – `src/app/features/<module>/`

| File | Nội dung |
|---|---|
| `<module>.routes.ts` | Thay `comingSoon(...)` bằng component thật (lazy) |
| `<module>.models.ts` | Interface khớp DTO backend |
| `<module>.api.ts` | Service gọi API qua `fundApiUrl(fundId, '/...')` |
| `<module>.errors.ts` | `registerErrorMessages({ 'payment.already_confirmed': '…' })` |
| `<screen>/<screen>.ts` | Standalone component, `ChangeDetectionStrategy.OnPush`, signal + `LoadState` |

- Trạng thái màn hình: `toSignal(toLoadState(api.list()), { initialValue: LOADING })` + `<app-state [kind]="stateKind(state())">`.
- Form: Reactive Forms + Kendo; lỗi server: `applyServerErrors(form, error, fieldFor)`.
- Hành động phá hủy: `ConfirmDialog`. Thành công: `AppNotifications.success`.
- Nút/khu vực theo quyền: `*appHasPermission="Permissions.X"`.
- Mọi giao dịch, khoản thu, thông báo hiển thị `<app-scope-badge>` (Quỹ chung / Sự kiện).
- Tiền: pipe `vndMoney`; ngày: `vnDate`.

## 3. Test (bắt buộc trước khi mở PR)

| Loại | Vị trí | Tối thiểu |
|---|---|---|
| Unit – Domain | `tests/ClassFund.UnitTests/Domain/` | Mỗi rule mới |
| Unit – Service | `tests/ClassFund.UnitTests/Application/` (dùng `FakeAppDb`) | Nhánh chính + lỗi + quyền theo dữ liệu (AZ-xx) |
| Integration – API | `tests/ClassFund.IntegrationTests/<Module>/` (`PostgresFixture`, WebApplicationFactory) | Mỗi endpoint: 200/201, 400, 401, 403, 404 |
| FE unit | `*.spec.ts` cạnh file | Logic thuần (validator, mapper), service API |
| E2E | `ClassFundUI/e2e/mock/<module>.spec.ts` | Luồng chính của màn hình |

Lệnh: `dotnet test` (ClassFundAPI), `npx ng test --watch=false --browsers=ChromeHeadless`, `npx ng build`, `npm run e2e` (ClassFundUI).

## 4. Hoàn tất (Definition of Done)

- [ ] Build backend không warning (`TreatWarningsAsErrors`), FE build OK.
- [ ] Toàn bộ test xanh.
- [ ] Mọi thao tác ghi có audit; thông báo đúng người nhận và có phạm vi.
- [ ] Tài liệu cập nhật: API catalogue (`docs/api/api-catalogue.md`), trạng thái (`docs/implementation/implementation-status.md`), test case (`docs/testing/test-cases.md`), DB nếu đổi.
- [ ] PR mô tả FR/BR đã làm, ảnh màn hình; người còn lại review.
