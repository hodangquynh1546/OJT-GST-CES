# Quy ước API chung (Backend ↔ Frontend)

> v1.1 · 2026-11-09 · Thuộc **Foundation**. Mọi module phải theo tài liệu này; muốn đổi quy ước thì làm PR Foundation riêng, không đổi trong PR module.
> Code tương ứng: Backend `ClassFund.Api/{Common,ErrorHandling,Filters,Authorization,Hosting}`; Frontend `core/` và `shared/`. Kiểm chứng bằng `tests/ClassFund.IntegrationTests/Foundation/FoundationTests.cs`.
> Đặc tả từng endpoint: [backend-api.md](backend-api.md). Cách thêm một module: [foundation-guide.md](../architecture/foundation-guide.md).

## 1. Nền tảng

| Mục | Quy ước |
|---|---|
| Prefix | `/api/v1/...` (Asp.Versioning, phiên bản trong URL). Hằng số `ApiRoutes.Prefix`, `ApiRoutes.Fund` |
| Controller | Kế thừa `ApiControllerBase`, khai báo `[ApiVersion(1)]` và `[Route(ApiRoutes.Fund + "/students")]` |
| Định dạng | Chỉ JSON (`application/json`), lỗi là `application/problem+json`. Tên thuộc tính **camelCase** |
| Route theo quỹ | `/funds/{fundId}/…`. `fundId` chỉ chọn quỹ; vai trò do backend suy ra từ phiên + dữ liệu. Không bao giờ nhận `role`, `teacherId`, `userId` của người gọi từ client |
| Swagger | `/swagger` (Development). Swagger là hợp đồng, được chụp snapshot theo module: `tests/ClassFund.IntegrationTests/Contracts/snapshots/<Tag>.json` |
| Health | `GET /api/health`, ẩn danh: 200 `Healthy` / 503 |
| Route API không tồn tại | 404 ProblemDetails `not_found` (không trả trang SPA) |

## 2. Xác thực, phiên, antiforgery

| Mục | Quy ước |
|---|---|
| Phiên | Cookie `cf_session` (HttpOnly, SameSite=Strict, Secure ngoài Development, 8 giờ trượt). Cookie chỉ chứa `UserId` và `SecurityStamp`. Mỗi request kiểm tra lại tài khoản còn `Active` và stamp còn khớp, nếu không thì coi như chưa đăng nhập |
| Mặc định | **Mọi endpoint cần đăng nhập.** Endpoint công khai phải ghi rõ `[AllowAnonymous]` |
| Antiforgery | Mọi `POST` / `PUT` / `DELETE` gửi header `X-XSRF-TOKEN`. Token lấy từ `GET /auth/antiforgery`. Response nào đổi phiên (đăng nhập, đăng ký, đổi mật khẩu) phải trả `antiforgeryToken` mới. Frontend: `antiforgeryInterceptor` + `AntiforgeryService.set()` |
| Mật khẩu tạm | Tài khoản `mustChangePassword` gọi API → 403 `password.change_required`. Ngoại lệ: endpoint `[AllowAnonymous]` và endpoint có `[AllowTemporaryPassword]` (ví dụ `/auth/me`, `/auth/change-password`, `/auth/logout`) |
| Rate limit | Đăng nhập / đăng ký: `[EnableRateLimiting(RateLimitPolicies.Auth)]`, quá giới hạn → 429 `auth.rate_limited` |

## 3. Phân quyền

- Theo **permission**, không theo role: `[RequirePermission(Permissions.X)]` trên action theo quỹ. Bảng role → permission nằm ở `Domain/Authorization/RolePermissions.cs` và [roles-and-permissions.md](../business-analysis/roles-and-permissions.md).
- Thứ tự kiểm tra: chưa đăng nhập → 401. **Không thuộc quỹ** (hoặc `fundId` sai định dạng) → **404** `not_found`, để không lộ quỹ tồn tại. Thuộc quỹ nhưng thiếu permission → 403 `forbidden`.
- Quyền cấp tài khoản (không theo quỹ): `Fund.Create` (giáo viên).
- Service vẫn lọc dữ liệu theo phạm vi người xem qua `IAccessService`. Ví dụ bản công khai không trả trường nội bộ (NFR-08).
- Frontend chỉ ẩn / hiện theo `permissions` của quỹ đang mở (`*appHasPermission`, `fundPermissionGuard`), **không** tự suy luận quyền.

## 4. Request

