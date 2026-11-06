# Backend Progress

> Cập nhật: 2026-11-06 · Nhánh `feature/devA/student-parent` (trên `main` đã có PR #5 → #8).

## Trạng thái
**Partially Implemented** – mã nguồn cũ đã xóa (D-35) và đang làm lại: đã có Codebase Foundation, Domain Entities, `AppDbContext`, `AccessService`, migration `InitialClassFund` + `AddDatabaseProtections` và các feature Backend **Auth & User Session**, **Danh mục** (Dev B), **Thu chi** (Dev B), **Fund Lifecycle**, **Học sinh & phụ huynh**. Chưa có Frontend nghiệp vụ. Các feature còn lại: xem [implementation-status.md](implementation-status.md) mục 1a.

## Feature đã triển khai

### Auth & User Session – Implemented (PR #5, 2026-11-05)
Yêu cầu: FR-20, FR-13, FR-21, BR-17, BR-18, BR-26, BR-28, NFR-10, NFR-11 · API: [backend-api.md](../api/backend-api.md) mục Auth.

| Hạng mục | Nội dung |
|---|---|
| Endpoint | `POST /auth/register-teacher` (201, luôn là `Teacher`, bỏ qua mọi trường vai trò), `POST /auth/login` (một ô `login` = email / username, không tham số vai trò; vẫn nhận `email` cũ), `POST /auth/logout`, `GET/PUT /auth/me`, `POST /auth/change-password`. Register / login / change-password trả `{ antiforgeryToken, user }` |
| Phiên | Cookie `sf_session` chỉ chứa UserId + SecurityStamp; `IdentityStore` (thay `NoIdentityStore`) kiểm tra trạng thái + stamp mỗi request; đổi mật khẩu / khóa tài khoản → phiên khác 401 |
| Mật khẩu tạm | `PasswordChangeRequiredFilter` toàn cục → 403 `password.change_required`, trừ `GET/PUT /auth/me`, `change-password`, `logout` (`[AllowTemporaryPassword]`) |
| Băm mật khẩu | `PasswordHasher<User>` (PBKDF2) + pepper `SECRET_KEY` (TD-98) |
| Lỗi | 401 `auth.invalid_credentials` / `auth.account_disabled` (chỉ khi đúng mật khẩu) · 422 `auth.password_policy`, `user.email_taken`, `auth.wrong_password`, `auth.password_reused` · 429 `auth.rate_limited` |
| AuditLog | `Account.RegisterTeacher`, `Account.UpdateProfile`, `Auth.SignIn`, `Auth.SignInFailed`, `Auth.PasswordChanged` – không chứa mật khẩu / hash / token / cookie |
| Phụ trợ | `IAccessService.GetMembershipsAsync` (cho `/auth/me`); Swagger có nút Authorize cho header `X-XSRF-TOKEN` |
| Test | Integration `Auth/AuthTests` (đăng ký, đăng nhập email / username, lỗi 401, mật khẩu tạm 403, đổi mật khẩu / khóa → phiên khác 401, quét log + AuditLog, rate limit, nâng cấp hash, thiếu `SECRET_KEY`); Unit `AuthServiceTests`, `AccessServiceMembershipTests`, `PasswordChangeRequiredFilterTests`, `PasswordPolicyTests`, `UserTests`, `AspNetPasswordHasherTests` |

### Fund Lifecycle – Implemented (PR #8, 2026-11-06)
Yêu cầu: FR-22, FR-23, FR-11, BR-02, BR-23, BR-36 · API: [backend-api.md](../api/backend-api.md) mục Funds.

| Hạng mục | Nội dung |
|---|---|
| `GET /funds` | Quỹ người dùng có vai trò, sắp theo tên. Mọi vai trò: `id`, `name`, `role`, `permissions[]`, `currentBalance` (thu − chi chưa hủy), `createdAt`, `treasurerStatus` (`Unassigned` / `Assigned`), `treasurerNames[]` (chỉ họ tên). Chỉ GVCN: `studentCount`. Chỉ phụ huynh: `linkedStudents[]` – trường riêng không có trong JSON của vai trò khác. Cần đăng nhập, không còn mật khẩu tạm |
| `POST /funds` | `[RequirePermission(Fund.Create)]` – chỉ giáo viên. Tên 1–100 ký tự (sau khi cắt khoảng trắng). Người tạo là GVCN; tạo 6 danh mục mặc định (`Domain/Rules/DefaultCategories`: Thu "Đóng quỹ", "Thu khác"; Chi "Văn phòng phẩm", "Sinh hoạt, liên hoan", "Khen thưởng", "Chi khác"); chưa có thủ quỹ → 201 `{ id }` |
| `PUT /funds/{fundId}` | `[RequirePermission(Fund.Manage)]` – chỉ GVCN. Không thuộc quỹ → 404; thủ quỹ / học sinh / phụ huynh → 403. Đổi sang đúng tên hiện tại → 204, không ghi nhật ký |
| AuditLog | `Fund.Create` (một dòng, kèm danh sách danh mục mặc định), `Fund.Rename` (tên cũ / mới) |
| Quyết định | Thông tin thủ quỹ trả cho mọi vai trò (theo `backend-api.md`, Canvas G-01); không thêm `ownerName` (2026-11-06) |
| Test | Integration `Funds/FundTests` (tạo quỹ + 6 danh mục, validate tên, chỉ giáo viên tạo, GVCN đổi tên + audit, vai trò khác 403 / ngoài quỹ 404, trường theo vai trò + số dư, chỉ quỹ của mình, 401 / 403 mật khẩu tạm); Unit `FundServiceTests` |

### Danh mục – Implemented (Dev B, PR #6)
Xem mã nguồn `Features/Categories` và test `Categories/CategoryTests`, `CategoryServiceTests`.

### Thu chi – Implemented (Dev B, PR #7)
Thêm, sửa, hủy (có lý do), danh sách, chi tiết, lịch sử thay đổi (FR-02 → FR-07). Xem mã nguồn `Features/Transactions`, TD-93 → TD-96 và test `Transactions/TransactionTests`, `TransactionServiceTests`, `TransactionsApiTests`, `FundTransactionTests`. `currentBalance` của `GET /funds` (Fund Lifecycle) tính trên cùng bảng `FundTransaction` nên phản ánh ngay các khoản thủ quỹ ghi qua API này.

### Học sinh & phụ huynh – Implemented, chờ review / merge (2026-11-06)
Yêu cầu: FR-24, FR-25, FR-26, BR-29, BR-30, BR-33, BR-35, P-13, P-14, P-16, NFR-10, NFR-11 · API: [backend-api.md](../api/backend-api.md) mục Students. Mọi endpoint `[RequirePermission(Student.Manage)]` – chỉ GVCN; thủ quỹ / học sinh / phụ huynh → 403, không thuộc quỹ hoặc học sinh của quỹ khác → 404. Mọi response `Cache-Control: no-store`.

| Endpoint | Nội dung |
|---|---|
| `GET /funds/{fundId}/students` | Học sinh của quỹ (sắp theo tên): `id`, `fullName`, `username`, `accountStatus` (`None` / `PendingActivation` / `Active` / `Disabled`), `isTreasurer`, `parent { fullName, email, pendingActivation }` hoặc `null`, `createdAt` |
| `POST /funds/{fundId}/students` | `{ fullName, parentEmail?, parentFullName? }` → 201 `{ id, parent? }`; có email phụ huynh thì liên kết luôn trong cùng thao tác – lỗi liên kết làm hỏng cả thao tác (không có học sinh nửa vời) |
| `GET / PUT /funds/{fundId}/students/{id}` | Chi tiết / đổi họ tên (1–100 ký tự) → 204 |
| `POST …/{id}/account` | `{ username? }` → `{ login, temporaryPassword }`. Bỏ trống → gợi ý `ten.ho.NNN` không dấu (ví dụ "Phạm Đức Huy" → `huy.pham.583`). Tạo `User` (Student, `MustChangePassword = true`), gán `Student.UserId`. 422 `student.account_exists`, `user.username`, `user.username_taken` |
| `POST …/{id}/reset-password` | → `{ login, temporaryPassword }`; đổi SecurityStamp → mọi phiên cũ 401, phải đổi mật khẩu lại. 422 `student.no_account` |
| `POST …/{id}/parent` | `{ email, fullName? }` → `{ accountCreated, credential \| null, emailSent: false }`. Email phụ huynh đã có → chỉ tạo `ParentLink`; chưa có → tạo `User` (Parent, mật khẩu tạm, tên mặc định "Phụ huynh {tên học sinh}"); email giáo viên / học sinh → 422 `parent.email_in_use`; học sinh đã có liên kết → 422 `parent_link.exists` |
| `POST …/{id}/parent/unlink` | 204; đánh dấu `IsActive = false`, `RemovedBy`, `RemovedAt` – tài khoản phụ huynh và lịch sử giữ nguyên; chưa có liên kết → 404 |

| Hạng mục | Nội dung |
|---|---|
| Mật khẩu tạm | `Credentials.TemporaryPassword()` – 16 ký tự `RandomNumberGenerator`, dạng `Xxxx-xxxx-0000-xxxx`, bỏ ký tự dễ nhầm (I, O, l, o, 0, 1); chỉ có trong response, lưu dạng hash (PBKDF2 + pepper), không vào AuditLog / log |
| Đồng thời | Cấp tài khoản, cấp lại mật khẩu, liên kết phụ huynh chạy trong `InTransactionAsync` + `LockAsync<Student>` (TD-106); UNIQUE `UQ_User_Username`, `UQ_Student_UserId`, `UQ_ParentLink_Student_Active` là chốt chặn cuối |
| AuditLog | `Student.Create`, `Student.Update`, `Student.IssueAccount`, `Student.ResetPassword`, `Parent.CreateAccount`, `ParentLink.Create`, `ParentLink.Remove` – không chứa mật khẩu |
| Không xóa cứng | Trigger `TR_NoDelete` chặn `DELETE` trên `Student`, `ParentLink` (đã kiểm chứng bằng test) |
| Test | Integration `Students/StudentTests` (13 test); Unit `StudentServiceTests`, `CredentialsTests` |

## Kiểm chứng gần nhất (2026-11-06)
| Kiểm chứng | Kết quả |
|---|---|
| Build toàn solution (`TreatWarningsAsErrors`) | 0 warning, 0 error |
| Unit test | 242 / 242 |
| Integration test (PostgreSQL 16 – Testcontainers) | 65 / 65 |
| Khởi động API thủ công (`app_user`, Swagger) | Đạt (2026-11-05) |

## Chạy dev
```bash
cd backend/SchoolFundAPI
cp .env.example .env               # điền mật khẩu DB + SECRET_KEY (openssl rand -base64 48); .env không commit
docker compose up -d --wait        # PostgreSQL 16 + migrator_user / app_user
set -a && source .env && set +a    # nạp biến cho dotnet
dotnet tool restore
dotnet ef database update --project src/SchoolFund.Infrastructure --startup-project src/SchoolFund.Infrastructure
dotnet run --project src/SchoolFund.Api   # http://localhost:5286/swagger
dotnet test                               # cần Docker đang chạy
```

## Historical (mã nguồn đã xóa 2026-11-05)
R3 – Giáo viên · Học sinh · Phụ huynh (D-24 → D-33) đã từng được cài đặt đầy đủ trước khi xóa: build 0 / 0 · unit 57 / 57 · integration 67 / 67 · migration `InitialClassFund` + `TeacherStudentParent` · `UpgradeMigrationTests`. 2026-11-04 – R1 (một thủ quỹ / quỹ): 45 unit / 53 integration. Lệnh CLI cũ (`create-teacher`, `reset-password`, `disable-account`, `enable-account`) **chưa được làm lại**.
