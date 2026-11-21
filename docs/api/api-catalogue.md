# Danh mục API (dự kiến)

> Hợp đồng **dự kiến** để hai developer làm song song. Module làm xong thì cập nhật cột "Trạng thái" và chi tiết request/response thật.
> Tiền tố `/api/v1`; `{f}` = `funds/{fundId}`. Permission theo `docs/business-analysis/roles-and-permissions.md`.

| Trạng thái | Ký hiệu |
|---|---|
| Foundation đã có | ✅ |
| Chưa làm | ⬜ |

## Foundation

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/auth/antiforgery` | Anonymous | – | ✅ |
| GET | `/api/health` (không version) | Anonymous | – | ✅ |

## M1 – Auth & tài khoản

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| POST | `/auth/register` `{ fullName, email, password }` → 201 `{ antiforgeryToken, user }` | Anonymous (rate limit) | FR-01 | ✅ |
| POST | `/auth/login` `{ email, password }` → `{ antiforgeryToken, user }` | Anonymous (rate limit) | FR-02 | ✅ |
| POST | `/auth/logout` → 204 | Đăng nhập, AllowTemporaryPassword | FR-02 | ✅ |
| GET | `/auth/me` | Đăng nhập, AllowTemporaryPassword | FR-03 | ✅ |
| PUT | `/auth/me` `{ fullName }` → user (còn mật khẩu tạm → 403 `password.change_required`) | Đăng nhập | FR-04 | ✅ |
| POST | `/auth/change-password` `{ currentPassword, newPassword }` → `{ antiforgeryToken, user }` | Đăng nhập, AllowTemporaryPassword | FR-03, FR-04 | ✅ |
| PUT | `/auth/me/avatar` (multipart `file`; ảnh JPG/PNG/WEBP ≤ 2 MB) → `{ attachmentId }`; thay ảnh cũ, xóa object cũ. 422 `file.invalid` | Đăng nhập | FR-04 | ✅ M5 (`AvatarController`) |
| GET | `/users/{userId}/avatar` (stream ảnh; chính mình hoặc người cùng quỹ đang hoạt động, không thì / chưa có ảnh → 404) | Đăng nhập | FR-04 | ✅ M5 (`AvatarController`) |

> Hai route ảnh đại diện do M5 cài trong `Features/Attachments/AvatarController` – `AuthController` của M1 không khai báo lại.

Mã lỗi M1: `auth.invalid_credentials` (401, dùng chung cho email lạ và mật khẩu sai), `auth.account_disabled` (401, chỉ báo sau khi mật khẩu đúng), `auth.email_exists` (409), `auth.password_policy` / `auth.invalid_email` / `auth.invalid_name` (422; form gửi sai thì 400 `validation_failed` kèm `errors`), `auth.current_password_incorrect` (**422**, không phải 401 để client không coi là hết phiên), `auth.password_unchanged` (422), `auth.rate_limited` (429).

`GET /auth/me` → `{ id, email, fullName, mustChangePassword, accountPermissions[], memberships[{ fundId, fundName, role, permissions[] }] }` (khớp `core/auth/auth.models.ts`).

## M2 – Quỹ

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/funds` → `[{ id, name, schoolYear, status, myRole, memberCount, generalBalance, pendingPaymentCount? }]` (chỉ quỹ mình là thành viên Active; `generalBalance` chỉ tính EventId null; `pendingPaymentCount` chỉ có với người có `Payment.Confirm`) | Đăng nhập | FR-08 | ✅ |
| POST | `/funds` `{ name (≤100), schoolYear ("2026-2027") }` → 201 `FundDto`; người tạo là Owner + danh mục mặc định, audit `fund.create` | `Fund.Create` (cấp tài khoản) | FR-05 | ✅ |
| GET | `/{f}` → `{ id, name, schoolYear, status, createdAt, ownerName, memberCount, myRole }` | `Fund.View` | FR-07 | ✅ |
| PUT | `/{f}` `{ name, schoolYear }` → `FundDto`; audit `fund.update` (before/after); 422 `fund.archived` | `Fund.Manage` | FR-06 | ✅ |

