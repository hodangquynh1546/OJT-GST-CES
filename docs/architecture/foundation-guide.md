# Foundation Guide – làm một module từ FE → BE → DB → Test

> v1.0 · 2026-11-09 · Áp dụng cho mọi module trong [module-plan.md](../implementation/module-plan.md). Kiến trúc và phiên bản theo `training-doc.md` (không đổi).
> Chuẩn tài liệu (bản đồ, thứ tự ưu tiên, mã, tài liệu cần cập nhật khi xong module): [docs/README.md](../README.md) · Quy ước API: [api-conventions.md](../api/api-conventions.md) · Design System: [design-system.md](../ui-design/design-system.md) · Màn hình: [screen-specifications.md](../ui-design/screen-specifications.md) + Canvas v4.0.

## 1. Foundation đã có

### Backend (`ClassFundAPI/`)
| Phần | Vị trí | Ghi chú |
|---|---|---|
| Host, DI, pipeline | `Api/Hosting/` | `ServiceRegistration`, `PipelineRegistration`. **Module không sửa.** Service tự đăng ký (`FeatureRegistration`), validator tự đăng ký |
| Controller nền | `Api/Common/` | `ApiControllerBase`, `ApiRoutes`, `PageRequest` + `ValidatePaging()`, `AntiforgeryController` (`GET /auth/antiforgery`) |
| Lỗi | `Api/ErrorHandling/` | `GlobalExceptionHandler` + `ApiProblems` (ProblemDetails có `code`, `traceId`, `correlationId`) |
| Filter | `Api/Filters/` | `ValidationFilter` (400), `AntiforgeryFilter`, `PasswordChangeRequiredFilter` |
| Xác thực / phân quyền | `Api/Authorization/`, `Api/Middleware/` | Cookie phiên, `SessionCookie.SignInAsync/SignOutAsync`, `CurrentUserMiddleware`, `[RequirePermission]`, `[AllowTemporaryPassword]` |
| Application chung | `Application/Common/` | Exception (`NotFound`, `Forbidden`, `Conflict`, `AuthenticationFailed`), `IAppDb`, `PagedResult<T>`, `AuditTrail`, `IAccessService` / `AccessService` (vai trò + permission theo quỹ, phạm vi xem), `ICurrentUser`, `IPasswordHasher`, `Credentials` (mật khẩu tạm, gợi ý username), `SessionTicket` |
| Domain | `Domain/` | Toàn bộ entity, enum, rule (`PasswordPolicy`, `LoginName`, `DefaultCategories`, `VietnamTime`), `Permissions`, `RolePermissions`, `BusinessRuleException` |
| DB | `Infrastructure/Persistence/` | `AppDbContext`, cấu hình 8 bảng, 2 migration (đầy đủ schema), `IdentityStore`, `AspNetPasswordHasher` |
| Test nền | `tests/` | Unit: middleware, filter, phân quyền, `AccessService`, Domain, kiến trúc. Integration: `PostgresFixture` (Testcontainers), `ApiClient` (cookie + antiforgery), `TestData` (seed thẳng DB), `ProbeController` + `SignInAsAsync()` (đăng nhập nhanh chỉ trong test), `FoundationTests`, `ApiContractTests` |

### Frontend (`ClassFundUI/`)
| Phần | Vị trí | Ghi chú |
|---|---|---|
| Khung ứng dụng | `core/navigation/` | `AppShell`, `AppHeader`, `AppNav` (menu theo permission – `nav-items.ts`) |
| Phiên / quyền | `core/auth/` | `AuthService` (`user`, `membership`, `homeUrl`, `restoreSession`), guard (`authGuard`, `guestGuard`, `homeGuard`, `fundPermissionGuard`), `Permissions` |
| HTTP | `core/config/api.config.ts`, `core/interceptors/` | `apiUrl()`, `fundApiUrl(fundId, path)`; antiforgery + `ApiError` + xử lý 401 / mật khẩu tạm |
| Service chung | `core/services/` | `CurrentFund` (quỹ đang mở, `can()`), `AppNotifications` (toast), `ConfirmDialog`, `AntiforgeryService` |
| Component chung | `shared/components/` | `app-state` (loading / empty / error / 403 / 404), `app-page-header`, `coming-soon` (giữ chỗ) |
| Form | `shared/forms/server-errors.ts` | `applyServerErrors(form, error, fieldFor)`, `serverError(control)` |
| State | `shared/state/load-state.ts` | `LoadState<T>`, `toLoadState()`, `stateKind()` |
| Tiện ích | `shared/utils/`, `shared/pipes/`, `shared/directives/` | `toApiDate` / `fromApiDate` / `todayApiDate`, `toHttpParams`, `registerErrorMessages`, `vndMoney`, `vnDate`, `*appHasPermission` |
| Style | `src/styles.scss` | Token `--cf-*` ánh xạ sang `--kendo-*`; class `.cf-card`, `.cf-tag(--success/--warning/--danger)`, `.cf-alert(--warning/--danger)`, `.cf-money`, `.cf-income`, `.cf-expense`, `.cf-kpis`, `.cf-page(--narrow)` |
| E2E | `e2e/support/`, `e2e/mock/`, `e2e/full/` | Xem mục 4 |

