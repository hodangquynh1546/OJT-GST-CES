# Backend Technical Decisions

> 2026-11-05: mã nguồn đã xóa (D-35). Các TD dưới đây là quyết định kỹ thuật dùng cho lần triển khai lại; tên lớp / file chỉ là tham chiếu lịch sử. Trạng thái từng feature: [implementation-status.md](implementation-status.md). *2026-11-06:* TD-93 → TD-96 (Thu chi, Dev B); TD-97 → TD-105 (Auth, Fund Lifecycle), TD-106 (Học sinh & phụ huynh), TD-107 → TD-110 (Tổng quan, Báo cáo, Thành viên – Dev B), TD-111 (Phân công thủ quỹ) – Implemented.

> Cập nhật 2026-11-04 (sau Review & Simplify). Quyết định kỹ thuật của phạm vi cũ (TD-40 → TD-55, DV-01 → DV-07) không còn áp dụng – Git history.

## Giữ từ M0
| Mã | Quyết định |
|---|---|
| TD-01 → TD-31 | Cấu trúc solution, build props, dependency rule (có test), ProblemDetails, validation ở Api (FluentValidation), Correlation ID, Serilog, versioning URL, Swagger (Development), health check, 2 tài khoản DB, Testcontainers |

## Quyết định hiện hành
| Mã | Quyết định | Lý do |
|---|---|---|
| TD-60 | Domain chỉ còn `User`, `Fund`, `Category`, `FundTransaction`, `AuditLog`; một bảng giao dịch có cột `Type` | Bài toán thủ quỹ một lớp |
| TD-61 | Số dư và mọi số liệu báo cáo tính bằng truy vấn tổng trên giao dịch chưa hủy; không bảng tổng hợp, không khóa dòng | Không thể lệch; đủ nhanh cho quy mô một lớp |
| TD-62 | Hủy = cờ `IsCancelled`; trigger DB chặn DELETE; nhật ký `AuditLog` append-only lưu giá trị trước / sau | Không mất dữ liệu, truy vết sửa / hủy |
| TD-63 | FK ghép (CategoryId, FundId, Type) → Category | DB chặn danh mục sai loại / sai quỹ |
| TD-64 | `idempotencyKey` bắt buộc khi thêm; trùng (tuần tự hoặc đồng thời) → trả giao dịch đã lưu (200, `replayed`) | Nhấn Lưu nhiều lần |
| TD-65 | Phạm vi dữ liệu qua `FundContext` (quỹ của người đăng nhập); client không gửi mã quỹ; quỹ khác → 404 | Không IDOR |
| TD-66 | `[RequirePermission]` + 5 permission cố định trong code cho thủ quỹ; không bảng vai trò | Giữ chuẩn training-doc, không thêm khái niệm |
| TD-67 | Tài khoản tạo / đặt lại mật khẩu / khóa bằng lệnh `dotnet run -- <lệnh>` đọc biến môi trường | D-02; không lộ mật khẩu trong source |
| TD-68 | Cookie session + antiforgery + rate limit đăng nhập (giữ) | Bảo mật cơ bản |
| TD-69 | Một migration baseline `InitialClassFund`; 14 migration cũ (chưa phát hành) đã xóa | D-08 |
| TD-70 | Giữ port `IAppDb` (IQueryable + thực thi async) nhưng bỏ khóa dòng | Application không phụ thuộc EF (dependency rule M0) |

## Dự kiến cho R2 (đã thiết kế, CHƯA triển khai – 2026-11-04)
| Mã | Quyết định | Thay thế |
|---|---|---|
| TD-71 | `FundMember` (UserId UNIQUE, Role) + `RolePermissions` trong Domain; `CurrentMember` scoped đọc vai trò từ DB mỗi request | TD-65 (`FundContext` theo owner), TD-66 (5 permission cố định) |
| TD-72 | 7 permission: Fund.View, Ledger.ViewFull, Ledger.ViewPublic, Transaction.Manage, Category.Manage, Fund.Manage, Member.Manage; `[RequirePermission]` hỗ trợ "một trong các" | TD-66 |
| TD-73 | Phạm vi công khai áp trong truy vấn Application (projection `PublicTransactionDto`), lọc trước phân trang / đếm | – |
| TD-74 | `MustChangePassword` → 403 `password.change_required` trừ me / change-password / logout | – |
| TD-75 | Thao tác thành viên (đổi vai trò, khóa) chạy SERIALIZABLE, đếm thủ quỹ hoạt động trong cùng giao dịch; retry 40001 đã có | – |
| TD-76 | `UpdatedBy` / `CancelledBy` cột nullable + CHECK `NOT VALID`; backfill từ AuditLog trong migration `AddFundMembers` | – |
| TD-77 | Mật khẩu tạm sinh bằng `RandomNumberGenerator`, chỉ trả trong response; không log body `/members*`, `/auth/*` | TD-67 vẫn giữ cho CLI |

