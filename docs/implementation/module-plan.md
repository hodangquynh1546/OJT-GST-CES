# Kế hoạch module – ClassFund

> v1.1 · 2026-11-11 (đổi phân công Quỳnh / Cường, v1.0 · 2026-11-09) · Backend đã đưa về **codebase Foundation** (nhánh `feature/foundation`). Bản backend đầy đủ trước đó dùng để **tham khảo** tại commit `6b6645d` trên `main` (`git show 6b6645d:<đường dẫn>`).
> Mỗi module làm **trọn luồng FE → BE → DB → Integration Test → E2E**, theo [foundation-guide.md](../architecture/foundation-guide.md) và [api-conventions.md](../api/api-conventions.md).
> Nguồn: màn hình [screen-specifications.md](../ui-design/screen-specifications.md) + Canvas v4.0 · API [backend-api.md](../api/backend-api.md) · Quy tắc [business-rules-and-processes.md](../business-analysis/business-rules-and-processes.md) · Quyền [roles-and-permissions.md](../business-analysis/roles-and-permissions.md) · Luồng [user-flows.md](../ui-design/user-flows.md) · Test [test-cases.md](../testing/test-cases.md).
> **Phân công (từ 2026-11-11):** Quỳnh – M1, M3, M5, M7a; Cường – M2, M4, M6, M7b; M8 cả hai (đợt 1 → 5, mục 1). ~~Dev A – M1, M3, M4, M7a; Dev B – M2, M5, M6, M7b~~ (bản 1.0: M4 và M5 đổi người). Hai module cùng đợt mà module sau cần dữ liệu của module trước (M4 cần M3, M6 cần M5) làm theo 4 bước: chốt hợp đồng → PR nhỏ (seed `TestData`, mock e2e) → làm module → e2e full sau khi module kia merge. Module cần đăng nhập / dữ liệu của module người kia chưa xong: integration dùng `TestData` + `SignInAsAsync`, FE / e2e mock dùng `signInAs` (mock `/auth/me`), e2e full chạy sau khi module kia merge. Không có deploy / CI/CD ở giai đoạn này.

## 1. Tổng quan và thứ tự

```text
M0 Foundation (xong)
 ├─ M1 Auth & Tài khoản ───────────────┐
 └─ M2 Quỹ ──┬─ M3 Học sinh & PH ─── M4 Thủ quỹ
             └─ M5 Danh mục & Thành viên ─ M6 Thu chi ─ M7 Tổng quan & Báo cáo
                                                         └─ M8 E2E toàn luồng + nghiệm thu
```

| Đợt | Module (làm song song) | Điều kiện bắt đầu | Ghi chú |
|---|---|---|---|
| 0 | **M0 Foundation** | – | Đã xong ở PR này |
| 1 | **M1** Auth & Tài khoản · **M2** Quỹ | M0 merge | M2 dùng `SignInAsAsync` ở integration; e2e full của M2 chạy sau khi M1 merge |
| 2 | **M3** Học sinh & Phụ huynh (Quỳnh) · **M4** Phân công thủ quỹ (Cường) | M2 merge | M4 dùng `TestData.CreateStudentAccountAsync(activated: true)` và mock học sinh 4 trạng thái (PR nhỏ của M3) nên không chờ M3; e2e full của M4 chạy sau M3 merge. ~~M3 ‖ M5~~ |
| 3 | **M5** Danh mục & Thành viên (Quỳnh) · **M6** Thu chi (Cường) | M2 merge (M4 nên xong trước để e2e full của M6 có thủ quỹ thật) | M6 seed thủ quỹ bằng `TestData`, danh mục mặc định có sẵn khi tạo quỹ; e2e full của M6 chạy sau M5 và M4. ~~M4 ‖ M6~~ |
| 4 | **M7a** Tổng quan (S-01) · **M7b** Báo cáo (S-03) | M6 merge | M7 tách hai nửa để 2 dev song song; S-01 dùng `reports/yearly` của M7b → chốt hợp đồng ngày đầu |
| 5 | **M8** E2E toàn luồng (mỗi dev một nhóm vai trò) | M7a + M7b merge | Kịch bản UF-01 → UF-13 qua cả 4 vai trò |

