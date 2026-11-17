# Chiến lược kiểm thử

| Tầng | Công cụ | Vị trí | Chạy |
|---|---|---|---|
| Unit – Domain & Application | xUnit 2.9 + Moq, `FakeAppDb` | `ClassFundAPI/tests/ClassFund.UnitTests` | `dotnet test` |
| Kiến trúc | xUnit (reflection) | `UnitTests/Architecture` | `dotnet test` |
| Integration – API + DB | `WebApplicationFactory<Program>` + Testcontainers PostgreSQL (`PostgresFixture`) | `ClassFundAPI/tests/ClassFund.IntegrationTests/<Module>` | `dotnet test` (cần Docker) |
| FE unit | Jasmine + Karma (ChromeHeadless) | `*.spec.ts` | `npx ng test --watch=false --browsers=ChromeHeadless` |
| E2E | Playwright; `e2e/mock` (API giả bằng `page.route`), `e2e/full` (API thật) | `ClassFundUI/e2e` | `npm run e2e` |

## Nguyên tắc

- **Không** chạy test trên Neon dùng chung. Integration dùng container riêng, tự hủy.
- Mỗi endpoint mới: test thành công + 400 + 401 + 403 (thiếu quyền) + 404 (ngoài phạm vi / quỹ khác).
- Mỗi quy tắc `BR-xx` / `AZ-xx` có ít nhất một test, tên test hoặc comment ghi mã.
- Test tiền: kiểm tra tổng (ví dụ tổng chia tiền thừa = số thừa), không chỉ từng dòng.
- Test audit: thao tác ghi sinh đúng `Action`, `Scope`, `IsSelfAction`; không chứa mật khẩu.
- Test thông báo: đúng người nhận (đặc biệt khoản thu sự kiện chỉ người tham gia).

## Test có sẵn (Foundation)

| Nhóm | Số test | Nội dung |
|---|---|---|
| `DependencyRuleTests` | 4 | Clean Architecture |
| `RolePermissionsTests` | 4 | Ma trận quyền, Owner-only `Treasurer.Manage`, `Fund.Create` |
| `EvidenceRuleTests`, `PaymentConfirmationPolicyTests`, `EventStatusRulesTests`, `SmallRulesTests` | ~30 | BR-05, 06, 07, 12, 16, 20, 27, 28 |
| `AccessServiceTests`, `AuditAndNotifierTests`, `CredentialsTests` | 10 | 404 cho người ngoài, thành viên Removed, audit/notification |
| `PipelineSmokeTests` | 2 | Antiforgery, 404 problem+json, correlation id |
| FE specs | 36 | menu theo vai trò, interceptor, pipe, LoadState, lỗi form… |

Danh sách test case nghiệp vụ theo module: `test-cases.md`.
