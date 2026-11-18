# Yêu cầu chức năng – ClassFund

> Nguồn: bản yêu cầu bài toán (2026-11-16). Mỗi yêu cầu có mã `FR-xx` để truy vết sang quy tắc (`BR-xx`), module (`Mx`), API, màn hình và test.
> Mục "Nguồn" trỏ về số mục trong bản yêu cầu gốc. Điểm chưa rõ ghi ở `open-questions.md` (`OQ-xx`).

## 1. Phạm vi

Hệ thống quản lý quỹ lớp với **hai phạm vi tài chính độc lập**:

| Phạm vi | Mô tả | Quyết toán |
|---|---|---|
| **Quỹ chung** | Thu chi phục vụ hoạt động chung của cả lớp, không gắn sự kiện | Không quyết toán theo từng hoạt động; tổng kết cuối năm |
| **Quỹ sự kiện** | Thu chi riêng của từng sự kiện | Quyết toán riêng khi sự kiện kết thúc: chia lại tiền thừa (hoàn trực tiếp / cộng số dư) theo tỷ lệ số tiền mỗi người đã đóng |

Không tự động gộp giao dịch sự kiện vào quỹ chung, không lấy quỹ chung bù sự kiện, không chuyển tiền thừa sự kiện vào quỹ chung. Ở mọi phạm vi, **tổng chi không bao giờ vượt tổng thu** (OQ-18) nên không có trường hợp quyết toán thiếu.

Người dùng: **Thủ quỹ chính** (Owner – người tạo quỹ), **Thủ quỹ** (được phân quyền), **Học sinh**. Thủ quỹ đồng thời có thể tham gia như học sinh.

## 2. Danh sách yêu cầu

### 2.1. Tài khoản & phiên (M1)

| Mã | Yêu cầu | Nguồn |
|---|---|---|
| FR-01 | Người dùng tự đăng ký tài khoản (email, họ tên, mật khẩu); tài khoản tự đăng ký được tạo quỹ và trở thành thủ quỹ chính của quỹ đó. | 1.1 |
| FR-02 | Đăng nhập bằng email + mật khẩu; đăng xuất. | 1.2 |
| FR-03 | Tài khoản có mật khẩu tạm bắt buộc đổi mật khẩu ở lần đăng nhập đầu; trước khi đổi chỉ được gọi API đổi mật khẩu / xem phiên / đăng xuất. | 1.2 |
| FR-04 | Xem / sửa thông tin cá nhân (họ tên, ảnh đại diện), đổi mật khẩu. | OQ-16 |

### 2.2. Quỹ (M2)

| Mã | Yêu cầu | Nguồn |
|---|---|---|
| FR-05 | Thủ quỹ tạo quỹ (tên, năm học); người tạo là Owner; tạo sẵn danh mục mặc định. | 1.1 |
| FR-06 | Đổi tên / cập nhật thông tin quỹ. | 1.1 |
| FR-07 | Mọi thành viên xem thông tin công khai của quỹ. | 1.2 |
| FR-08 | Người dùng xem danh sách quỹ mình là thành viên. | – |

### 2.3. Học sinh (M3)

| Mã | Yêu cầu | Nguồn |
|---|---|---|
| FR-09 | Thủ quỹ thêm học sinh vào quỹ bằng email; email chưa có tài khoản → tạo tài khoản (Provisioned) với mật khẩu tạm; email đã có → chỉ thêm thành viên. | 1.1 |
| FR-10 | Thủ quỹ cấp lại mật khẩu tạm cho học sinh (hiển thị một lần). | 1.1 |
| FR-11 | Thủ quỹ xem, tìm, sửa họ tên, xóa (ngừng) học sinh khỏi quỹ; dữ liệu tài chính lịch sử được giữ. | 1.1 |

### 2.4. Thủ quỹ (M4)

| Mã | Yêu cầu | Nguồn |
|---|---|---|
| FR-12 | Owner phân quyền một học sinh trong quỹ thành thủ quỹ; thu hồi quyền thủ quỹ. | 1.1 |
| FR-13 | Thủ quỹ được phân quyền **không** thêm/sửa/xóa/thay đổi quyền của thủ quỹ khác (chỉ Owner). | 1.1 |

### 2.5. Danh mục & chứng từ (M5)