**Khi một module xong** (Definition of Done – foundation-guide.md mục 3.7), cùng PR cập nhật:
- [implementation-status.md](implementation-status.md): mục 1a, 3a.
- [test-cases.md](../testing/test-cases.md): cột Trạng thái của các case thuộc module.
- [functional-requirements.md](../requirements/functional-requirements.md): cột Trạng thái.
- [backend-api.md](../api/backend-api.md): nếu hợp đồng đổi.
- [backend-known-issues.md](backend-known-issues.md): nếu có giới hạn mới.

Mỗi đợt có hai module **không dùng chung file** (foundation-guide.md mục 5). Một module **chỉ được nghiệm thu** khi đạt Definition of Done (foundation-guide.md mục 3.7). Module phụ thuộc chỉ bắt đầu e2e full sau khi module nó phụ thuộc đã nghiệm thu.

## 2. Ma trận module

| Module | Màn hình | Thư mục FE | Thư mục BE (`Features/`) | Bảng DB chính | Permission |
|---|---|---|---|---|---|
| M1 | A-01, A-02, A-03, A-04 | `features/auth`, `features/account` (+ mở rộng `core/auth/auth.service.ts`) | `Auth` | User, AuditLog | ẩn danh / đăng nhập |
| M2 | G-01, G-04 | `features/funds` | `Funds` | Fund, Category (mặc định) | `Fund.Create`, `Fund.Manage` |
| M3 | G-02 | `features/students` | `Students` | Student, User, ParentLink | `Student.Manage` |
| M4 | G-03 | `features/treasurers` | `Treasurers` | TreasurerAssignment | `Treasurer.Assign` |
| M5 | T-02, T-03 | `features/categories`, `features/members` | `Categories`, `Members` | Category | `Category.Manage`, `Member.View`, `Fund.View` |
| M6 | S-02, T-01 | `features/transactions` | `Transactions` | FundTransaction, AuditLog | `Fund.View`, `Ledger.ViewFull`, `Transaction.Manage` |
| M7 | S-01, S-03 | `features/overview`, `features/reports` | `Overview`, `Reports` | FundTransaction (đọc) | `Fund.View` |
| M8 | Toàn bộ + X-01 | `e2e/full/journey.spec.ts` | – | – | – |

---

## 3. Chi tiết module

**Mẫu một module (giữ đúng thứ tự mục khi thêm / sửa module):**
1. Phạm vi.
2. Màn hình (mã + route + board Canvas).
3. API (endpoint + permission, theo [backend-api.md](../api/backend-api.md)).
4. Quy tắc (FR / BR / NFR).
5. Mã lỗi.
6. Acceptance Criteria (đánh số, kiểm chứng được).
7. Test (mã trong [test-cases.md](../testing/test-cases.md) + kịch bản e2e full).
8. Phụ thuộc.

Module không được thêm yêu cầu ngoài các tài liệu nguồn. Thấy thiếu hoặc mâu thuẫn thì ghi vào [open-questions.md](../requirements/open-questions.md) và hỏi trước.

### M0 – Foundation (đã xong, chờ merge)

- **Phạm vi:** khung BE / FE, quy ước chung, hạ tầng test – chi tiết [foundation-guide.md](../architecture/foundation-guide.md) mục 1.
- **Test:** G-01, G-02, G-03 (phần khóa tài khoản), `FoundationTests`, `ApiContractTests`, e2e mock shell, e2e full nền. Còn thiếu: M-03 / trigger (KI-23 – giao cho M3, M4).
- **Sửa Foundation sau này:** PR nhỏ riêng, merge trước module cần nó (foundation-guide.md mục 5).

### M1 – Auth & Tài khoản

- **Phạm vi:**
  - Đăng ký giáo viên, đăng nhập bằng một ô (email hoặc tên đăng nhập), đăng xuất.
  - Đặt mật khẩu mới bắt buộc khi còn mật khẩu tạm.
  - Trang tài khoản: sửa họ tên, đổi mật khẩu.
  - Mọi phiên khác đăng xuất khi đổi mật khẩu.
- **Màn hình:** A-01 `/register-teacher`, A-02 `/login`, A-03 `/change-password`, A-04 `/account` (Canvas: `TeacherRegister`, `Login`, `ChangePasswordRequired`, `Account*`).
- **API:**
  - `POST /auth/register-teacher` (ẩn danh, rate limit) → 201 phiên.
  - `POST /auth/login` (ẩn danh, rate limit).
  - `POST /auth/logout`.
  - `GET` / `PUT /auth/me`.
  - `POST /auth/change-password`.
  - Response đổi phiên trả `antiforgeryToken`. `/auth/me`, `/auth/change-password`, `/auth/logout` có `[AllowTemporaryPassword]`.