## M3 – Học sinh

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/members?search=&role=&status=&page=&pageSize=` → `PagedResult<{ id, userId, fullName, email, role, status, mustChangePassword, joinedAt, removedAt, canManageAccount }>` (mặc định `status=Active`; thứ tự Owner → Treasurer → học sinh theo tên) | `Member.Manage` | FR-11 | ✅ |
| POST | `/{f}/members` `{ email, fullName }` → 201 `{ email, outcome, member, temporaryPassword?, message? }` (đã là thành viên → 409 `member.already_exists`; quỹ Archived → 422 `fund.archived`) | `Member.Manage` | FR-09 | ✅ |
| POST | `/{f}/members/bulk` `{ members[≤50]: [{ email, fullName? }] }` (UI: "Thêm nhiều học sinh", mỗi dòng `email Họ tên`) → `{ results[{ email, outcome (`Created`/`ExistingAdded`/`Reactivated`/`AlreadyExists`/`Invalid`), member?, temporaryPassword?, message? }] }` (mỗi email độc lập; mật khẩu tạm trả một lần; tên của tài khoản mới = `fullName` nhập kèm (tối đa 100, quá dài → dòng `Invalid`), bỏ trống thì lấy phần trước `@`; thêm / thêm lại thành viên thì tự có nghĩa vụ của các khoản thu chung đang mở; rate limit 10 lần/phút/người dùng → 429) | `Member.Manage` | FR-09, OQ-20 | ✅ |
| PUT | `/{f}/members/{memberId}` `{ fullName }` → member (AZ-02, AZ-07) | `Member.Manage` | FR-11 | ✅ |
| POST | `/{f}/members/{memberId}/reset-password` → `{ temporaryPassword }` (đổi hash + SecurityStamp + MustChangePassword; AZ-02, AZ-07). Chỉ khi học sinh CHƯA tự đặt mật khẩu – đã đổi mật khẩu thì 422 `member.password_already_set` (áp dụng cả `PUT` sửa tên) | `Member.Manage` | FR-10 | ✅ |
| GET | `/{f}/members/{memberId}/removal-preview` → `{ unpaidCount, pendingPaymentCount, balance, canRemove, blockedReason }` (cảnh báo trước khi ngừng, OQ-15) | `Member.Manage` | FR-11 | ✅ |
| POST | `/{f}/members/{memberId}/remove` → `{ cancelledAssignments }` (422 `member.has_balance` / `member.pending_payments` / `member.cannot_remove_owner`; 409 `member.already_removed`) | `Member.Manage` | FR-11 | ✅ |

## M4 – Thủ quỹ

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/treasurers` → `[{ memberId, userId, fullName, email, role (Owner/Treasurer), joinedAt, grantedAt? }]` (thủ quỹ chính trước; `grantedAt` lấy từ nhật ký, null với thủ quỹ chính) | `Treasurer.Manage` | FR-12 | ✅ |
| POST | `/{f}/treasurers` `{ memberId }` → 201 TreasurerDto (404 không thuộc quỹ; 409 `treasurer.already_treasurer`; 422 `treasurer.member_not_active` / `fund.archived`) | `Treasurer.Manage` | FR-12 | ✅ |
| POST | `/{f}/treasurers/{memberId}/revoke` → TreasurerDto (422 `treasurer.cannot_revoke_owner` – AZ-03; 409 `treasurer.not_treasurer`) | `Treasurer.Manage` | FR-12, FR-13 | ✅ |

