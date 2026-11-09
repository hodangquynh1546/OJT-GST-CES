# Roadmap triển khai

> Cập nhật 2026-11-09 (D-39). Từ 2026-11-09 roadmap là **M0 → M8 theo module trọn luồng** ([module-plan.md](module-plan.md)); các bước R1 → R11 bên dưới là *historical*. 2026-11-04 sau Review & Simplify. Roadmap M1 → M14 (phạm vi toàn trường) đã **hủy** – xem `scope-review-2026-11-04.md`.

| Bước | Nội dung | Trạng thái |
|---|---|---|
| *M0 (2026-11-04)* | Nền tảng (build, ProblemDetails, log, versioning, Swagger, health, 2 tài khoản DB, test hạ tầng) | Not Implemented (từng Done; mã đã xóa 2026-11-05) |
| R1 | Tái cấu trúc theo bài toán thủ quỹ: tài khoản, quỹ, danh mục, khoản thu / chi, tổng quan, báo cáo tháng / năm | Thay bằng R3 (historical: 45 unit / 53 integration; mã đã xóa) |
| R2 | 3 vai trò (Thủ quỹ / GVCN / Phụ huynh): `FundMember`, permission theo vai trò, phạm vi công khai, quản lý thành viên – kế hoạch B1 → B7 trong `role-scope-review-2026-11-04.md` | Thiết kế v1.2 hoàn tất; PO duyệt D-10, D-11, D-12, Q-11, Q-12, B2–B7 (2026-11-04). **Chờ chốt B1 (+ D-09), X-01, X-02 và lệnh triển khai.** Chưa có code / migration |
| R3 | Giáo viên · Học sinh · Phụ huynh (D-24 → D-33): đăng ký giáo viên, nhiều quỹ, học sinh, tài khoản tạm, liên kết phụ huynh, phân công thủ quỹ, API theo quỹ, migration `TeacherStudentParent` | **Not Implemented** – từng làm xong 2026-11-05 (historical: 57 unit / 67 integration), mã nguồn đã xóa cùng ngày (D-35). Đặc tả giữ nguyên |
| R4 | Làm lại theo docs: Codebase Foundation, Domain Entities, `AppDbContext`, `AccessService`, migration `InitialClassFund` + `AddDatabaseProtections` | Done (nhánh `feature/codebase`) |
| R5 | Feature Auth & User Session: đăng ký giáo viên, đăng nhập một ô, `/auth/me`, đổi mật khẩu tạm, phiên (`IdentityStore` thay `NoIdentityStore`), pepper `SECRET_KEY`, integration test (Testcontainers), `docker-compose.yml` | *Historical* – Done PR #5, 2026-11-05; endpoint Auth **đã bỏ khỏi codebase 2026-11-09** (tham khảo `6b6645d`); phần nền **giữ trong Foundation**: `IdentityStore`, cookie phiên, pepper `SECRET_KEY`, `ClassFund.IntegrationTests`, `docker-compose.yml` |
| R6 | Feature Danh mục (Dev B) | *Historical* – Done PR #6; **đã bỏ khỏi codebase 2026-11-09** (tham khảo `6b6645d`) |
| R7 | Feature Thu chi (Dev B): thêm, sửa, hủy, danh sách, lịch sử thay đổi | *Historical* – Done PR #7, 2026-11-06; **đã bỏ khỏi codebase 2026-11-09** (tham khảo `6b6645d`) |
| R8 | Feature Fund Lifecycle: `GET /funds`, `POST /funds` (+ 6 danh mục mặc định), `PUT /funds/{fundId}` | *Historical* – Done PR #8, 2026-11-06; **đã bỏ khỏi codebase 2026-11-09** (tham khảo `6b6645d`) |
| R9 | Feature Học sinh & phụ huynh: danh sách / thêm / sửa học sinh, cấp tài khoản + mật khẩu tạm, cấp lại mật khẩu, liên kết / gỡ phụ huynh | *Historical* – Done PR #9, 2026-11-06; **đã bỏ khỏi codebase 2026-11-09** (tham khảo `6b6645d`) |
| R10 | Feature Tổng quan, Báo cáo, Thành viên (Dev B): `GET /funds/{fundId}`, `reports/monthly`, `reports/yearly`, `members` | *Historical* – Done `feature/be-reports`, 2026-11-06; **đã bỏ khỏi codebase 2026-11-09** (tham khảo `6b6645d`) |
| R11 | Feature Phân công thủ quỹ: danh sách / phân công / thu hồi, khóa dòng `Fund` chống thu hồi thủ quỹ cuối cùng khi đồng thời | *Historical* – Done `feature/devA/treasurers`, 2026-11-06; **đã bỏ khỏi codebase 2026-11-09** (tham khảo `6b6645d`) |
| **M0** | **Foundation** BE + FE (D-39): quy ước API, `ApiControllerBase`, tự đăng ký service, Swagger hợp đồng + snapshot, `FoundationTests`, tiện ích FE, Design System → Kendo, e2e mock / full | **Done** – nhánh `feature/foundation`, chờ review / merge |
| M1 → M8 | Module trọn luồng (chia 2 dev: [module-plan.md](module-plan.md) mục 1) FE → BE → DB → Test: Auth · Quỹ · Học sinh & PH · Thủ quỹ · Danh mục & Thành viên · Thu chi · Tổng quan & Báo cáo · E2E toàn luồng | Not Implemented – thứ tự và đợt song song: [module-plan.md](module-plan.md) |
| Chờ quyết định | PD-01 → PD-06, CLI vận hành (TD-88), deploy + CI/CD (chưa cần), sinh model từ Swagger, đợt thu / duyệt chi / hoàn tiền | Pending Decision |

## Lịch sử
| Ngày | Thay đổi |
|---|---|
| 2026-11-04 | Thêm R2 (3 vai trò) |
| 2026-11-04 | R2 bổ sung: đổi vai trò, `UpdatedBy` / `CancelledBy`, bản công khai có khoản đã hủy, test cases `docs/testing/test-cases.md` |
| 2026-11-05 | R3 triển khai; R2 đánh dấu thay thế |
| 2026-11-05 | Xóa mã nguồn (D-35): M0, R1, R3 → Not Implemented |
| 2026-11-05 | Thêm R4; cập nhật Hiện tại / Tiếp theo |
| 2026-11-06 | Thêm R5 (Auth), R6 (Danh mục), R7 (Thu chi); cập nhật Tiếp theo |
| 2026-11-06 | Thêm R8 (Fund Lifecycle); cập nhật Tiếp theo |
| 2026-11-06 | Thêm R9 (Học sinh & phụ huynh); cập nhật Tiếp theo |
| 2026-11-06 | Thêm R10 (Tổng quan, Báo cáo, Thành viên); cập nhật Tiếp theo |
| 2026-11-06 | Thêm R11 (Phân công thủ quỹ) – hoàn thành 4 module của Dev A; cập nhật Tiếp theo |
| 2026-11-09 | D-39: R5 → R11 thành *historical* (đã bỏ khỏi codebase); thêm M0 Foundation (Done), M1 → M8 theo module |