| Mã | Yêu cầu | Nguồn |
|---|---|---|
| FR-14 | Quản lý danh mục thu / chi của quỹ (thêm, đổi tên, ngừng dùng). | 6.3 |
| FR-15 | Lưu file thực tế trên Neon Object Storage: ảnh hóa đơn / chứng từ, bill chuyển khoản (ảnh hoặc PDF ≤ 5 MB), ảnh đại diện (≤ 2 MB), tài liệu (PDF/Word/Excel/ảnh ≤ 10 MB); kèm ghi chú, mã giao dịch chuyển khoản. | 6.4, OQ-16 |
| FR-16 | Giao dịch **> 500.000đ bắt buộc** có chứng từ; ≤ 500.000đ được phép đính kèm. | 6.4 |
| FR-17 | Chứng từ được phân quyền hiển thị: thủ quỹ xem tất cả; học sinh xem chứng từ chi công khai và minh chứng của chính mình, không xem minh chứng thanh toán của học sinh khác. | 6.4 |

### 2.6. Khoản thu & thanh toán (M6)

| Mã | Yêu cầu | Nguồn |
|---|---|---|
| FR-18 | Thủ quỹ tạo khoản thu **chung** (tên, mục đích, số tiền mỗi người, hạn đóng) áp cho mọi học sinh đang hoạt động (gồm thủ quỹ). | 2.1, 6.1 |
| FR-19 | Thủ quỹ tạo khoản thu **sự kiện** áp cho người đã xác nhận tham gia sự kiện. | 3.2 |
| FR-20 | Học sinh xem các khoản cần đóng (chung + sự kiện mình tham gia), xác nhận đã nộp, chọn tiền mặt / chuyển khoản, gửi ảnh minh chứng khi chuyển khoản. | 1.2 |
| FR-21 | Thủ quỹ kiểm tra và xác nhận / từ chối (kèm lý do) thanh toán của học sinh; bị từ chối thì học sinh gửi lại. | 3.3 |
| FR-22 | Thủ quỹ tự ghi nhận đã nộp cho bản thân và tự xác nhận (không cần thủ quỹ khác); giao dịch đánh dấu **tự ghi nhận**. | 1.3, 3.3 |
| FR-23 | Học sinh theo dõi trạng thái xác nhận thanh toán. | 1.2 |
| FR-24 | Thủ quỹ theo dõi danh sách đã đóng / chưa đóng theo từng khoản thu. | 2.1 |
| FR-25 | Dùng số dư cá nhân để đóng khoản thu **chung và sự kiện**; ghi nhận giao dịch sử dụng số dư. | 4.1, 5, OQ-10 |

### 2.7. Thu chi (M7)

| Mã | Yêu cầu | Nguồn |
|---|---|---|
| FR-26 | Thủ quỹ ghi thu / chi với phạm vi bắt buộc: quỹ chung **hoặc** một sự kiện (không bao giờ cả hai). Form **thu** và **chi** khác nhau: ghi **thu** = tạo khoản thu (như FR-18 / FR-19: tên, mục đích, số tiền mỗi người, hạn đóng), tiền vào sổ khi từng thanh toán được xác nhận; ghi **chi** = khoản chi theo FR-27. Tổng chi của phạm vi không được vượt tổng thu (số dư hiện có), OQ-18. Màn "Thu chi chung → Ghi giao dịch" chỉ ghi cho **quỹ chung** (không có lựa chọn phạm vi); thu chi của sự kiện ghi trong trang chi tiết sự kiện. | 6.3, OQ-18 |
| FR-27 | Mỗi khoản chi có: tên, mục đích, danh mục, số tiền, ngày giao dịch, người thực hiện, chứng từ & ghi chú (nếu có). | 6.3 |
| FR-28 | Xem lịch sử giao dịch theo phạm vi, lọc theo thời gian / danh mục / loại. | 2.1, 9.2 |
| FR-29 | Sổ thu chi chỉ ghi thêm; sửa sai bằng bút toán đảo có lý do (không sửa/xóa dòng cũ). | 8 |

### 2.8. Sự kiện (M8)