## M5 – Danh mục & chứng từ

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/categories?type=&activeOnly=` → `[{ id, name, type, isSystem, isActive, usageCount }]` | `Fund.View` | FR-14 | ✅ |
| POST | `/{f}/categories` `{ name (≤100), type }` → 201; 409 `category.duplicate_name` (trùng tên cùng loại, không phân biệt hoa thường); 422 `fund.archived` | `Category.Manage` | FR-14 | ✅ |
| PUT | `/{f}/categories/{id}` `{ name, isActive }` – đổi tên, ngừng dùng / dùng lại (không có xóa) | `Category.Manage` | FR-14 | ✅ |
| POST | `/{f}/attachments` (multipart: `file`, `kind` = `Receipt`/`PaymentProof`/`Document`) → 201 `{ id, kind, fileName, contentType, sizeBytes, uploadedAt }`; file lên Neon Object Storage, `OwnerId` null tới khi gắn. Kiểm loại + dung lượng + chữ ký đầu file → 422 `file.invalid`; quỹ Archived → 422 `fund.archived`. Đồng thời dọn file chưa gắn > 24h của quỹ | Route: `Fund.View`; service: `Ledger.Manage` (Receipt, Document) hoặc `Payment.Submit` (PaymentProof), thiếu → 403 | FR-15 | ✅ |
| GET | `/{f}/attachments/{id}` (stream file qua API, `nosniff`, `no-store`; kiểm AZ-05 – không được xem → 404; không trả URL bucket) | `Fund.View` | FR-17 | ✅ |

**Dùng trong module khác (không phải endpoint):** `IAttachmentsService.AttachToAsync(fundId, ids, ownerId, allowedKinds)` – gọi trong transaction của M6/M7 để gắn file (file phải thuộc quỹ, chưa gắn, do chính người dùng tải lên, đúng loại; không thì 422 `attachment.unusable`), `ListByOwnerAsync`; `ICategoriesService.EnsureUsableAsync` – 422 `category.unusable`.

## M6 – Khoản thu & thanh toán

M6a (✅) chỉ làm phạm vi **quỹ chung** (`EventId = null`); khoản thu sự kiện và đóng bằng số dư do M6b. Danh sách phân trang theo `PagedResult` (`items`, `total`, `page`, `pageSize`).

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/charges?status=&page=&pageSize=` → `PagedResult<{ id, title, purpose, amountPerPerson, dueDate, status, createdAt, categoryId, categoryName, progress?, mine? }>`; `progress` = `{ assignedCount, paidCount, awaitingCount, collectedAmount }` chỉ có với `Charge.Manage`; `mine` = nghĩa vụ của chính người xem (null nếu không có). Khoản đang mở lên trước. Phạm vi sự kiện (`scope`/`eventId`) thêm ở M6b | `Fund.View` | FR-18 | ✅ |
| GET | `/{f}/charges/{id}` → như một phần tử ở trên (404 nếu không phải khoản thu chung của quỹ) | `Fund.View` | FR-18 | ✅ |
| POST | `/{f}/charges` `{ title, purpose?, amountPerPerson, dueDate, categoryId }` → 201 (`categoryId` bắt buộc: danh mục THU đang dùng của quỹ, do thủ quỹ tự tạo ở mục Danh mục; 422 `category.unusable`). Tạo nghĩa vụ cho mọi thành viên đang hoạt động (gồm thủ quỹ) + thông báo `GeneralChargeCreated`. 422 `charge.due_date_in_past` / `money.invalid` / `fund.archived` | `Charge.Manage` | FR-18 | ✅ |
| GET | `/{f}/charges/{id}/assignments?status=&search=&page=&pageSize=` → `PagedResult<{ assignmentId, userId, fullName, email, role, amountDue, status, isSelfDeclared, paymentId?, method?, paymentAt?, hasProof, rejectionReason? }>` (đã đóng / chưa đóng; chưa đóng lên trước; không gồm nghĩa vụ đã hủy) | `Charge.Manage` | FR-24 | ✅ |
| POST | `/{f}/charges/{id}/remind` → `{ reminded }`: thông báo `ChargeDueSoon` cho người CHƯA đóng (không nhắc người đang chờ xác nhận); 409 `charge.remind_too_soon` (1 giờ/khoản), 422 `charge.nothing_to_remind`, 409 `charge.invalid_status` | `Charge.Manage` | – | ✅ |
| POST | `/{f}/charges/{id}/close` → khoản thu `Closed` (409 `charge.invalid_status` nếu không còn mở). `/cancel` → `Cancelled` khi chưa ai nộp (422 `charge.has_payments`), mọi nghĩa vụ → `Cancelled` | `Charge.Manage` | – | ✅ |
| GET | `/{f}/me/assignments` → `[{ assignmentId, chargeId, title, purpose, amountDue, dueDate, chargeStatus, status, paymentId?, method?, paymentStatus?, rejectionReason?, isSelfDeclared }]` (chỉ của chính mình, không gồm đã hủy) | `Payment.Submit` | FR-20, FR-23 | ✅ |
| POST | `/{f}/me/assignments/{id}/payments` `{ method: Cash \| BankTransfer \| PersonalBalance, transferCode?, attachmentId? }` → 201 `{ id, assignmentId, amount, method, transferCode, status, submittedAt, isSelfDeclared }`. Chuyển khoản bắt buộc `attachmentId` (PaymentProof của chính mình, BR-11) → 422 `payment.proof_required`; nghĩa vụ người khác → 404; đã nộp / đang chờ → 409 `payment.not_payable`; khoản thu đã đóng → 409 `payment.charge_not_open`. Người có `Payment.Confirm` nộp cho mình = tự xác nhận (`status` = Confirmed, `isSelfDeclared`). `PersonalBalance` (OQ-10, khoản chung và sự kiện): trừ số dư (`IBalanceService.DebitAsync(ChargePayment)`) + Payment `Confirmed` (`Verified`) + bút toán thu đúng phạm vi trong CÙNG transaction, không cần thủ quỹ xác nhận, audit `payment.pay_with_balance`; số dư thiếu → 422 `balance.insufficient`; đang chờ rút → 422 `balance.locked_by_withdrawal` (BR-26) | `Payment.Submit` | FR-20, FR-22 | ✅ |
| POST | `/{f}/assignments/{assignmentId}/record-cash` → 201 như nộp tiền: thủ quỹ ghi nhận học sinh đã đưa tiền mặt (gửi + xác nhận, `Verified`, thông báo `PaymentConfirmed`); ghi cho chính mình = tự ghi nhận. 409 `payment.not_payable` (đã đóng / đang chờ), 409 `payment.charge_not_open` | `Payment.Confirm` | FR-21, FR-22 | ✅ |
| GET | `/{f}/payments?status=Submitted&page=&pageSize=` → `PagedResult<{ id, assignmentId, chargeId, chargeTitle, payerUserId, payerName, payerEmail, amount, method, transferCode, status, submittedAt, confirmedAt, isSelfDeclared, rejectionReason, attachments[] }>` (mặc định hàng chờ `Submitted`; cũ nhất lên trước) | `Payment.Confirm` | FR-21 | ✅ |
| POST | `/{f}/payments/{id}/confirm` → ghi bút toán thu (`Source=Payment`, danh mục = danh mục của khoản thu; khoản cũ chưa có danh mục dùng "Đóng góp quỹ lớp" nếu còn), nghĩa vụ → Paid, thông báo `PaymentConfirmed`. 409 `payment.already_processed` (khóa dòng chống xác nhận đôi) | `Payment.Confirm` | FR-21 | ✅ |
| POST | `/{f}/payments/{id}/reject` `{ reason }` (bắt buộc, ≤ 500) → nghĩa vụ về Unpaid để nộp lại, thông báo `PaymentRejected` kèm lý do | `Payment.Confirm` | FR-21 | ✅ |