TD-65, TD-66 giữ nguyên trong bảng trên để lưu lịch sử. **2026-11-05:** TD-71 → TD-77 (R2) **không triển khai** – thay bằng TD-80 → TD-88 (R3). TD-65, TD-66 thay bởi TD-81, TD-82.

| Mã | Quyết định (R3, 2026-11-05 – thiết kế kỹ thuật hiện hành; **Not Implemented**: mã nguồn đã xóa cùng ngày) | Thay |
|---|---|---|
| TD-80 | `User.Kind` (Teacher / Student / Parent) cố định; học sinh đăng nhập bằng `Username`, giáo viên / phụ huynh bằng email; `POST /auth/login { login }` | – |
| TD-81 | `AccessService` (scoped, nhớ trong request) suy ra vai trò trong quỹ từ `Fund.OwnerUserId` → `Student` + `TreasurerAssignment` → `Student` → `ParentLink`; `RolePermissions` (Domain) ánh xạ vai trò → permission | TD-65, TD-71 |
| TD-82 | 10 permission (`Fund.Create` cấp tài khoản; 9 permission theo quỹ); `PermissionAuthorizationHandler` đọc route `fundId`, ném `NotFoundException` / `ForbiddenAccessException(code)` → ProblemDetails qua `GlobalExceptionHandler`; service kiểm tra lại | TD-66, TD-72 |
| TD-83 | API theo quỹ `/funds/{fundId}/…`; bỏ `/fund`, `/transactions`, `/categories` (không có consumer) | – |
| TD-84 | Bản công khai: một `TransactionRow` + `ToDto(full)`; trường nội bộ null + `JsonIgnore(WhenWritingNull)`; lọc `IsPublic` trong `Visible(scope)` trước mọi điều kiện | TD-73 |
| TD-85 | Thu hồi / phân công thủ quỹ: `IAppDb.LockAsync<Fund>` (`SELECT … FOR UPDATE`) trong `InTransactionAsync` (Read Committed + retry); UNIQUE lọc chống phân công trùng | TD-75 |
| TD-86 | `Credentials.TemporaryPassword()` – `RandomNumberGenerator`, 16 ký tự không gây nhầm; `SuggestUsername` bỏ dấu; response có `Cache-Control: no-store`; AuditLog không chứa mật khẩu; request logging không ghi body | TD-77 |
| TD-87 | *(Không triển khai – thay bằng TD-91)* Migration `TeacherStudentParent` bổ sung: default `Kind = 'Teacher'` rồi DROP DEFAULT; backfill `UpdatedBy` / `CancelledBy` từ AuditLog (`DISTINCT ON … ORDER BY OccurredAt DESC`); NoDelete 3 bảng mới; không tạo phân công | TD-76 |
| TD-88 | CLI `create-teacher` (quỹ tùy chọn) thay `create-treasurer`; `SCHOOLFUND_LOGIN` (fallback `SCHOOLFUND_EMAIL`) | TD-67 (một phần) |
| TD-89 | `Entity<TId>` là lớp gốc; `Entity` = `Entity<Guid>` (UUID v7), `AuditLog : Entity<long>` để giữ `bigint identity` | – |
| TD-90 | `AccessService` nằm ở Application, dùng port `IAppDb`, nhận `userId` + `fundId`, ném `NotFoundException` khi không thuộc quỹ; đăng ký DI ở Infrastructure vì Application không có package DI. Chưa nhớ kết quả trong request như TD-81 (mỗi request hiện chỉ gọi một lần) | TD-81 (một phần) |
| TD-91 | Hai migration: `InitialClassFund` (toàn bộ schema 2.0) và `AddDatabaseProtections` (SQL tay: `UQ_Category_Fund_Type_Name`, `fn_block_delete`, `fn_block_audit_change`, trigger). `dotnet-ef` 10.0.4 khai báo ở `dotnet-tools.json`; `AppDbContextFactory` đọc `ConnectionStrings__Migrator` | TD-87 |
| TD-92 | `FundTransaction.IsPublic` dùng `HasDefaultValue(true).HasSentinel(true)`: nếu không, EF coi `false` là chưa gán và cột nhận `true`, biến khoản không công khai thành công khai | – |
| TD-93 | Sửa và hủy giao dịch: `IAppDb.LockAsync<T>` (`SELECT … FOR UPDATE`) trong `InTransactionAsync`, để sửa không ghi đè khoản vừa bị hủy và hai lệnh hủy không cùng lọt (NFR-09); sửa đồng thời vẫn là lần sau thắng (DD-06) | – |
| TD-94 | Enum trong API là chuỗi qua `JsonStringEnumConverter(allowIntegerValues: false)` đăng ký toàn cục | – |
| TD-95 | Ngày nghiệp vụ dùng UTC+7 cố định (`VietnamTime`), không phụ thuộc dữ liệu múi giờ của máy chủ. Vi phạm ngày tương lai là 422 `transaction.future_date` | – |
| TD-96 | Bản công khai của giao dịch và danh mục có truy vấn riêng, không chọn cột nội bộ; JSON bỏ trường null | – |