- **Quy tắc:** FR-20, FR-13, FR-21 · BR-17, BR-18, BR-26, BR-28 · NFR-10 · mật khẩu 8–128 ký tự, có chữ và số (`PasswordPolicy`).
- **Mã lỗi:** `auth.invalid_credentials`, `auth.account_disabled` (401), `auth.password_policy`, `auth.password_reused`, `auth.wrong_password`, `user.email_taken` (422), `auth.rate_limited` (429).
- **Acceptance Criteria:**
  1. Giáo viên đăng ký thành công thì được đăng nhập luôn và vào `/funds` (trống). Gửi kèm `role` / `kind` bị bỏ qua.
  2. Đăng nhập không có ô chọn vai trò. Sai thông tin → một thông báo chung, không tiết lộ email có tồn tại hay không.
  3. Tài khoản còn mật khẩu tạm bị đưa về A-03 ở mọi route. Đổi xong → về trang chính theo `homeUrl()`.
  4. Đổi mật khẩu thì các phiên khác bị đăng xuất (401). Mật khẩu cũ / tạm không dùng lại được.
  5. Không có mật khẩu, hash hay cookie trong log và AuditLog.
- **Test:**
  - Integration: T-01, T-02, S-03, S-04, G-03 (phần đổi mật khẩu), G-05. (G-02 antiforgery đã có ở Foundation – M1 thêm case login / register gửi thiếu header.)
  - E2E full: GVCN đăng ký → đăng xuất → đăng nhập. Học sinh / phụ huynh có mật khẩu tạm (seed bằng `TestData` qua integration, hoặc chờ M3) → A-03.
- **Phụ thuộc:** M0. **Cung cấp cho module khác:** `RealApi` seed bằng đăng ký và đăng nhập thật.

### M2 – Quỹ (Quỹ của tôi / Quỹ của con, tạo quỹ, thông tin quỹ)

- **Phạm vi:**
  - Danh sách quỹ theo vai trò.
  - GVCN tạo quỹ, quỹ mới có sẵn danh mục mặc định và chưa có thủ quỹ.
  - GVCN đổi tên quỹ.
  - Phụ huynh xem "Quỹ của con".
- **Màn hình:** G-01 `/funds` (`FundList`, `FundListParent`), G-04 `/funds/:id/settings` (`FundSettingsTeacher`).
- **API:**
  - `GET /funds` trả `{ id, name, role, permissions, currentBalance, createdAt, treasurerStatus, studentCount (GVCN), treasurerNames, linkedStudents (PH) }`.
  - `POST /funds` (`Fund.Create`) → 201 `{ id }`.
  - `PUT /funds/{id}` (`Fund.Manage`) → 204.
- **Quy tắc:** FR-22, FR-23, FR-11 · BR-36, BR-37 · `DefaultCategories`.
- **Mã lỗi:** `validation_failed` (tên 1–100 ký tự), `forbidden`, `not_found`.
- **Acceptance Criteria:**
  1. GVCN thấy thẻ quỹ: tên, "Chưa phân công thủ quỹ / Đã có thủ quỹ", số dư, số học sinh, tên thủ quỹ, ngày tạo.
  2. Phụ huynh thấy tên con, không thấy `studentCount`.
  3. Tạo quỹ bằng dialog 480 px. Quỹ mới có danh mục mặc định, ở trạng thái `Unassigned`.
  4. Giáo viên B không xem / sửa được quỹ của A (404). Thủ quỹ / học sinh / phụ huynh không tạo / sửa được (403).
  5. Đổi tên ghi AuditLog `Fund.Rename`. Header cập nhật tên mới.
- **Test:**
  - Integration: T-03, T-05, P-04 (danh sách quỹ), TR-03 (đổi tên, tạo quỹ).
  - E2E full: GVCN đăng ký → tạo 2 quỹ → đổi tên.
- **Phụ thuộc:** M0 (integration), M1 (e2e full).

### M3 – Học sinh & Phụ huynh

- **Phạm vi:**
  - Danh sách học sinh; thêm, sửa họ tên.
  - Cấp tài khoản (gợi ý tên đăng nhập, mật khẩu tạm hiện một lần), cấp lại mật khẩu tạm.
  - Liên kết phụ huynh theo email: tạo tài khoản nếu chưa có, chỉ liên kết nếu đã có. Gỡ liên kết.
