# Quy tắc & ràng buộc code

> Áp dụng cho cả người và Claude Code. Vi phạm mục **[Bắt buộc]** thì PR không được merge.

## 1. Chung

- **[Bắt buộc]** Không đổi tech stack / phiên bản / kiến trúc khác `training-doc.md`. Không thêm package khi task không yêu cầu (hỏi trước).
- **[Bắt buộc]** Không commit secret: connection string, `SECRET_KEY`, mật khẩu Neon, license Kendo, file `.env`, `App_Data/`, `wwwroot/` build.
- **[Bắt buộc]** Làm trên nhánh `feature/<tên>`; không commit thẳng `main`. Commit message: `feat:`, `fix:`, `docs:`, `chore:`, `test:`, `refactor:`.
- Chỉ làm đúng phạm vi task. Thấy vấn đề ngoài phạm vi → ghi vào `implementation-status.md` mục "Việc tồn đọng".
- Tên code tiếng Anh; comment, thông báo lỗi, UI tiếng Việt.

## 2. Backend (.NET 10)

- **[Bắt buộc]** `Nullable` bật, `TreatWarningsAsErrors` – build phải 0 warning (`Directory.Build.props`).
- **[Bắt buộc]** Domain không tham chiếu project/NuGet nào; Application chỉ tham chiếu Domain; Api không dùng EF Core trực tiếp (test kiến trúc kiểm tra).
- **[Bắt buộc]** Mọi endpoint có `[RequirePermission(...)]` hoặc `[AllowAnonymous]` rõ ràng. Không kiểm tra theo tên role trong controller/service – dùng permission; quyền theo dữ liệu ghi rõ mã AZ-xx.
- **[Bắt buộc]** Người dùng hiện tại chỉ lấy từ `ICurrentUser`, **không bao giờ** từ body/query.
- **[Bắt buộc]** Mọi thao tác ghi gọi `AuditTrail.Record` trong cùng transaction. Không ghi mật khẩu/hash/token/cookie vào log hay audit.
- **[Bắt buộc]** Tiền là `long` (VND). Không dùng `double`/`float`/`decimal` cho tiền.
- **[Bắt buộc]** Không UPDATE/DELETE `LedgerEntry`, `AuditLog`, `BalanceTransaction`. Không xóa cứng dữ liệu nghiệp vụ.
- **[Bắt buộc]** Mọi khoản thu/giao dịch xác định phạm vi qua `EventId` (null = quỹ chung); truy vấn báo cáo luôn lọc phạm vi.
- Entity không có navigation property; truy vấn qua `IAppDb.Query<T>()` + projection sang DTO (không trả entity).
- Đọc danh sách lớn phải phân trang (`PageRequest`, `PagedResult<T>`).
- Async xuyên suốt, truyền `CancellationToken`. Không `.Result` / `.Wait()`.
- Mã lỗi dạng `module.reason` (snake_case), ví dụ `payment.already_confirmed`.
- Dùng `TimeProvider` thay `DateTime.Now`.
- Không sửa file Foundation (`Hosting/`, `Common/`, `Authorization/`, `Filters/`, `Middleware/`, `AppDbContext`) trong task module trừ khi task ghi rõ; nếu cần, tách PR "foundation" riêng và báo đồng đội.

## 3. Frontend (Angular 20 + Kendo 23.3)

- **[Bắt buộc]** Standalone component, `ChangeDetectionStrategy.OnPush`, signal; không NgModule, không store toàn cục.
- **[Bắt buộc]** Gọi API qua `apiUrl`/`fundApiUrl`; không hard-code `/api/v1`. Không tự gắn header antiforgery (interceptor làm).
- **[Bắt buộc]** Màu qua token CSS (`--cf-*`), không viết mã màu trong component. Dùng component Kendo cho input/grid/dialog.
- **[Bắt buộc]** Hiển thị phạm vi (`app-scope-badge`) ở mọi danh sách/chi tiết giao dịch, khoản thu, thông báo.
- Mỗi module chỉ sửa `features/<module>/**`; sửa `core/`, `shared/`, `app.routes.ts` cần PR foundation riêng.
- Thông báo lỗi theo `code` qua `registerErrorMessages`, không hiển thị chi tiết kỹ thuật.
- Không lưu dữ liệu nhạy cảm trong `localStorage`.

## 4. Test

- Tên test mô tả hành vi (`Student_cannot_confirm_payments`). Test quy tắc ghi mã BR/AZ trong tên hoặc comment.
- Integration test dùng Testcontainers, **không** trỏ Neon.
- Không merge khi có test đỏ hoặc test bị skip không lý do.

## 5. Claude Code

- Đọc `CLAUDE.md`, `training-doc.md`, task trong `module-plan.md` và tài liệu FR/BR liên quan trước khi code.
- Không tự commit/push, không tự chạy migration lên Neon, không tạo migration khi task không yêu cầu.
- Báo cáo sau mỗi task: file tạo/sửa, test đã chạy (kết quả thật), giới hạn đã tuân thủ, câu hỏi mở.