| Mã | Yêu cầu | Nguồn |
|---|---|---|
| FR-30 | Thủ quỹ tạo, sửa, hủy sự kiện; mọi thành viên đang hoạt động được mời. | 3 |
| FR-31 | Học sinh (và thủ quỹ) xác nhận tham gia / không tham gia; thủ quỹ tự xác nhận không cần thủ quỹ khác. | 3.1 |
| FR-32 | Danh sách người tham gia chỉ gồm người **đã xác nhận tham gia**, với tình trạng đóng tiền: Đã đóng / Chờ xác nhận / Chưa đóng. Thanh toán tự ghi nhận của thủ quỹ được tính nhưng lưu lịch sử riêng. | 3.4, OQ-18 |
| FR-33 | Thủ quỹ **kết thúc** sự kiện đang mở (không có bước chốt danh sách riêng): người chưa đóng khoản nào bị loại; người chưa đóng đủ nhận thông báo nhắc và vẫn được đóng phần còn lại; từ đó sự kiện không tạo khoản thu và không ghi chi mới. | 3, OQ-18 |
| FR-34 | Theo dõi tổng thu, tổng chi, tiền thừa, người tham gia và tình hình đóng tiền của từng sự kiện. | 2.2 |

### 2.9. Quyết toán sự kiện (M9)

| Mã | Yêu cầu | Nguồn |
|---|---|---|
| FR-35 | Quyết toán sự kiện đã kết thúc: tổng hợp số người tham gia, tổng thu, tổng chi, tiền thừa (luôn ≥ 0 vì chi ≤ thu). | 4, OQ-18 |
| FR-36 | Tiền thừa: chọn hoàn tiền trực tiếp **hoặc** cộng vào số dư cá nhân; luôn chia **theo tỷ lệ số tiền mỗi người tham gia đã đóng** (không có lựa chọn chia đều). | 4.1, OQ-18 |
| FR-37 | Hoàn trực tiếp: thủ quỹ ghi nhận giao dịch hoàn cho từng người. | 4.1 |
| FR-38 | ~~Thiếu: tạo khoản thu bổ sung~~ – **bỏ** (OQ-18): chi không vượt thu nên không phát sinh thiếu. Cần thêm tiền thì tạo khoản thu khi sự kiện còn mở. | 4.2, OQ-18 |
| FR-39 | Hoàn tất quyết toán → sự kiện chuyển "Đã quyết toán", không nhận thu/chi mới. | 4.3 |

### 2.10. Số dư cá nhân (M10)

| Mã | Yêu cầu | Nguồn |
|---|---|---|
| FR-40 | Mỗi thành viên có số dư cá nhân theo quỹ, độc lập với quỹ chung; mọi biến động có lịch sử. | 5 |
| FR-41 | Người dùng bấm "Rút tiền" để rút **một lần toàn bộ** số dư → yêu cầu gửi thủ quỹ → thủ quỹ liên hệ trả tiền rồi xác nhận đã hoàn (hoặc từ chối kèm lý do). Trong khi chờ, số dư bị giữ. | 5, OQ-11 |
| FR-42 | Học sinh xem số dư, lịch sử đóng góp, các khoản được hoàn trả. | 1.2 |

### 2.11. Thông báo (M11)

| Mã | Yêu cầu | Nguồn |
|---|---|---|
| FR-43 | Thông báo nội bộ: sự kiện mới; khoản thu chung; khoản thu sự kiện (**chỉ người tham gia**); khoản thu sắp đến hạn; bị loại / nhắc đóng phần còn lại khi sự kiện kết thúc; kết quả quyết toán; tiền cộng vào số dư; yêu cầu rút tiền được xử lý; thanh toán được xác nhận / bị từ chối. | 7 |
| FR-44 | Mỗi thông báo ghi rõ phạm vi Quỹ chung / Sự kiện; xem danh sách, đánh dấu đã đọc, đếm chưa đọc. | 7 |

### 2.12. Audit log (M12)

| Mã | Yêu cầu | Nguồn |
|---|---|---|
| FR-45 | Mọi thao tác ghi của thủ quỹ được lưu: ai, khi nào, hành động, phạm vi (chung/sự kiện), trạng thái trước–sau, số tiền, phương thức. | 1.1, 8 |
| FR-46 | Đánh dấu riêng: thủ quỹ tự xác nhận giao dịch cá nhân; thủ quỹ xác nhận cho học sinh; điều chỉnh tiền; hoàn tiền; sử dụng số dư; học sinh gửi xác nhận; từ chối kèm lý do. | 8 |
| FR-47 | Thủ quỹ xem, lọc nhật ký (theo thời gian, người, phạm vi, sự kiện, hành động). | 1.1 |