- **Màn hình:** G-02 `/funds/:id/students` (`Students`): Kendo Grid + Drawer 460 px + dialog mật khẩu tạm (phải tick "Tôi đã lưu" mới đóng được).
- **API:**
  - `GET` / `POST /funds/{id}/students`.
  - `GET` / `PUT …/{sid}`.
  - `POST …/{sid}/account`, `…/reset-password`, `…/parent`, `…/parent/unlink`.
  - Tất cả cần `Student.Manage`. Response có mật khẩu tạm → `Cache-Control: no-store`.
- **Quy tắc:** FR-24, FR-25, FR-26 · BR-27, BR-29, BR-30, BR-33, BR-35, BR-37 · NFR-10, NFR-11, NFR-12 · `Credentials`, `LoginName`.
- **Mã lỗi:** `student.account_exists`, `student.no_account`, `user.username`, `user.username_taken`, `parent_link.exists`, `parent.email_in_use`, `parent.email_conflict` (422).
- **Acceptance Criteria:**
  1. Danh sách có tên đăng nhập, trạng thái tài khoản (Chưa cấp / Chờ kích hoạt / Đang hoạt động / Bị khóa), cờ thủ quỹ, phụ huynh.
  2. Cấp tài khoản: username gợi ý dạng `huy.pham.NNN`, mật khẩu tạm `Xxxx-xxxx-0000-xxxx`, ghi rõ "Hệ thống không gửi email". Cấp lần 2 → lỗi `student.account_exists`.
  3. Cấp lại mật khẩu (có dialog xác nhận) → phiên cũ của học sinh hết hiệu lực.
  4. Cùng email phụ huynh ở quỹ khác → chỉ liên kết (`credential = null`). Email của giáo viên → `parent.email_in_use`.
  5. Mật khẩu tạm không có trong log và AuditLog. Không DELETE được bằng `app_user`.
- **Test:**
  - Integration: S-01, S-02, S-05, S-06, S-09, P-01, P-02, P-03, P-05, T-05 / TR-03 (phần học sinh), M-02 (Student, ParentLink – KI-23).
  - E2E full: GVCN thêm học sinh kèm email phụ huynh → cấp tài khoản → học sinh đăng nhập bằng mật khẩu tạm → A-03. Phụ huynh đăng nhập → thấy "Quỹ của con".
- **Phụ thuộc:** M2 (quỹ), M1 (e2e đăng nhập).

### M4 – Phân công thủ quỹ

- **Phạm vi:**
  - Phân công học sinh đã kích hoạt; một quỹ có thể có nhiều thủ quỹ.
  - Thu hồi; không thu hồi được thủ quỹ cuối cùng.
  - Lịch sử phân công.
- **Màn hình:** G-03 `/funds/:id/treasurers` (`Treasurers`): thẻ "Đang là thủ quỹ (n)", bảng lịch sử, dialog chọn học sinh (radio). Banner "Chưa phân công thủ quỹ" (UF-12).
- **API:** `GET` / `POST /funds/{id}/treasurers`, `POST …/{aid}/revoke` (`Treasurer.Assign`).
- **Quy tắc:** FR-27 · BR-31, BR-32, BR-33, BR-36 · NFR-09 (thu hồi đồng thời).
- **Mã lỗi:** `treasurer.no_account`, `treasurer.account_disabled`, `treasurer.account_not_activated`, `treasurer.already_assigned`, `treasurer.last` (422), `treasurer.already_revoked` (409).
- **Acceptance Criteria:**
  1. Chỉ chọn được học sinh của quỹ có tài khoản đang hoạt động. Học sinh ngoài quỹ → 404.
  2. Nút "Thu hồi" của thủ quỹ cuối cùng bị khóa, kèm giải thích "phân công người mới trước". Gọi thẳng API → `treasurer.last`.
  3. Người bị thu hồi mất quyền ghi ngay ở request kế tiếp (403). Người mới ghi được.
  4. Hai lệnh thu hồi đồng thời: đúng một lệnh thành công, quỹ vẫn còn ít nhất một thủ quỹ.
- **Test:**
  - Integration: TR-01, TR-04, TR-05, TR-06, TR-07, T-05 / TR-03 (phần thủ quỹ), M-02 (TreasurerAssignment – KI-23).
  - E2E full: GVCN phân công → thủ quỹ thấy menu Danh mục → đổi người → người cũ mất menu.