## M7 – Thu chi

> ✅ 2026-11-19 (nhánh `feature/m7-m8-events-transactions`). Một phạm vi mỗi lần: không có `eventId` = quỹ chung (không gồm giao dịch sự kiện, BR-01). Tổng: bút toán đảo trừ vào loại của dòng gốc nên "tổng chi" không tính khoản đã đảo (`LedgerBalances`).

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/ledger-entries?eventId=&type=&categoryId=&from=&to=&page=&pageSize=` → `{ items, total, page, pageSize, summary: { balance, totalIncome, totalExpense } }`; `balance` toàn thời gian, tổng thu / chi trong khoảng ngày lọc. Học sinh: chỉ dòng `Source = Manual` (chi + bút toán đảo – không lộ ai đã đóng, OQ-14); sổ sự kiện chỉ người đã nhận tham gia (không thì 404) | `Fund.View` | FR-28 | ✅ |
| POST | `/{f}/ledger-entries` `{ eventId?, categoryId, title, purpose?, amount, transactionDate, note?, attachmentIds[] }` → 201 chi tiết – **chỉ ghi chi** (OQ-18, BR-39). Khóa dòng Fund (+ Event) rồi kiểm: quỹ `Active`; sự kiện thuộc quỹ và `Open` (422 `ledger.event_closed`); danh mục chi đang dùng (422 `category.unusable`); ngày không ở tương lai giờ VN (422 `ledger.future_date`); chi ≤ số dư phạm vi (422 `ledger.exceeds_balance`); > 500.000đ phải có chứng từ (422 `evidence.required`); chứng từ do chính mình tải, chưa gắn (422 `attachment.unusable`). Audit `ledger.create` đúng Scope / EventId | `Ledger.Manage` | FR-26, FR-27, FR-16 | ✅ |
| GET | `/{f}/ledger-entries/{id}` → `{ entry, purpose, note, performedByRole, attachments[], reversedBy?, reverses?, canReverse, history[] }` (`history` từ nhật ký, chỉ khi có `Audit.View`) | `Fund.View` | FR-27 | ✅ |
| POST | `/{f}/ledger-entries/{id}/reverse` `{ reason }` → 201 dòng đảo (Thu cùng số tiền, cùng phạm vi, `ReversesEntryId`, lý do ở `note`). Chỉ khoản chi ghi tay chưa đảo (422 `ledger.not_reversible`); đã đảo → 409 `ledger.already_reversed`; sự kiện đã quyết toán / hủy → 422 `ledger.scope_locked`. Audit `ledger.reverse` | `Ledger.Manage` | FR-29 | ✅ |

## M8 – Sự kiện

> ✅ 2026-11-19. Học sinh chỉ thấy sự kiện mình được mời (không thì 404). Khoản thu / thanh toán sự kiện (M6b) chỉ được đọc để tính tình trạng đóng tiền.

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/events` → thẻ E-01: `invitedCount, participantCount, paidInFullCount, totalIncome, totalExpense, balance, myResponse` | `Event.Participate` | FR-30, FR-34 | ✅ |
| POST | `/{f}/events` `{ name, description?, eventDate }` → 201; mời mọi thành viên `Active` (`Pending`), thông báo `EventCreated` cho người khác, audit `event.create` | `Event.Manage` | FR-30 | ✅ |
| GET | `/{f}/events/{eventId}` → chi tiết + KPI + `me` + `myCharges` (của tôi) + `charges` (thủ quỹ: số đã đóng / chờ / chưa đóng) | `Event.Participate` | FR-34 | ✅ |
| PUT | `/{f}/events/{eventId}` `{ name, description?, eventDate }` – không sửa khi `Settled` / `Cancelled` (422 `event.not_editable`) | `Event.Manage` | FR-30 | ✅ |
| POST | `/{f}/events/{eventId}/cancel` `{ reason }` – chỉ `Open` và chưa có dòng sổ / thanh toán chưa bị từ chối (422 `event.has_money`); khoản thu + nghĩa vụ chưa đóng bị hủy theo | `Event.Manage` | FR-30, BR-16 | ✅ |
| GET | `/{f}/events/{eventId}/completion-preview` → `{ excluded[], underpaid[{ fullName, missing }], pendingPaymentCount, remainingCount }` | `Event.Manage` | FR-33 | ✅ |
| POST | `/{f}/events/{eventId}/complete` – BR-19: người `Accepted` chưa đóng khoản thu nào (không `Paid`, không thanh toán chờ / đã xác nhận – kể cả khi chưa có khoản nào áp cho họ) bị loại (`IsFinal = false`, nghĩa vụ `Unpaid` → `Excluded`, thông báo `EventParticipantExcluded`); người còn nghĩa vụ `Unpaid` được nhắc (`EventUnpaidReminder`). Đã đóng ít nhất một khoản thì ở lại dù chưa đủ. Sai trạng thái → 422 `event.invalid_transition` | `Event.Manage` | FR-33 | ✅ |
| GET | `/{f}/events/{eventId}/participants?paymentStatus=` (`Paid` / `Pending` / `Unpaid` / `NoCharge`) – chỉ người `Accepted` (sau khi kết thúc: `IsFinal`); thủ quỹ có `paid / due / paymentStatus / hasSelfDeclared`; học sinh chỉ có tên | `Event.Participate` | FR-32, FR-34 | ✅ |
| PUT | `/{f}/events/{eventId}/me/response` `{ response }` (`Accepted` / `Declined`) – chỉ khi `Open` (422 `event.not_open`); đã nộp tiền thì không đổi sang không tham gia (422 `event.response_locked`); nhận tham gia muộn được áp các khoản thu đang mở, không tham gia thì nghĩa vụ chưa đóng bị hủy | `Event.Participate` | FR-31, BR-17, BR-18 | ✅ |