### 2.13. Báo cáo (M13)

| Mã | Yêu cầu | Nguồn |
|---|---|---|
| FR-48 | Tổng quan quỹ chung: tổng thu, tổng chi, số dư, tổng học sinh, khoản thu chưa hoàn thành, khoản chi đã phát sinh – **không** gồm số liệu sự kiện. | 9.1 |
| FR-49 | Báo cáo thu chi chung: theo danh mục, lọc thời gian, theo dõi số dư, lịch sử. | 9.2 |
| FR-50 | Tổng quan sự kiện: số tham gia, số còn lại sau kết thúc, tổng cần thu, đã thu, chưa thu, tổng chi, tiền thừa, đã hoàn, đã cộng số dư, trạng thái quyết toán. | 9.3 |
| FR-51 | Báo cáo học sinh (thủ quỹ): danh sách, đã đóng quỹ chung, khoản chung chưa đóng, lịch sử đóng sự kiện, số dư, lịch sử hoàn tiền, lịch sử dùng số dư. Học sinh chỉ xem của mình. | 9.4 |
| FR-52 | Báo cáo tổng kết cuối năm quỹ chung: tổng thu/chi năm, số dư cuối năm, khoản chưa hoàn thành, lịch sử giao dịch năm. | 9.5 |

### 2.14. Quyết toán cuối năm quỹ chung (M14)

| Mã | Yêu cầu | Nguồn |
|---|---|---|
| FR-53 | Quyết toán quỹ chung cuối năm **một lần duy nhất**, cách làm như quyết toán sự kiện nhưng chia cho **cả lớp** (mọi thành viên đang hoạt động): thừa → hoàn trực tiếp hoặc cộng số dư cá nhân, chia theo tỷ lệ số tiền mỗi người đã đóng vào quỹ chung (đề xuất OQ-19, chờ duyệt; trước đây: chọn chia đều / tỷ lệ, thiếu → khoản thu bổ sung chung). | 9.5, OQ-13, OQ-19 |
| FR-54 | Chỉ quyết toán cuối năm khi mọi sự kiện đã quyết toán hoặc hủy. Xong thì quỹ chuyển "Đã tổng kết" (chỉ xem): không nhận khoản thu, giao dịch, sự kiện mới; vẫn rút được số dư cá nhân. | OQ-13 |

## 3. Yêu cầu phi chức năng

| Mã | Yêu cầu |
|---|---|
| NFR-01 | Bảo mật phiên: cookie HttpOnly + SameSite=Strict, chống CSRF bằng antiforgery header, giới hạn tần suất đăng nhập/đăng ký. |
| NFR-02 | Mật khẩu băm PBKDF2 + pepper (`SECRET_KEY`); mật khẩu tạm chỉ hiển thị một lần; không ghi mật khẩu/hash/token vào log hay audit. |
| NFR-03 | Phân quyền theo permission ở backend (`[RequirePermission]`); không thuộc quỹ → 404, thiếu quyền → 403. Frontend ẩn/hiện chỉ để hiển thị. |
| NFR-04 | Tiền là số nguyên VND (`bigint`), không dùng số thực. |
| NFR-05 | Mọi thao tác ghi nghiệp vụ + audit + thông báo nằm trong một transaction. |
| NFR-06 | AuditLog, LedgerEntry, BalanceTransaction chỉ ghi thêm (trigger chặn UPDATE/DELETE; `app_user` không có quyền DELETE). |
| NFR-07 | Ngày nghiệp vụ theo giờ Việt Nam (UTC+7); thời điểm lưu UTC (`timestamptz`). |
| NFR-08 | Database dùng chung Neon PostgreSQL; thay đổi schema chỉ qua EF Core Migration theo quy trình `docs/implementation/neon-database-guide.md`. |
| NFR-09 | Giao diện desktop ≥ 1280 px, tiếng Việt, Kendo UI theo Design System. |
| NFR-10 | File thực tế lưu trên Neon Object Storage (S3-compatible); DB chỉ lưu metadata + khóa; tải file luôn qua API có kiểm quyền, không lộ URL bucket. |