| Mục | Quy ước |
|---|---|
| Body | `record` C# trong `Features/<Module>/<Module>Requests.cs`, kèm một `AbstractValidator<T>` (FluentValidation, tự đăng ký) |
| Lỗi validate | 400 `validation_failed`, `errors: { "<field camelCase>": ["thông báo tiếng Việt"] }`. JSON sai hoặc enum dạng số → cũng 400 `validation_failed` (không lộ lỗi parser) |
| Chuỗi | Service `Trim()` trước khi lưu. Độ dài tối đa theo [data-dictionary.md](../database/data-dictionary.md) |
| PUT | Gửi **đủ** các trường sửa được. Trường tùy chọn bỏ trống / `null` là xóa giá trị |
| Danh sách có phân trang | Request kế thừa `PageRequest` (`page` từ 1, mặc định 1; `pageSize` 1–100, mặc định 20). Validator gọi `this.ValidatePaging()` |
| Bộ lọc | Query string camelCase (`type`, `categoryId`, `from`, `to`, `search`, `status`). Frontend tạo bằng `toHttpParams()` (bỏ giá trị rỗng) |
| Chống ghi trùng | Lệnh tạo dễ bị bấm nhiều lần (giao dịch) nhận `idempotencyKey` (UUID do frontend sinh mỗi lần mở form). Gửi lại → 200 + `replayed: true` |

## 5. Response thành công

| Thao tác | Mã | Body |
|---|---|---|
| Đọc một / nhiều | 200 | DTO. Danh sách không phân trang là mảng JSON |
| Danh sách phân trang | 200 | `{ items, total, page, pageSize }` (`PagedResult<T>`). `total` đếm **sau** khi lọc theo phạm vi người xem |
| Tạo | 201 | `{ id }`, hoặc DTO khi frontend cần ngay (ví dụ giao dịch kèm `isNegativeBalance`) |
| Sửa | 204 | Không body, hoặc 200 + DTO khi có giá trị tính lại cần hiện ngay |
| Hành động (`POST …/{id}/cancel`, `/revoke`, `/reset-password`…) | 200 / 204 | 200 khi có dữ liệu trả về (mật khẩu tạm, số dư mới) |
| Xóa | – | **Không có DELETE.** Dùng hủy / thu hồi / gỡ liên kết (đánh dấu), NFR-05, NFR-12 |

## 6. Lỗi (ProblemDetails)

```json
{ "status": 422, "title": "Không thể thu hồi thủ quỹ cuối cùng.", "code": "treasurer.last",
  "traceId": "…", "correlationId": "…", "errors": { "name": ["…"] } }
```

| Mã HTTP | `code` | Ném ở backend | Frontend |
|---|---|---|---|
| 400 | `validation_failed` | FluentValidation / model binding | Gắn vào ô bằng `applyServerErrors()` |
| 400 | `antiforgery_failed` | `AntiforgeryFilter` | "Tải lại trang" |
| 401 | `unauthorized`, `auth.invalid_credentials`, `auth.account_disabled` | `AuthenticationFailedException` | Interceptor xóa phiên → `/login` (trừ `/auth/me`, `/auth/login`) |
| 403 | `forbidden`, `password.change_required` | `ForbiddenAccessException` / filter | `password.change_required` → `/change-password`; 403 khi trang đang mở → AppState "forbidden" |
| 404 | `not_found` | `NotFoundException` | AppState "not-found" |
| 409 | `<miền>.<lỗi>` | `ConflictException` (xung đột trạng thái, ví dụ `treasurer.already_revoked`) | Thông báo + tải lại dữ liệu |
| 422 | `<miền>.<lỗi>` | `BusinessRuleException` (vi phạm quy tắc nghiệp vụ) | Gắn vào ô (bảng `code → field`) hoặc hộp tóm tắt |
| 429 | `auth.rate_limited` | Rate limiter | Thông báo chờ 1 phút |
| 500 | `internal_error` | Mọi exception khác. Chỉ log, không trả chi tiết kỹ thuật | Thông báo chung |

- **Đặt tên mã lỗi:** `<miền>.<snake_case>`. Miền là khái niệm nghiệp vụ, không phải tên module: `auth`, `password`, `user`, `student`, `parent`, `parent_link`, `treasurer`, `category`, `transaction` (ví dụ `student.account_exists`, `transaction.future_date`). Mã chung không có miền: `validation_failed`, `antiforgery_failed`, `unauthorized`, `forbidden`, `not_found`, `internal_error`. Mã đã có trong [backend-api.md](backend-api.md) **giữ nguyên chính tả**. Mỗi module khai báo hằng số mã lỗi trong thư mục của mình (`Features/<Module>/<Module>Errors.cs`) và thêm thông báo cho frontend trong `features/<module>/<module>.errors.ts` (`registerErrorMessages`).
- `title` là câu tiếng Việt cho người dùng. Frontend ưu tiên thông báo đăng ký theo `code`, nếu không có thì dùng `title`.
- Danh sách mã lỗi từng module: [module-plan.md](../implementation/module-plan.md).

## 7. Kiểu dữ liệu và định dạng