Nội bộ cho M3 (TD-10): `IEventInvitationService.InviteToOpenEventsAsync` – thêm / kích hoạt lại thành viên thì được mời vào các sự kiện `Open` (audit `event.invite`).

## M9 – Quyết toán

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/events/{eventId}/settlement/preview?surplusMethod=` (luôn chia theo tỷ lệ đã đóng, OQ-18) | `Settlement.Execute` | FR-35, FR-36 | ⬜ |
| POST | `/{f}/events/{eventId}/settlement` | `Settlement.Execute` | FR-35–FR-39 | ⬜ |
| GET | `/{f}/events/{eventId}/settlement` | `Event.Participate` | FR-50 | ⬜ |
| POST | `/{f}/events/{eventId}/settlement/lines/{lineId}/refunded` | `Settlement.Execute` | FR-37 | ⬜ |

## M10 – Số dư cá nhân

> ✅ 2026-11-21 (nhánh `feature/m10-m13-balance-reports`). Số dư là tiền giữ hộ, không ghi `LedgerEntry` (BR-26); khóa dòng `PersonalBalance` khi cộng / trừ. Nội bộ cho M6b / M9 / M14: `IBalanceService.CreditAsync` / `DebitAsync` (trong transaction của module gọi; đóng khoản thu bằng số dư khi có yêu cầu rút chờ → 422 `balance.locked_by_withdrawal`; không đủ → 422 `balance.insufficient`).

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/me/balance` → `{ fundName, balance, latestWithdrawal?, history[{ type, amount, balanceAfter, createdAt, description, scope, eventName }], otherFunds[] }` | `Balance.ViewOwn` | FR-40, FR-42 | ✅ |
| POST | `/{f}/me/withdrawal-requests` (không body – rút toàn bộ số dư, OQ-11) → 201 như GET. 422 `withdrawal.nothing_to_withdraw` / `withdrawal.pending_exists`. Quỹ `Archived` vẫn rút được (BR-37) | `Balance.Withdraw` | FR-41 | ✅ |
| GET | `/{f}/withdrawal-requests?status=Requested` (`Paid` / `Rejected`) → `{ items[{ fullName, email, amount, status, requestedAt, handledAt, handledByName, rejectionReason, isMine }], requestedCount, paidCount, rejectedCount }` | `Balance.Handle` | FR-41 | ✅ |
| POST | `/{f}/withdrawal-requests/{id}/paid` – trừ đúng số tiền yêu cầu, `BalanceTransaction(Withdrawal)`, thông báo `WithdrawalProcessed`, audit `withdrawal.paid` (`IsSelfAction` khi tự xử lý của mình); đã xử lý → 409 `withdrawal.not_pending` · `/reject` `{ reason }` – lý do bắt buộc, số dư mở lại | `Balance.Handle` | FR-41 | ✅ |