- **Phụ thuộc:** M3 (học sinh có tài khoản), M1.

### M5 – Danh mục & Thành viên

- **Phạm vi:**
  - Thủ quỹ thêm, đổi tên, ẩn / hiện danh mục thu chi.
  - Danh sách danh mục cho bộ lọc theo phạm vi người xem.
  - Thủ quỹ / GVCN xem danh sách thành viên (chỉ họ tên + vai trò).
- **Màn hình:** T-02 `/funds/:id/categories` (`FundSettings`), T-03 `/funds/:id/members` (`Members`).
- **API:**
  - `GET /funds/{id}/categories?type&includeHidden` (`Fund.View`; bản công khai theo X-01).
  - `POST` / `PUT …/categories/{cid}` (`Category.Manage`).
  - `GET /funds/{id}/members` (`Member.View`).
- **Quy tắc:** FR-10, FR-12 · BR-08, BR-25 · NFR-08.
- **Mã lỗi:** `category.name_taken` (422, so tên không phân biệt hoa thường).
- **Acceptance Criteria:**
  1. Danh mục chia theo loại Thu / Chi, có `transactionCount`. Danh mục ẩn chỉ hiện khi bật "Hiện danh mục ẩn".
  2. Trùng tên trong cùng quỹ và cùng loại → lỗi gắn vào ô tên.
  3. Học sinh / phụ huynh chỉ thấy danh mục đang hiện có ít nhất một khoản công khai, không có `transactionCount`.
  4. Thành viên: chỉ `fullName` + `role` (GVCN, thủ quỹ, học sinh, kể cả học sinh chưa có tài khoản). Không có email / username. Không gồm phụ huynh.
  5. GVCN thêm danh mục → 403.
- **Test:**
  - Integration: T-04 (danh mục), TR-02 (thành viên), P-07 (danh mục công khai).
  - E2E full: thủ quỹ thêm / ẩn danh mục. GVCN không thấy menu Danh mục.
- **Phụ thuộc:** M2. Seed thủ quỹ bằng `TestData` (không chờ M4).

### M6 – Thu chi (sổ quỹ)

- **Phạm vi:**
  - Thủ quỹ thêm khoản thu / chi (≤ 3 thao tác, chống ghi trùng), sửa, hủy có lý do.
  - Danh sách có lọc và phân trang, chi tiết, lịch sử thay đổi.
  - Bản công khai cho học sinh chưa phân công và phụ huynh.
- **Màn hình:**
  - S-02 `/funds/:id/transactions(/:txId)` (`Ledger*`): bộ lọc, Grid server paging, panel chi tiết 340 px.
  - T-01 dialog 560 px (`TransactionForm`, `TxDialog`), mở từ S-01 / S-02.
- **API:**
  - `GET /funds/{id}/transactions` (lọc `type`, `categoryId`, `from`, `to`, `search`, `status`, `page`, `pageSize`).
  - `GET …/{tid}`, `GET …/{tid}/history` (`Ledger.ViewFull`).
  - `POST` (có `idempotencyKey`) / `PUT …/{tid}`, `POST …/{tid}/cancel` (`Transaction.Manage`).
- **Quy tắc:** FR-02 → FR-07 · BR-01 → BR-07, BR-11, BR-12, BR-14, BR-20, BR-22 · NFR-03, NFR-05, NFR-08, NFR-09.
- **Mã lỗi:** `transaction.future_date`, `transaction.category_invalid`, `transaction.cancelled`, `transaction.cancel_reason_required` (422).
- **Acceptance Criteria:**
  1. Thêm: số tiền > 0, ngày không ở tương lai (giờ Việt Nam), danh mục đúng loại và chưa ẩn. Bấm Lưu nhiều lần không tạo khoản trùng.
  2. Chi vượt số dư vẫn lưu, kèm cảnh báo vàng (`isNegativeBalance`).
  3. Sửa không đổi được loại thu / chi. Khoản đã hủy không sửa được. Hủy bắt buộc lý do ≤ 500 ký tự, có dialog xác nhận. Không có nút Xóa.
  4. Lịch sử thay đổi có người, thời điểm, giá trị trước / sau. Người ghi / sửa / hủy được lưu đúng.
  5. Bản công khai: không có `note`, `isPublic`, `cancelReason`, tên người thực hiện. Khoản không công khai → 404. Tìm kiếm chỉ trong nội dung. `total` đếm sau khi lọc.
  6. Hai thủ quỹ ghi đồng thời không làm sai số dư.
