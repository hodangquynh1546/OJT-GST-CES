# Tài liệu ClassFund – chỉ mục và chuẩn tài liệu

> v1.0 · 2026-11-09 · **Giai đoạn hiện tại:** chỉ có **Foundation (M0)**. Chưa triển khai chức năng nghiệp vụ nào (D-39). Tài liệu dưới đây là chuẩn chung để mọi module sau này làm theo cùng một cách.
> Tech stack và kiến trúc: [`training-doc.md`](../training-doc.md) (không sửa). Quy tắc làm việc: [`CLAUDE.md`](../CLAUDE.md).

## 1. Bản đồ tài liệu

| Loại | Tài liệu | Dùng để |
|---|---|---|
| **Đặc tả nghiệp vụ** (Source of Truth) | [requirements/functional-requirements.md](requirements/functional-requirements.md) (FR, NFR) · [business-analysis/business-rules-and-processes.md](business-analysis/business-rules-and-processes.md) (BR, P-) · [business-analysis/roles-and-permissions.md](business-analysis/roles-and-permissions.md) · [requirements/open-questions.md](requirements/open-questions.md) (D-, PD-, Q-) | Hệ thống phải làm gì, ai được làm gì |
| **Đặc tả dữ liệu** | [database/](database/database-design-overview.md): ERD, data dictionary, ràng buộc, công thức tài chính, thao tác dữ liệu, quyết định DD- | Bảng, cột, ràng buộc, cách ghi DB |
| **Hợp đồng API** | [api/backend-api.md](api/backend-api.md) (từng endpoint, theo module) | Đường dẫn, permission, body, mã lỗi |
| **Đặc tả giao diện** | [ui-design/](ui-design/ui-design-overview.md) + Canvas v4.0 Desktop Web | Màn hình, luồng UF-, Design System |
| **Chuẩn triển khai** | [api/api-conventions.md](api/api-conventions.md) · [architecture/foundation-guide.md](architecture/foundation-guide.md) · [implementation/backend-technical-decisions.md](implementation/backend-technical-decisions.md) (TD-) | Code thế nào: quy ước API / DB, cấu trúc module, quy trình, test, tránh conflict |
| **Kế hoạch** | [implementation/module-plan.md](implementation/module-plan.md) (M0 → M8) · [implementation/backend-roadmap.md](implementation/backend-roadmap.md) | Làm gì, theo thứ tự nào, nghiệm thu ra sao |
| **Kiểm thử** | [testing/test-cases.md](testing/test-cases.md) (mục A) · [implementation/backend-checklist.md](implementation/backend-checklist.md) (checklist Foundation + mẫu checklist PR) | Case phải có test, kết quả |
| **Trạng thái** | [implementation/implementation-status.md](implementation/implementation-status.md) – **nguồn duy nhất** · [implementation/backend-known-issues.md](implementation/backend-known-issues.md) (KI-) | Cái gì đã có, rủi ro đã biết |
| *Lịch sử* | [implementation/backend-progress.md](implementation/backend-progress.md) · [implementation/scope-review-2026-11-04.md](implementation/scope-review-2026-11-04.md) · [implementation/role-scope-review-2026-11-04.md](implementation/role-scope-review-2026-11-04.md) · các mục "Historical" / "R2" trong file khác | Chỉ tham khảo, không làm theo |

## 2. Thứ tự ưu tiên khi tài liệu mâu thuẫn

1. `training-doc.md`: công nghệ, phiên bản, kiến trúc.
2. Quyết định đã chốt (D-) trong `open-questions.md`. Quyết định mới hơn thay quyết định cũ, có ghi "Thay / Sửa".
3. Đặc tả nghiệp vụ: FR / NFR, BR, roles-and-permissions.
4. Hợp đồng: `backend-api.md` + `api-conventions.md`; dữ liệu: `database/`.
5. Giao diện: Canvas v4.0 + `ui-design/`. Màn hình chỉ hiển thị trường có trong hợp đồng API.
6. Kế hoạch, trạng thái: `implementation/`.

Thấy mâu thuẫn thì **không tự đoán**. Ghi vào `open-questions.md` (Q- hoặc PD-), hỏi lead / PO, rồi mới sửa các tài liệu liên quan trong cùng một PR.

## 3. Mã định danh