## 2. Cấu trúc một module

```text
ClassFundAPI/src/ClassFund.Api/Features/<Module>/
  <Module>Controller.cs        # kế thừa ApiControllerBase, [ApiVersion(1)], [Route(ApiRoutes.Fund + "/…")], [RequirePermission]
  <Module>Requests.cs          # record request + AbstractValidator (tự đăng ký)
ClassFundAPI/src/ClassFund.Application/Features/<Module>/
  I<Module>Service.cs, <Module>Service.cs   # tên kết thúc "Service" → tự đăng ký DI (scoped)
  <Module>Models.cs            # DTO trả về
  <Module>Errors.cs            # hằng số mã lỗi "<module>.<lỗi>" + thông báo
  <Module>AuditActions.cs      # hằng số "<Entity>.<Action>" cho AuditTrail
ClassFundAPI/tests/ClassFund.UnitTests/Application/<Module>ServiceTests.cs
ClassFundAPI/tests/ClassFund.IntegrationTests/<Module>/<Module>Tests.cs
ClassFundAPI/tests/ClassFund.IntegrationTests/Contracts/snapshots/<Tag>.json     # tự sinh, commit cùng PR

ClassFundUI/src/app/features/<module>/
  <module>.routes.ts           # đã có sẵn (giữ chỗ) – thay comingSoon bằng màn hình thật
  <module>.api.ts              # service gọi API: HttpClient + fundApiUrl + toHttpParams
  <module>.models.ts           # interface khớp DTO backend (tên trường y hệt Swagger)
  <module>.errors.ts           # registerErrorMessages({...}) – import trong routes để chạy khi lazy-load
  <screen>/<screen>.ts (+ .spec.ts)   # component standalone, OnPush, signals
ClassFundUI/e2e/mock/<module>.spec.ts    # UI với API giả lập
ClassFundUI/e2e/full/<module>.spec.ts    # luồng thật FE + BE + DB
```

**Quy tắc code:**
- **Backend:**
  - Controller mỏng, chỉ map request → service → response.
  - Quy tắc nghiệp vụ đặt ở Domain (entity / rule) hoặc service.
  - Service kiểm tra phạm vi qua `IAccessService` và ném exception chung; không tự tạo ProblemDetails.
- **Frontend:**
  - Component standalone, `ChangeDetectionStrategy.OnPush`.
  - State bằng `signal` / `computed`; dữ liệu tải về đi qua `toSignal(toLoadState(api.x()), { initialValue: LOADING })`.
  - Không dùng store toàn cục, không thêm thư viện state.
  - Hiển thị trạng thái bằng `<app-state [kind]="…">`.
  - Màu và chữ chỉ dùng token, không viết mã màu.
  - Kendo theo bảng ánh xạ ở design-system.md mục 6.

## 3. Quy trình làm một module (Full Flow)

1. **Đọc:**
   - Mục của module trong [module-plan.md](../implementation/module-plan.md).
   - Board Canvas.
   - Bảng màn hình trong screen-specifications.md.
   - Đặc tả endpoint trong backend-api.md.
   - Business rule và test case liên quan.
