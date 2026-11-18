# Quy ước API

## 1. URL & phiên bản

- Tiền tố: `/api/v1` (`ApiRoutes.Prefix`, versioning theo URL segment).
- Tài nguyên theo quỹ: `/api/v1/funds/{fundId}/...` (`ApiRoutes.Fund`). `fundId` chỉ **chọn quỹ**; quyền do backend suy ra từ `FundMember`.
- Tài nguyên theo người dùng hiện tại: `/me/...` dưới quỹ (ví dụ `/funds/{fundId}/me/assignments`), không truyền userId.
- Danh từ số nhiều, kebab-case: `/charges`, `/ledger-entries`, `/withdrawal-requests`.
- Hành động chuyển trạng thái: `POST /{id}/<động-từ>` (`/confirm`, `/reject`, `/complete`).
- Phạm vi tài chính truyền bằng query/body `scope=General|Event` + `eventId` khi `Event`; hoặc route con `/events/{eventId}/...`.

## 2. Định dạng

- JSON camelCase; enum là **chuỗi** (`"Confirmed"`), không nhận số.
- Tiền: số nguyên VND (`250000`). Ngày nghiệp vụ: `"yyyy-MM-dd"`. Thời điểm: ISO 8601 UTC (`"2026-11-16T03:00:00Z"`).
- Danh sách: `?page=1&pageSize=20` → `{ items, total, page, pageSize }`.
- Upload chứng từ: `multipart/form-data`, trường `file` (+ `kind`), tối đa 5 MB.

## 3. Mã trạng thái & lỗi

Mọi lỗi là `application/problem+json`:

```json
{ "status": 422, "title": "Giao dịch trên 500.000đ bắt buộc có chứng từ.", "code": "evidence.required",
  "traceId": "...", "correlationId": "...", "errors": { "amount": ["..."] } }
```

| Status | Khi nào | `code` |
|---|---|---|
| 200 / 201 / 204 | Thành công (201 khi tạo, kèm body) | – |
| 400 | Dữ liệu không hợp lệ (FluentValidation, JSON sai) | `validation_failed` (+ `errors`) |
| 400 | Thiếu/sai header antiforgery | `antiforgery_failed` |
| 401 | Chưa đăng nhập, phiên hết hiệu lực, sai thông tin đăng nhập | `unauthorized`, `auth.invalid_credentials`, `auth.account_disabled` |
| 403 | Thiếu permission / quyền theo dữ liệu / còn mật khẩu tạm | `forbidden`, `password.change_required` |
| 404 | Không tồn tại **hoặc không thuộc phạm vi người xem** | `not_found` |
| 409 | Xung đột trạng thái / trùng | `<module>.<reason>` |
| 422 | Vi phạm quy tắc nghiệp vụ | `<module>.<reason>` |
| 429 | Quá giới hạn đăng nhập/đăng ký | `auth.rate_limited` |
| 500 | Lỗi không lường trước (không lộ chi tiết) | `internal_error` |

## 4. Bảo mật

- Cookie phiên `cf_session` (HttpOnly, SameSite=Strict, Secure ngoài dev). Không có token trong localStorage.
- Request ghi (POST/PUT/PATCH/DELETE) phải gửi `X-XSRF-TOKEN` (lấy từ `GET /auth/antiforgery` hoặc response đăng nhập/đăng ký/đổi mật khẩu).
- Endpoint đăng nhập/đăng ký: `[EnableRateLimiting(RateLimitPolicies.Auth)]`.
- Endpoint dùng được khi còn mật khẩu tạm: đánh dấu `[AllowTemporaryPassword]`.
- Header `X-Correlation-Id` luôn có trong response; ghi kèm log và audit.

## 5. Swagger

`/swagger` (Development). Dán `antiforgeryToken` vào nút Authorize để thử request ghi.