| Mã | Nghĩa | Nơi định nghĩa |
|---|---|---|
| FR-, NFR- | Yêu cầu chức năng / phi chức năng | functional-requirements.md |
| BR-, P- | Quy tắc / quy trình nghiệp vụ | business-rules-and-processes.md |
| D-, PD-, Q- | Quyết định đã chốt / chờ quyết định / câu hỏi mở | open-questions.md |
| DD-, TD- | Quyết định thiết kế DB / kỹ thuật | database-design-decisions.md, backend-technical-decisions.md |
| KI- | Vấn đề / giới hạn đã biết | backend-known-issues.md |
| UF- | Luồng người dùng | ui-design/user-flows.md |
| A-, G-, S-, T-, X-, DS- | Mã màn hình. Dùng đặt tên thư mục / component | ui-design/sitemap-and-navigation.md |
| M0 → M8 | Module triển khai | module-plan.md |
| T-, S-, TR-, P-, G-, M-, R-, X- (bảng test) | Test case 2.0 | test-cases.md mục A |

Lưu ý: tiền tố `T-` / `S-` / `G-` vừa là mã màn hình vừa là mã test case. Khi viết luôn ghi rõ "màn hình S-02" hoặc "case S-02". Mã quyết định của Canvas (D-36 Mobile, D-37 ClassFund) tương ứng **D-37, D-38** trong `open-questions.md`.

## 4. Trạng thái dùng chung

`Not Implemented` (chưa có code) · `Partially Implemented` (có một phần – ghi rõ phần thiếu) · `Implemented` (có code + test tự động) · `Pending Decision` (chờ PD-) · `Passed` / `Failed` (kết quả test case trên codebase hiện tại) · *Historical* (của mã nguồn đã bỏ – chỉ tham khảo).

Trạng thái chỉ đổi khi có code + test trên **codebase hiện tại**. `implementation-status.md` là nguồn, các file khác ghi theo.

## 5. Quy tắc viết và cập nhật

- Đầu file có dòng `> vX.Y · yyyy-mm-dd · …` (phiên bản, ngày, phạm vi). Cuối file có bảng "Lịch sử thay đổi".
- **Không xóa dòng đặc tả cũ.** Đánh dấu *(Thay yyyy-mm-dd bằng …)* / *(Sửa yyyy-mm-dd: …)* hoặc gạch ngang, rồi thêm dòng mới.
- Một sự thật chỉ ở một nơi. Nơi khác chỉ dẫn link, ví dụ quy ước lỗi ở `api-conventions.md`, trạng thái ở `implementation-status.md`.
- Ngày tuyệt đối (`2026-11-09`), không ghi "hôm nay", "tuần sau". Viết tiếng Việt; mã, tên trường, endpoint giữ nguyên tiếng Anh trong dấu `` ` ``.
- Không ghi secret, connection string, license key vào tài liệu.

## 6. Tài liệu trong vòng đời một chức năng (module)

| Bước | Đọc | Cập nhật (cùng PR với code) |
|---|---|---|
| 1. Nhận module | module-plan.md (mục của module, phân công Dev A / Dev B), foundation-guide.md | implementation-status.md 1a: tên nhánh, "đang làm" |
| 2. Phân tích | FR / BR / roles, test-cases (cột Module), Canvas + screen-specifications, user-flows | Mâu thuẫn / thiếu → open-questions.md |
| 3. Hợp đồng | backend-api.md, api-conventions.md, database/ | backend-api.md (nếu đổi, có lý do), snapshot `Contracts/snapshots/<Tag>.json` |
| 4. Code + test | foundation-guide.md mục 2 → 4, backend-technical-decisions.md (TD có phạm vi = module) | TD- mới nếu có quyết định kỹ thuật mới |
| 5. Nghiệm thu | Definition of Done (foundation-guide.md 3.7), checklist PR (backend-checklist.md) | implementation-status.md (1a, 3a), test-cases.md (Trạng thái), functional-requirements.md (Trạng thái), backend-known-issues.md (nếu có) |

## 7. Lịch sử thay đổi

| Ngày | Thay đổi |
|---|---|
| 2026-11-09 | Tạo chỉ mục và chuẩn tài liệu. Rà soát toàn bộ docs (trừ ui-design) theo D-39: trạng thái về Foundation; thêm cột Module (FR, test case, known issue), phạm vi TD; thêm D-37 → D-40 |