- **Test:**
  - Integration: T-04, T-06, TR-02, TR-08, S-07, S-08, P-06, P-08, G-04, R-01.
  - E2E full: thủ quỹ thêm thu / chi → sửa → hủy. GVCN xem đủ chi tiết nhưng không có nút ghi. Phụ huynh không thấy khoản không công khai.
- **Phụ thuộc:** M5 (danh mục), M4 (e2e full – thủ quỹ thật).

### M7 – Tổng quan & Báo cáo (làm song song: M7a Tổng quan S-01 · M7b Báo cáo S-03)

- **Phạm vi:**
  - Tổng quan quỹ: số dư, tổng thu, tổng chi, tháng hiện tại, 5 khoản gần nhất, người quản lý, biểu đồ 3 tháng.
  - Báo cáo tháng (đầu kỳ, thu, chi, chênh lệch, cuối kỳ, theo danh mục, danh sách).
  - Thống kê 12 tháng.
- **Màn hình:** S-01 `/funds/:id` (`Overview*`, tweak `viewState`), S-03 `/funds/:id/reports` (`Reports*`, TabStrip "Theo tháng | Theo năm").
- **API:**
  - `GET /funds/{id}` (overview).
  - `GET …/reports/monthly?year&month`.
  - `GET …/reports/yearly?year`.
  - Tất cả cần `Fund.View`.
- **Quy tắc:** FR-01, FR-08, FR-09 · BR-09, BR-10, BR-15, BR-23, BR-24 · bản công khai gộp "Khoản khác" không có số lượng (P1).
- **Mã lỗi:** `validation_failed` (`year` 1–9999, `month` 1–12), `not_found`.
- **Acceptance Criteria:**
  1. Số dư, tổng thu, tổng chi và số liệu tháng giống nhau ở mọi vai trò, không tính khoản đã hủy.
  2. Tháng theo giờ Việt Nam. Số dư đầu tháng bằng số dư cuối tháng trước. Năm luôn đủ 12 tháng.
  3. Bản công khai: danh sách chỉ có khoản công khai. `byCategory` có dòng "Khoản khác" (`categoryId: null`, `count: null`).
  4. S-01 có đủ trạng thái loading / empty / error / số dư âm / chưa phân công thủ quỹ. Thủ quỹ có nút "+ Khoản thu / + Khoản chi" (mở T-01 của M6).
- **Test:**
  - Integration: S-07 (tổng quan), P-07 (báo cáo), R-01 (báo cáo tháng / năm).
  - E2E full: dữ liệu M6 → số liệu S-01 / S-03 khớp ở 4 vai trò.
- **Phụ thuộc:** M6. Integration seed giao dịch bằng `TestData` (thêm hàm seed giao dịch trong PR Foundation nhỏ nếu cần).

### M8 – E2E toàn luồng & nghiệm thu

- **Phạm vi:**
  - Kịch bản xuyên module bằng API, DB và UI thật: UF-01 → UF-13 (bỏ phần PD đang chờ quyết định).
  - Kiểm tra lại trạng thái X-01 (401 / 403 / 404 / 409 / 422 / mật khẩu tạm / chưa phân công).
  - Smoke test X-01 trong test-cases.
- **Acceptance Criteria:**
  1. GVCN đăng ký → tạo quỹ → thêm học sinh + phụ huynh → cấp tài khoản → phân công thủ quỹ.
  2. Thủ quỹ đổi mật khẩu → ghi thu chi.
  3. Học sinh và phụ huynh xem bản công khai đúng phạm vi.
  4. Không còn màn hình `coming-soon`.
  5. Cập nhật `implementation-status.md` và `test-cases.md`.
- **Phụ thuộc:** M1 → M7.

## 4. Ngoài phạm vi (cần lead quyết định trước khi thêm module)

Đợt thu, duyệt chi, hoàn tiền · quên mật khẩu / gửi email · deploy + CI/CD (chưa cần – chưa có môi trường deploy) · sinh model TypeScript tự động từ Swagger.

**Không làm (đã chốt):** GVCN khóa / mở khóa tài khoản học sinh, học sinh rời lớp, chuyển quỹ cho giáo viên khác (D-41, bỏ FR-28).