## Quyết định khi triển khai lại (2026-11-05 → 2026-11-06) – Implemented

| Mã | Quyết định | Lý do |
|---|---|---|
| TD-97 | Phiên: cookie `sf_session` (HttpOnly, SameSite Strict) chỉ chứa UserId + SecurityStamp; `IdentityStore` (Infrastructure) kiểm tra `Status = Active` và stamp mỗi request. `PasswordChangeRequiredFilter` toàn cục chặn tài khoản còn mật khẩu tạm (403 `password.change_required`), endpoint được phép đánh dấu `[AllowTemporaryPassword]`: `GET/PUT /auth/me`, `change-password`, `logout` | BR-17, BR-18; không phải lặp kiểm tra ở từng controller |
| TD-98 | Băm mật khẩu: `HMAC-SHA256(SECRET_KEY, mật khẩu)` rồi `PasswordHasher<User>` (PBKDF2, salt riêng). `SECRET_KEY` (≥ 32 ký tự) bắt buộc – thiếu thì API không khởi động. Hash cũ không pepper vẫn xác thực được và được băm lại ở lần đăng nhập đúng kế tiếp | Bổ sung D-06: lộ riêng DB không đủ để dò mật khẩu. Chưa hỗ trợ xoay khóa (KI-15) |
| TD-99 | Register / login / change-password trả `{ antiforgeryToken, user }`: token antiforgery gắn với danh tính nên phải cấp mới sau khi danh tính đổi. Mọi POST / PUT (kể cả login, register) cần header `X-XSRF-TOKEN` (chống CSRF và login CSRF). Swagger khai báo header này (nút Authorize) | Thay cho việc client phải gọi lại `/auth/antiforgery` |
| TD-100 | Rate limit fixed window 1 phút theo IP cho login / register (`RateLimiting:AuthPermitLimit`, mặc định 10) → 429 `auth.rate_limited` | TD-68 |
| TD-101 | Đăng nhập sai tài khoản và sai mật khẩu cùng trả 401 `auth.invalid_credentials`, kiểm tra hash giả khi không có tài khoản; `auth.account_disabled` chỉ khi đúng mật khẩu; AuditLog `Auth.SignInFailed` không lưu chuỗi đăng nhập | BR-26; không lộ tài khoản tồn tại |
| TD-102 | Project `SchoolFund.IntegrationTests`: `WebApplicationFactory` + Testcontainers PostgreSQL 16, áp migration thật; `ProbeController` chỉ có trong test để kiểm tra chặn mật khẩu tạm; `CapturedLogSink` (Serilog `ReadFrom.Services`) quét log không chứa bí mật | NFR-10; kiểm chứng trên DB thật |
| TD-103 | Dev DB: `docker-compose.yml` + `docker/postgres/init/01-roles.sh` tạo `migrator_user` (sở hữu schema) / `app_user` (SELECT, INSERT, UPDATE); tạo sẵn `__EFMigrationsHistory` và thu hồi quyền của `app_user`. Biến trong `.env` (không commit), mẫu `.env.example` | TD-01 → TD-31 (2 tài khoản DB), KI-10 |
| TD-104 | `GET /funds` dựa trên `IAccessService.GetMembershipsAsync` + 4 truy vấn gộp (ngày tạo, số dư, thủ quỹ, số học sinh) cho mọi quỹ một lần, không truy vấn theo từng quỹ. `studentCount` (GVCN), `linkedStudents` (phụ huynh) là `null` với vai trò khác và bị bỏ khỏi JSON (`JsonIgnore(WhenWritingNull)`) | FR-22; tránh N+1 |
| TD-105 | Danh mục mặc định là hằng số Domain (`Rules/DefaultCategories`), tạo cùng quỹ trong một lần `SaveChanges`; một dòng AuditLog `Fund.Create` liệt kê danh mục thay vì 6 dòng `Category.Create` | D-25, Q-09 |
| TD-106 | Học sinh & phụ huynh: thao tác tạo tài khoản / mật khẩu tạm (cấp tài khoản, cấp lại mật khẩu, liên kết phụ huynh) chạy trong `InTransactionAsync` + `LockAsync<Student>` – hai lệnh đồng thời không tạo hai tài khoản cho một học sinh; vi phạm UNIQUE (`UQ_User_Username`, `UQ_Student_UserId`, `UQ_ParentLink_Student_Active`) đổi thành 422 có mã. `Credentials` (Application): mật khẩu tạm 16 ký tự `RandomNumberGenerator` dạng `Xxxx-xxxx-0000-xxxx` không ký tự dễ nhầm; gợi ý tên đăng nhập `ten.ho.NNN` bỏ dấu, thử tối đa 20 lần nếu trùng. Định dạng tên đăng nhập kiểm tra ở Application để trả 422 `user.username` đúng đặc tả | TD-86, BR-29, NFR-09 |
| TD-107 | Báo cáo tháng: số tổng cộng từ chính các dòng `byCategory`, không truy vấn riêng, nên tổng các dòng luôn khớp số tổng (BR-15). Số dư đầu tháng là tổng mọi khoản chưa hủy trước ngày 1, không đọc lại báo cáo tháng trước, nên sửa / hủy khoản cũ không làm đứt chuỗi (BR-10) |
| TD-108 | Bản công khai: truy vấn theo danh mục chỉ lấy khoản công khai; khoản không công khai chỉ được `SUM` theo loại trong database, nên danh mục và số lượng của chúng không rời khỏi database (NFR-08). Danh mục có cả hai loại khoản: phần không công khai vào "Khoản khác" |
| TD-109 | `recentTransactions` và `transactions` của báo cáo dùng lại `TransactionService.ListAsync`, chỉ một nơi quyết định phạm vi công khai. Cả hai chỉ lấy khoản đang hiệu lực (FR-05, LEAK-09, LEAK-11); danh sách báo cáo không phân trang vì một tháng của quỹ lớp có ít khoản |
| TD-110 | `GET /funds/{fundId}/members` gồm cả học sinh chưa có tài khoản (vẫn là thành viên lớp), không gồm phụ huynh (FR-12). Thứ tự: GVCN, thủ quỹ, học sinh; họ tên học sinh lấy từ `Student.FullName` như `GET /funds` |
| TD-111 | Phân công / thu hồi thủ quỹ: `InTransactionAsync` + `LockAsync<Fund>` (`SELECT … FOR UPDATE` trên dòng quỹ) **trước** mọi truy vấn kiểm tra; Read Committed nên sau khi chờ được khóa, mỗi câu lệnh thấy dữ liệu đã commit của lệnh trước → hai lệnh thu hồi đồng thời không thể cùng thấy "còn ≥ 2 thủ quỹ". Thứ tự kiểm tra thu hồi: 404 (ngoài quỹ) → 409 `treasurer.already_revoked` → 422 `treasurer.last`; phân công: 404 → `already_assigned` → `no_account` → `account_disabled` → `account_not_activated`. "Học sinh đã kích hoạt" = `MustChangePassword = false` và `Status = Active`; "thủ quỹ cuối cùng" chỉ đếm phân công (mặc định PD-06). Test đồng thời được kiểm chứng bằng đột biến (bỏ khóa → thất bại) | TD-85, BR-32 |

## Đã loại bỏ
Mã: email sender, file storage, permission evaluator theo phạm vi, mọi service nghiệp vụ cũ.

Package: *(2026-11-05)* thêm `Testcontainers.PostgreSql` 4.16.0 và `Microsoft.AspNetCore.Mvc.Testing` 10.0.4 (chỉ project test), `FrameworkReference Microsoft.AspNetCore.App` cho Infrastructure (`PasswordHasher<TUser>`) – đã được duyệt.
