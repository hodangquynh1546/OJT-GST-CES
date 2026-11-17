# Kiến trúc tổng quan

> Tech stack, phiên bản và cấu trúc project theo `training-doc.md` (tài liệu chuẩn – không thay đổi khi chưa hỏi).

## 1. Sơ đồ

```text
Trình duyệt ── ClassFundUI (Angular 20 + Kendo 23.3) ── /api/v1 ──► ClassFund.Api (ASP.NET Core, phục vụ luôn SPA)
                                                                     │  Controller · Validation · AuthN/AuthZ · Middleware
                                                                     ▼
                                                     ClassFund.Application  ◄── DI ── ClassFund.Infrastructure
                                                     Service · DTO · Port           EF Core · Npgsql · Storage
                                                                     ▼                       │
                                                         ClassFund.Domain                    ▼
                                                     Entity · Enum · Rule        Neon PostgreSQL (dùng chung)
```

| Project | Chứa | Phụ thuộc |
|---|---|---|
| `ClassFund.Domain` | Entity, enum, `Permissions`/`RolePermissions`, rule thuần (`EvidenceRule`, `FileRule`, `WithdrawalRule`, `YearEndRule`, `EventStatusRules`, `PaymentConfirmationPolicy`…) | Không |
| `ClassFund.Application` | `Common/` (port & dịch vụ dùng chung), `Features/<Module>/` (service, DTO) | Domain |
| `ClassFund.Infrastructure` | `AppDbContext`, Configurations, `AppDb` (cài `IAppDb`), password hasher, session store, file storage, seeder | Application, Domain |
| `ClassFund.Api` | Pipeline, filter, authorization, `Features/<Module>/` (controller, request + validator), host SPA | Application, Infrastructure (chỉ DI) |

Quy tắc phụ thuộc được kiểm tra tự động bởi `tests/ClassFund.UnitTests/Architecture/DependencyRuleTests.cs`.

## 2. Thành phần Foundation (đã có – module dùng lại, không viết lại)

### Backend

| Thành phần | Vị trí | Vai trò |
|---|---|---|
| Pipeline | `Api/Hosting/PipelineRegistration.cs` | CorrelationId → Serilog → ExceptionHandler → Swagger (dev) → static/SPA → RateLimiter → Authentication → CurrentUser → Authorization → Controllers → fallback 404 API |
| Đăng ký service | `Api/Hosting/ServiceRegistration.cs` | Serilog JSON, Infrastructure, controllers + filter mặc định, API versioning, Swagger, antiforgery, cookie auth, rate limit |
| Tự đăng ký module | `Api/Hosting/FeatureRegistration.cs` | Class `*Service` trong `ClassFund.Application.Features.*` → scoped, không sửa file DI |
| Phiên | `Authorization/SessionCookie.cs`, `SessionClaims.cs`, `Middleware/CurrentUserMiddleware.cs`, `Infrastructure/Security/IdentityStore.cs` | Cookie `cf_session` (HttpOnly, SameSite=Strict, 8h trượt) chỉ chứa UserId + SecurityStamp; mỗi request đối chiếu DB |
| Phân quyền | `Authorization/RequirePermissionAttribute.cs`, `PermissionAuthorizationHandler.cs`, `Application/Common/Security/AccessService.cs` | Permission theo quỹ từ route `{fundId}`; 404/403 đúng thứ tự |
| Mật khẩu tạm | `Filters/PasswordChangeRequiredFilter.cs`, `AllowTemporaryPasswordAttribute.cs` | Chặn mọi endpoint (403 `password.change_required`) trừ endpoint được đánh dấu |
| CSRF | `Filters/AntiforgeryFilter.cs`, `Common/AntiforgeryController.cs` | Header `X-XSRF-TOKEN` bắt buộc cho request ghi |
| Validation | `Filters/ValidationFilter.cs` | FluentValidation → 400 `validation_failed` + `errors` theo ô |
| Lỗi | `ErrorHandling/GlobalExceptionHandler.cs`, `ApiProblems.cs` | Exception → ProblemDetails (`code`, `title`, `traceId`, `correlationId`) |
| Phân trang | `Api/Common/PageRequest.cs`, `Application/Common/Paging/PagedResult.cs` | `?page=&pageSize=` (≤ 100) |
| Dữ liệu | `Application/Common/Persistence/IAppDb.cs` | `Query<T>`, `Add`, async helpers, `LockAsync` (FOR UPDATE), `InTransactionAsync` (retry-safe) |
| Audit | `Application/Common/Auditing/AuditTrail.cs` | `Record(AuditEntry)` trong cùng SaveChanges |
| Thông báo | `Application/Common/Notifications/Notifier.cs` | `Send(recipients, NotificationMessage)` + tiền tố phạm vi |
| File (chứng từ, ảnh đại diện, tài liệu) | `Application/Common/Storage/IFileStorage.cs` (+ `StorageKeys`), `Infrastructure/Storage/LocalFileStorage.cs`, `StorageOptions.cs` | Lưu file thực tế trên Neon Object Storage (adapter `AWSSDK.S3`: task M5-T0); `Local` cho test |
| Mật khẩu | `Infrastructure/Security/AspNetPasswordHasher.cs`, `Application/Common/Security/Credentials.cs` | PBKDF2 + pepper; sinh mật khẩu tạm |
| Health | `GET /api/health` | Kiểm tra kết nối DB |
| Seed | `Infrastructure/Persistence/Seeding/DatabaseSeeder.cs` | `-- --seed` |

### Frontend

| Thành phần | Vị trí | Vai trò |
|---|---|---|
| Khởi động | `app.config.ts` | Locale vi, router (component input binding), interceptor, khôi phục phiên |
| Route | `app.routes.ts` + `features/<module>/<module>.routes.ts` | Bản đồ route có guard theo permission; module chỉ sửa file routes của mình |
| Phiên | `core/auth/auth.service.ts`, `auth.guards.ts`, `permissions.ts` | `user()` signal, `membership(fundId)`, guard 401/404/403 |
| Interceptor | `core/interceptors/` | Gắn `X-XSRF-TOKEN`; chuẩn hóa lỗi → `ApiError`; 401 → `/login`; mật khẩu tạm → `/change-password` |
| Shell | `core/navigation/` | Header, menu sinh từ permission (`nav-items.ts`), vùng nội dung |
| Quỹ hiện tại | `core/services/current-fund.service.ts` | `fundId`, `permissions`, `can()` từ URL |
| Dùng chung | `shared/` | `app-state`, `page-header`, `coming-soon`, `scope-badge`, `*appHasPermission`, pipe `vndMoney`/`vnDate`, `LoadState`, `applyServerErrors`, `registerErrorMessages`, `ConfirmDialog`, `AppNotifications` |

## 3. Luồng request mẫu

`POST /api/v1/funds/{fundId}/payments/{id}/confirm`

1. `CorrelationIdMiddleware` gắn mã; Serilog ghi log có mã.
2. Cookie → `CurrentUserMiddleware` kiểm SecurityStamp.
3. `[RequirePermission(Permissions.PaymentConfirm)]` → `AccessService` đọc `FundMember` → 404/403/OK.
4. `PasswordChangeRequiredFilter`, `AntiforgeryFilter`, `ValidationFilter`.
5. Controller gọi `IPaymentsService.ConfirmAsync` → trong `InTransactionAsync`: khóa Payment, `PaymentConfirmationPolicy.Decide`, cập nhật, thêm `LedgerEntry`, `AuditTrail.Record`, `Notifier.Send`, `SaveChangesAsync`.
6. DTO → JSON; exception → ProblemDetails.