## M11 – Thông báo

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/me/notifications?unreadOnly=` | `Fund.View` | FR-44 | ⬜ |
| GET | `/{f}/me/notifications/unread-count` | `Fund.View` | FR-44 | ⬜ |
| POST | `/{f}/me/notifications/{id}/read` · `/me/notifications/read-all` | `Fund.View` | FR-44 | ⬜ |

## M12 – Audit log

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/audit-logs?from=&to=&actorId=&scope=&eventId=&action=&selfOnly=&page=&pageSize=` → `PagedResult<{ id, at, actorUserId, actorName, action, scope, eventId, eventName, entityType, entityId, summary, isSelfAction, note, before, after, ipAddress }>`. Mới nhất trước; `from`/`to` là ngày giờ VN (`yyyy-MM-dd`, tính trọn ngày); `action` kết thúc bằng "." lọc cả nhóm (`member.`); `before`/`after` là JSON đã lưu; `ipAddress` đã che (`113.190.xx.xx`); `summary` dựng sẵn cho cột "Chi tiết". 400 khi `to < from`, `pageSize > 100`, hoặc `eventId` với `scope=General` | `Audit.View` (học sinh → 403) | FR-45–FR-47 | ✅ |
| GET | `/{f}/audit-logs/actors` → `[{ userId, fullName }]` – người đã có thao tác trong quỹ (ô "Người thực hiện") | `Audit.View` | FR-47 | ✅ |