2. **Chốt hợp đồng API trước:** viết request / DTO / controller, chạy `UPDATE_API_CONTRACTS=1 dotnet test --filter ApiContractTests`, review file snapshot. Frontend viết `<module>.models.ts` theo snapshot này.
3. **FE:** dựng màn hình theo Canvas, dùng API giả lập (`e2e/mock`), xử lý đủ trạng thái (loading / empty / error / 403 / 404 / lỗi từng ô).
4. **BE:** service + Domain rule, unit test cho rule; integration test cho endpoint (status, `code`, phạm vi theo từng vai trò, không lộ trường nội bộ).
5. **DB:** dùng schema có sẵn. Chỉ thêm migration khi bắt buộc (api-conventions.md mục 9).
6. **E2E full:** luồng thật với các vai trò liên quan (`e2e/full/<module>.spec.ts`), seed bằng API của module đã xong (`RealApi`).
7. **Hoàn thành (Definition of Done):**
   - Màn hình đúng Canvas.
   - API đúng quy ước, snapshot đã review.
   - Unit, integration, e2e mock, e2e full đều đạt.
   - Build 0 cảnh báo (`TreatWarningsAsErrors`).
   - Cập nhật tài liệu theo [docs/README.md](../README.md) mục 6 (implementation-status, test-cases, functional-requirements, backend-api nếu đổi).
   - Mô tả PR dùng mẫu checklist trong [backend-checklist.md](../implementation/backend-checklist.md).
   - Có người còn lại review chéo.

## 4. Test và môi trường

| Lớp | Lệnh | Cần |
|---|---|---|
| Backend unit | `cd ClassFundAPI && dotnet test tests/ClassFund.UnitTests` | .NET 10 |
| Backend integration + hợp đồng | `dotnet test` (cả solution) | Docker đang chạy (Testcontainers tự dựng PostgreSQL) |
| Cập nhật snapshot hợp đồng | `UPDATE_API_CONTRACTS=1 dotnet test --filter ApiContractTests` | như trên |
| Frontend unit | `cd ClassFundUI && npm test` | Chrome |
| E2E mock | `npm run e2e` | Chrome; tự chạy `ng serve` |
| E2E full | `docker compose up -d --wait` → `set -a && source .env && set +a` → `dotnet ef database update …` (trong `ClassFundAPI`), rồi `cd ClassFundUI && npm run e2e:full` | Như README "Chạy dự án"; Playwright tự chạy `dotnet run` + `ng serve` |

- **Seed trong integration test:**
  - `TestData` ghi thẳng DB (quỹ, học sinh có tài khoản, phụ huynh, thủ quỹ).
  - `client.SignInAsAsync(userId)` đăng nhập không cần API Auth.
  - Nhờ vậy module của đợt sau không phải chờ màn hình của module khác.
- **Seed trong e2e full:** chỉ dùng API thật đã có (`RealApi` – đăng ký GVCN → tạo quỹ → …). E2E full của một module chạy được khi các module nó phụ thuộc đã merge.
- **Dữ liệu:** dùng `unique()` / `TestData.Unique()` cho email, tên. Không phụ thuộc thứ tự test, không xóa dữ liệu.

## 5. Quy tắc tránh conflict (2 người song song)

| File / vùng | Quy tắc |
|---|---|
| `features/<module>/`, `Features/<Module>/`, test của module | Chỉ người làm module đó sửa |
| `ServiceRegistration.cs`, `app.routes.ts`, `nav-items.ts`, `app.config.ts`, `styles.scss` | **Foundation – module không sửa.** Route, menu và DI đã có chỗ sẵn cho mọi module |
| `shared/`, `core/`, `Application/Common/`, `Api/Common/`, `Domain/` | Muốn sửa: tách **PR Foundation nhỏ riêng**, merge trước rồi rebase. Ngoại lệ: M1 Auth được mở rộng `core/auth/auth.service.ts` (login / logout / đổi mật khẩu) |
| Mã lỗi, audit action, thông báo lỗi FE | Mỗi module một file riêng (`<Module>Errors.cs`, `<module>.errors.ts`) |
| Snapshot hợp đồng | Mỗi tag Swagger (controller) một file |
| E2E | Mỗi module một file `e2e/mock/<module>.spec.ts`, `e2e/full/<module>.spec.ts`; helper chung ở `e2e/support/` (sửa = PR Foundation) |
| Migration | Một migration một lần, rebase trước khi tạo; báo nhóm khi bắt đầu |
| Docs | Mỗi module chỉ sửa dòng / mục của mình trong `implementation-status.md`, `test-cases.md` |

**Git:**
- Nhánh `feature/<mã module>-<tên>` (ví dụ `feature/m3-students`), tách từ `main` mới nhất.
- Rebase `main` trước khi mở PR.
- Mỗi PR một module (hoặc một PR Foundation).
- Người còn lại review chéo.
