# Tài liệu ClassFund – chỉ mục & chuẩn tài liệu

## 1. Bản đồ tài liệu

| Thư mục | File | Nội dung |
|---|---|---|
| (gốc repo) | `training-doc.md` | **Tài liệu chuẩn** tech stack & kiến trúc – không sửa |
| `requirements/` | `functional-requirements.md` | Phạm vi, FR-01…FR-54, NFR |
| | `open-questions.md` | OQ-xx + quyết định (M0-01 đã duyệt, gồm OQ-17) |
| `business-analysis/` | `roles-and-permissions.md` | Vai trò, ma trận permission, quy tắc AZ-xx |
| | `business-rules-and-processes.md` | BR-xx, quy trình thanh toán / sự kiện / quyết toán / rút tiền |
| `architecture/` | `architecture-overview.md` | Sơ đồ, thành phần Foundation, luồng request |
| | `foundation-guide.md` | **Cách làm một module FE → BE → DB → Test**, Definition of Done |
| | `coding-standards.md` | Quy tắc & ràng buộc code (bắt buộc) |
| `database/` | `database-design.md` | Quy ước, ERD, bảng, ràng buộc, seed |
| `api/` | `api-conventions.md` | URL, định dạng, lỗi, bảo mật |
| | `api-catalogue.md` | Danh mục endpoint dự kiến theo module |
| `ui-design/` | `design-system.md` | Token, component, quy tắc hiển thị |
| | `sitemap-and-navigation.md` | Route, mã màn hình, menu theo vai trò |
| `implementation/` | `module-plan.md` | **Module, task, phân công Dev A/B, phụ thuộc** |
| | `implementation-status.md` | Trạng thái, người chạy migration, tồn đọng, sự cố |
| | `neon-database-guide.md` | Neon dùng chung, tài khoản DB, quy trình migration, seed, backup |
| `testing/` | `test-strategy.md` | Công cụ, nguyên tắc, test có sẵn |
| | `test-cases.md` | Test case nghiệp vụ bắt buộc |

## 2. Thứ tự ưu tiên khi mâu thuẫn

1. `training-doc.md` (tech stack, kiến trúc)
2. Bản yêu cầu bài toán → `requirements/` (sau khi OQ được chốt)
3. `business-analysis/`
4. `database/`, `api/`, `ui-design/`
5. Code

Gặp mâu thuẫn: dừng lại, hỏi, sửa tài liệu trước rồi mới sửa code.

## 3. Mã định danh

| Tiền tố | Ý nghĩa | Định nghĩa ở |
|---|---|---|
| `FR-xx` / `NFR-xx` | Yêu cầu chức năng / phi chức năng | `functional-requirements.md` |
| `OQ-xx` | Câu hỏi mở | `open-questions.md` |
| `BR-xx` | Quy tắc nghiệp vụ | `business-rules-and-processes.md` |
| `AZ-xx` | Quy tắc phân quyền theo dữ liệu | `roles-and-permissions.md` |
| `Mx`, `Mx-Ty` | Module, task | `module-plan.md` |
| `A-`, `F-`, `M-`, `T-`, `C-`, `H-`, `L-`, `E-`, `B-`, `N-`, `U-`, `S-` | Mã màn hình | `sitemap-and-navigation.md` |
| `TC-Mx-yy` | Test case | `test-cases.md` |
| `TD-xx` | Việc tồn đọng | `implementation-status.md` |

## 4. Tài liệu phải cập nhật khi làm module

| Thay đổi | Cập nhật |
|---|---|
| Thêm/đổi endpoint | `api/api-catalogue.md` |
| Đổi schema | `database/database-design.md` (+ quy trình Neon) |
| Quy tắc mới / OQ được chốt | `business-rules-and-processes.md`, `open-questions.md` |
| Màn hình | `ui-design/sitemap-and-navigation.md` |
| Xong task | `implementation/implementation-status.md`, `testing/test-cases.md` |