`summary` do `AuditSummary` dựng từ Before/After/Note, không cần biết từng hành động: lấy trường đối tượng (`name`, `fullName`, `title`, `email`, `fileName`…), số tiền (`amount`, `amountDue`, `total`…), trường phụ (`role`, `kind`, `type`, `method`, `response`, `count`, `reason`), giá trị đổi hiện "cũ → mới". **Module mới đặt tên trường trong `After` theo các khóa này là cột Chi tiết tự hiện**; nhãn tiếng Việt của mã hành động (`AUDIT_ACTION_LABEL`), của trường và giá trị enum trong Before/After (`AUDIT_FIELD_LABEL`, `AUDIT_VALUE_LABEL`) thêm vào `ClassFundUI/src/app/features/audit/audit-format.ts` – hộp "Trước / sau" hiện bảng so sánh tiếng Việt, không hiện JSON; trường mã định danh (`id`, `…Id`) tự ẩn. `ipAddress` của máy nội bộ (127.0.0.1, ::1) trả "nội bộ (localhost)".

## M13 – Báo cáo

> ✅ 2026-11-21. Chỉ đọc; số liệu quỹ chung chỉ lấy `EventId = null`, sự kiện chỉ lấy đúng sự kiện (BR-02); thu tự ghi nhận tách riêng (BR-13); bút toán đảo trừ vào loại của dòng gốc.

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/reports/overview` → S-01: `general { balance, totalIncome, totalExpense, selfDeclaredIncome, incomeCount, expenseCount }`, `memberCount`, `treasurerCount`, `heldBalance` (tiền giữ hộ), `openCharges[]`, `events[]`, `me { dueAmount, nearestDue…, awaitingAmount, balance }`, `notifications[]` (3 mới nhất của tôi); thủ quỹ có thêm `recentEntries[]` và `todo` (thanh toán chờ, yêu cầu rút chờ, sự kiện chờ quyết toán) | `Fund.View` | FR-48 | ✅ |
| GET | `/{f}/reports/general?from=&to=` → tổng thu / chi trong khoảng ngày, `balance` toàn thời gian, `expenseByCategory[]`, `months[{ year, month, income, expense, closingBalance }]`, `openChargeCount`; 400 khi `to < from` | `Report.View` | FR-49 | ✅ |
| GET | `/{f}/reports/events` → mỗi sự kiện: tham gia / sau kết thúc, cần thu, đã thu, chưa thu, tổng chi, tiền thừa, cách xử lý (từ Settlement nếu có) | `Report.View` | FR-50 | ✅ |
| GET | `/{f}/reports/students` → mỗi thành viên: đã đóng quỹ chung, khoản chung chưa đóng, đã đóng sự kiện, được hoàn, đã dùng số dư, số dư | `Report.View` | FR-51 | ✅ |
| GET | `/{f}/reports/me` – dòng báo cáo của chính tôi (học sinh chỉ xem của mình) | `Fund.View` | FR-51 | ✅ |
| – | S-03 dùng chung E-03 (`/reports/events/:id` chuyển sang trang sự kiện); S-05 tổng kết cuối năm làm cùng M14 | – | FR-50, FR-52 | – |

## M14 – Quyết toán cuối năm quỹ chung

| Method | Path | Permission | FR | Trạng thái |
|---|---|---|---|---|
| GET | `/{f}/year-end-settlement/preview?surplusMethod=&allocation=` | `Settlement.Execute` | FR-53 | ⬜ |
| POST | `/{f}/year-end-settlement` (một lần; quỹ → Archived) | `Settlement.Execute` | FR-53, FR-54 | ⬜ |
| GET | `/{f}/year-end-settlement` | `Fund.View` | FR-53 | ⬜ |
| POST | `/{f}/year-end-settlement/lines/{lineId}/refunded` | `Settlement.Execute` | FR-53 | ⬜ |