| Loại | JSON | Backend | Frontend |
|---|---|---|---|
| Id | chuỗi UUID v7 | `Guid` (sinh ở ứng dụng, `Entity`) | `string` |
| Tiền | số nguyên VND (`1250000`) | `long`, > 0 khi nhập (BR-01) | `vndMoney` → `1.250.000 đ`, có dấu `+` / `−`. Nhập bằng `kendo-numerictextbox format="n0"` |
| Ngày nghiệp vụ | `"2026-11-09"` | `DateOnly`, theo **giờ Việt Nam** (`VietnamTime.DateOf`) | `toApiDate` / `fromApiDate` / `todayApiDate`; hiển thị `vnDate` → `09/11/2026` |
| Thời điểm | `"2026-11-09T03:15:00+00:00"` (ISO 8601, UTC) | `DateTimeOffset` lưu `timestamptz` UTC, lấy từ `TimeProvider` | `vnDate` (đổi sang giờ Việt Nam). Ngày giờ: `dd/MM/yyyy HH:mm` |
| Enum / trạng thái | chuỗi PascalCase (`Income`, `Active`, `PendingActivation`) | `JsonStringEnumConverter(allowIntegerValues: false)` | union type TypeScript cùng giá trị |
| Boolean | `isXxx`, `hasXxx` | `bool` | – |
| Không có giá trị | `null` (ví dụ `note` trống, `revokedAt` chưa thu hồi) | nullable reference type | `T \| null` |
| Trường theo vai trò | **Không có trong JSON** khi không áp dụng cho người xem (bản công khai không có `note`, `isPublic`, `cancelReason`, tên người thực hiện…; `studentCount` chỉ có với GVCN) | DTO riêng cho bản công khai (TD-96) hoặc `[JsonIgnore(Condition = WhenWritingNull)]` (TD-84, TD-104) | `?: T` |
| Tháng / năm báo cáo | `year` 1–9999, `month` 1–12 (query) | `int` | – |

Giá trị trạng thái:
- **Có sẵn trong Domain** (`ClassFund.Domain/Enums`, `Domain/Authorization`): `AccountStatus` của `User.Status` (`Active`, `Disabled`), `UserKind` (`Teacher`, `Student`, `Parent`), `FundRole` (`Teacher`, `Treasurer`, `Student`, `Parent`), `TransactionType` (`Income`, `Expense`).
- **Enum hiển thị, module tự khai báo trong Application** (giá trị cố định theo [backend-api.md](backend-api.md)):
  - Trạng thái tài khoản học sinh (M3): `None`, `PendingActivation`, `Active`, `Disabled`.
  - `treasurerStatus` (M2): `Unassigned`, `Assigned`.
  - Lọc trạng thái giao dịch (M6): `Active`, `Cancelled`, `All`.

Chi tiết cột: [data-dictionary.md](../database/data-dictionary.md).

## 8. Dữ liệu nhạy cảm và nhật ký

- Không ghi mật khẩu (kể cả mật khẩu tạm), hash, cookie, token vào log hay `AuditLog` (NFR-10).
- Response chứa mật khẩu tạm có `Cache-Control: no-store` (NFR-11). Mật khẩu tạm chỉ hiện một lần.
- Thao tác ghi gọi `AuditTrail.Record(action, …)` trong cùng lần `SaveChanges`. Tên hành động `<Entity>.<Action>` (ví dụ `Fund.Rename`), hằng số đặt trong module.
- Header `X-Correlation-Id` (client gửi hoặc server sinh) được trả lại và có trong log và ProblemDetails.

## 9. Quy ước database

| Mục | Quy ước |
|---|---|
| CSDL | PostgreSQL 16, EF Core 10 + Npgsql. Schema đầy đủ đã có trong Foundation (migration `InitialClassFund`, `AddDatabaseProtections`) – [database-design-overview.md](../database/database-design-overview.md) |
| Tên | Bảng / cột **PascalCase**, bảng số ít (`Student`). Ràng buộc: `PK_`, `FK_`, `UQ_<Bảng>_<Cột>`, `IX_<Bảng>_<Cột>`, `CK_<Bảng>_<Cột>`, `AK_` |
| Khóa | UUID v7 sinh ở ứng dụng (`ValueGeneratedNever`). Riêng `AuditLog` dùng `bigint identity` |
| Kiểu | Tiền `bigint` + CHECK > 0 · ngày nghiệp vụ `date` · thời điểm `timestamptz` UTC · enum `varchar(30)` + CHECK · chuỗi có `HasMaxLength` |
| Xóa | Không xóa cứng dữ liệu nghiệp vụ. Trigger `TR_NoDelete` chặn DELETE với `app_user` (NFR-12) |
| Cấu hình | Mỗi entity một `IEntityTypeConfiguration` trong `Infrastructure/Persistence/Configurations/` |
| Tài khoản DB | API dùng `app_user` (chỉ SELECT / INSERT / UPDATE). Migration dùng `migrator_user` |
| Truy vấn | Application dùng `IAppDb` (`Query<T>()`, `ToListAsync`…). Không dùng EF Core trực tiếp ở Api / Application |
| Migration mới | Chỉ khi module **thật sự** cần đổi schema: báo nhóm trước, rebase `main` mới nhất rồi mới `dotnet ef migrations add <Module><Thay đổi>`, merge **một migration một lần**. Không sửa migration đã merge |
