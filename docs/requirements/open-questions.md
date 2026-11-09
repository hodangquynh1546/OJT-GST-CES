# Quyết định & Câu hỏi cần xác nhận – ClassFund

> Phiên bản: 2.0 · 2026-11-05 – mô hình **Giáo viên · Học sinh · Phụ huynh** (D-24 → D-33, mục 1b); B1 → P1, X-01, X-02 đã chốt (mục 4). v1.2 · 2026-11-04 (PO duyệt D-10, D-11, D-12, Q-11, Q-12, B2–B7). D-01 → D-58 và Q-01 → Q-56 của phạm vi quỹ toàn trường không còn hiệu lực (Git history `f6cecab`).

## 1. Quyết định đã chốt

| Mã | Quyết định |
|---|---|
| D-01 | Hệ thống quản lý **một quỹ lớp**; không quản lý trường, năm học, nhiều lớp, phê duyệt. *(Sửa 2026-11-04: có 3 vai trò – xem D-13)* *(Sửa 2026-11-05: một giáo viên quản lý **nhiều quỹ** – D-25; vẫn không quản lý trường / năm học / phê duyệt)* |
| D-02 | Thủ quỹ đầu tiên và quỹ được tạo bằng lệnh quản trị trên server; không đăng ký công khai *(Thay bằng D-24, 2026-11-05: giáo viên tự đăng ký; lệnh server chỉ còn `create-teacher`, `reset-password`, `disable-account`, `enable-account`)* |
| D-03 | Khoản chi lớn hơn số dư **vẫn được lưu**; hệ thống cảnh báo số dư âm |
| D-04 | Tháng chỉ là kỳ báo cáo; số dư cộng dồn; số dư cuối tháng trước = số dư đầu tháng sau |
| D-05 | Thu / chi lưu là có hiệu lực ngay – không có yêu cầu, phiếu, duyệt, xác nhận |
| D-06 | Đăng nhập bằng cookie session, mật khẩu băm PBKDF2. *(Bổ sung 2026-11-06 – TD-98:* trước khi băm, mật khẩu được trộn với khóa bí mật `SECRET_KEY` (pepper, HMAC-SHA256) lưu ngoài DB; thiếu khóa thì API không khởi động; hash cũ không pepper được nâng cấp khi đăng nhập đúng.*)* |
| D-07 | Ngày nghiệp vụ theo giờ Asia/Ho_Chi_Minh |
| D-08 | Thiết kế database cũ được thay bằng baseline `InitialClassFund`; tài liệu ghi đè tại chỗ |
| D-13 | *(Thay bằng D-26, 2026-11-05)* **3 vai trò**: Thủ quỹ (quản lý), GVCN (chỉ xem toàn bộ), Phụ huynh (chỉ xem dữ liệu công khai). Phân quyền theo permission ở backend. Phụ huynh không có chức năng nộp tiền, báo chuyển khoản hay đề nghị chi (task 2026-11-04; trả lời Q-05) |
| D-14 | Hủy giao dịch **bắt buộc có lý do** (task 2026-11-04: "hủy có lý do") |
| D-15 | Thiết kế UI/UX v2 thay thế toàn bộ trên Design Canvas hiện có (cùng link), đổi tên dự án thành "Quỹ Lớp – UI/UX 3 vai trò" *(2026-11-05: Canvas đổi tên thành "Quỹ Lớp – UI/UX Giáo viên · Học sinh · Phụ huynh", cùng link – D-24 → D-34; 2026-11-07: "ClassFund – UI/UX v4.0 Desktop Web" – D-37, D-38)* |
| D-10 | *(Duyệt 2026-11-04 – trước đó là quyết định tạm ở mục 2)* *(Người cấp tài khoản đổi thành GVCN – D-27, D-28, 2026-11-05; nguyên tắc mật khẩu tạm giữ nguyên)* Thành viên được tạo với **mật khẩu tạm ngẫu nhiên**, bắt buộc đổi ở lần đăng nhập đầu; không ghi mật khẩu rõ vào log |
| D-11 | *(Duyệt 2026-11-04; **giữ nguyên** 2026-11-05 – xem mục 1c)* Một quỹ có thể có **nhiều thủ quỹ**; mỗi thao tác ghi xác định đúng người thực hiện; **không khóa / hạ quyền thủ quỹ hoạt động cuối cùng**; lưu lịch sử thay đổi và người thực hiện |
| D-12 | *(Duyệt 2026-11-04)* Phụ huynh xem báo cáo **mọi tháng**, chỉ dữ liệu công khai; không có thao tác công bố tháng |
| D-16 | *(Thay bằng D-29, 2026-11-05: không còn "đổi vai trò"; GVCN phân công / thu hồi thủ quỹ)* *(Trả lời Q-11, 2026-11-04)* Người có `Member.Manage` **đổi được vai trò** thành viên; không tự đổi vai trò của chính mình; không hạ quyền thủ quỹ hoạt động cuối cùng; nhật ký ghi vai trò cũ / mới, người thực hiện, thời điểm; quyền luôn xác định lại từ dữ liệu backend |
| D-17 | *(Trả lời Q-12, 2026-11-04; áp dụng cho học sinh chưa phân công – 2026-11-05)* Phụ huynh chỉ thấy **họ tên và vai trò** của thủ quỹ / GVCN; không email, số điện thoại, liên hệ cá nhân |
| D-18 | *(B2)* Khoản **công khai** đã hủy: phụ huynh thấy nhãn "Đã hủy" và ngày hủy (lọc trạng thái "Đã hủy"); không tính vào số dư. Lý do hủy và người hủy **không** hiện cho phụ huynh (giữ nguyên tắc không lộ thông tin nội bộ – xem mục 3, X-02) |
| D-19 | *(B3)* Thêm `UpdatedBy`, `CancelledBy` (nullable) vào `FundTransaction`, `UpdatedBy` vào `FundMember`; dữ liệu cũ được điền từ `AuditLog` nếu có, không sửa giá trị khác |
| D-20 | *(B4)* Tổng thu / chi trên Tổng quan là **cộng dồn từ khi lập quỹ**; số liệu tháng hiện tại hiển thị ở nhóm riêng có nhãn rõ |
| D-21 | *(B5)* Không thêm ghi chú nhận diện học sinh / thành viên trong v1.1 |
| D-22 | *(B6)* Bộ lọc danh mục của phụ huynh chỉ gồm danh mục **đang hiện** và có **ít nhất một khoản công khai** (đang hiệu lực hoặc đã hủy); ô "Khoản khác" luôn hiện, không phụ thuộc bộ lọc |
| D-23 | *(B7)* Giữ hành vi backend: mật khẩu đúng nhưng tài khoản bị khóa → thông báo "Tài khoản đang bị khóa, liên hệ thủ quỹ"; sai mật khẩu → thông báo chung *(Sửa 2026-11-05: "… liên hệ giáo viên chủ nhiệm")* |

## 1b. Quyết định 2026-11-05 – Mô hình Giáo viên · Học sinh · Phụ huynh

PO duyệt cả 4 đề xuất (tài khoản học sinh, phụ huynh, thủ quỹ, migration) kèm điều kiện bổ sung. Cột "Trạng thái" = **trạng thái triển khai**; mọi quyết định đều **đã chốt**. 2026-11-05: mã nguồn cũ đã xóa → tất cả **Not Implemented** (xem [implementation-status.md](../implementation/implementation-status.md)). UI: Canvas.

| Mã | Quyết định | Thay / sửa | Trạng thái |
|---|---|---|---|
| D-24 | **Giáo viên tự đăng ký**: họ tên, email, mật khẩu, xác nhận mật khẩu. Tài khoản luôn là *Giáo viên* – request không chọn được vai trò. Không tạo quỹ mặc định. Chưa có xác minh email (xem PD-01) | Thay D-02 | Not Implemented |
| D-25 | Một giáo viên **quản lý nhiều quỹ** và tự tạo quỹ. Quỹ mới có danh mục mặc định, trạng thái **"Chưa phân công thủ quỹ"** | Sửa D-01 | Not Implemented |
| D-26 | Vai trò **trong từng quỹ** suy ra từ dữ liệu, không lưu và không nhận từ client: **GVCN** = người phụ trách quỹ (`Fund.OwnerUserId`) – xem toàn bộ (kể cả khoản không công khai, ghi chú, lý do hủy, người thực hiện), quản lý thông tin quỹ, học sinh, phụ huynh, phân công thủ quỹ; **không** thêm / sửa / hủy giao dịch, **không** quản lý danh mục. **Thủ quỹ** = học sinh của quỹ có phân công đang hiệu lực – quản lý thu chi và danh mục, chỉ xem danh sách thành viên. **Học sinh** chưa phân công và **phụ huynh** – chỉ dữ liệu công khai | Thay D-13 | Not Implemented |
| D-27 | **Tài khoản học sinh**: không bắt buộc email. GVCN cấp **tên đăng nhập + mật khẩu tạm** sinh ngẫu nhiên an toàn, chỉ hiển thị **một lần**, không lưu / ghi log dạng rõ; bắt buộc đổi ở lần đăng nhập đầu. Học sinh **chưa kích hoạt** (chưa đổi mật khẩu tạm) không được phân công thủ quỹ. Học sinh chưa phân công chỉ xem dữ liệu công khai. Học sinh thuộc **đúng một quỹ** | Sửa D-10 | Not Implemented |
| D-28 | **Phụ huynh**: một tài khoản / email, liên kết được nhiều học sinh ở nhiều quỹ; v1 mỗi học sinh **một** email phụ huynh. Email đã có tài khoản phụ huynh → chỉ tạo liên kết, không tạo tài khoản mới. Chưa có → tạo tài khoản với mật khẩu tạm ngẫu nhiên, hiển thị **một lần** cho GVCN tự chuyển; bắt buộc đổi ở lần đầu. **Không** giả định email đã được gửi. GVCN đóng màn hình là không xem lại được mật khẩu. Email đang thuộc tài khoản giáo viên / học sinh → từ chối (`parent.email_in_use`). Phụ huynh **không** nhận quyền thủ quỹ khi con là thủ quỹ | Sửa D-10 | Not Implemented |
| D-29 | **Thủ quỹ**: một quỹ có thể có **nhiều thủ quỹ** (giữ D-11). GVCN phân công / thu hồi. Đổi người: phân công người mới thành công **trước**, rồi thu hồi người cũ. **Không** thu hồi được thủ quỹ cuối cùng của quỹ đã có thủ quỹ (`treasurer.last`). Lưu lịch sử phân công / thu hồi, người thực hiện, thời điểm. Thủ quỹ chỉ xem danh sách thành viên – không tạo tài khoản, không đổi vai trò. GVCN quản lý thông tin quỹ; thủ quỹ quản lý danh mục | Thay D-16 | Not Implemented |
| D-30 | **Migration dữ liệu cũ**: người phụ trách cũ (`OwnerUserId`) trở thành **GVCN** của quỹ; quỹ thành "Chưa phân công thủ quỹ"; **không** giữ người lớn cũ làm thủ quỹ; không xóa giao dịch, AuditLog, dữ liệu; migration bổ sung, giữ dữ liệu. Người phụ trách cũ chỉ ghi chép lại được khi có học sinh được phân công thủ quỹ (thực chất: chính học sinh đó ghi) | – | Không áp dụng – chưa có dữ liệu cũ (D-36) |
| D-31 | Phạm vi tài khoản: **giáo viên** và **phụ huynh** có thể liên quan nhiều quỹ; **học sinh** đúng một quỹ. Quy tắc "mỗi tài khoản thuộc đúng một quỹ" của v1.2 hết hiệu lực (xem mục 1c) | Thay quy tắc ở FR §3, BR-13 | Not Implemented |
| D-32 | API theo quỹ: `/api/v1/funds/{fundId}/…`. Không dùng đường dẫn cũ `/fund`, `/transactions`, `/categories` (của R1, đã xóa). Quyền xác định từ phiên đăng nhập + dữ liệu, không từ `teacherId` / `role` gửi lên | – | Not Implemented |
| D-33 | Không có Email Service: hệ thống **không gửi** email; response liên kết phụ huynh có `emailSent = false`; UI ghi rõ "Hệ thống không gửi email – hãy tự chuyển thông tin" | – | Not Implemented; UI: Canvas |
| D-34 | **Đăng nhập không chọn vai trò**: một ô "Email hoặc tên đăng nhập" + mật khẩu; hệ thống tự xác định loại tài khoản và vai trò trong từng quỹ (2026-11-05) | Sửa FR-13 | Not Implemented; UI: Canvas |
| D-35 | **Xóa mã nguồn** Backend / Frontend cũ (2026-11-05); giai đoạn đó chỉ hoàn thiện Docs + Canvas làm Source of Truth *(giai đoạn kết thúc – thay bằng D-39)* | – | Đã thực hiện |
| D-36 | **Gộp schema 2.0 vào baseline** `InitialClassFund`, kèm `AddDatabaseProtections`; không có migration `TeacherStudentParent`. Mã nguồn cũ đã xóa, chưa có DB cần nâng cấp nên D-30 và luồng UF-12 chưa phát sinh (2026-11-05) | Thay DD-21, TD-87 | Đã thực hiện |
| D-37 | **Desktop Web only** (2026-11-07): bỏ phạm vi Mobile App / Mobile Responsive; khung 1440 × 900, tối thiểu 1280 px; sửa NFR-06. *Lưu ý mã:* Canvas và `docs/ui-design` ghi quyết định này là "D-36" theo đánh số riêng của Canvas – cùng một quyết định, mã chuẩn là **D-37** | Sửa NFR-06 | Đã áp dụng (Canvas v4.0, Foundation Frontend) |
| D-38 | Đổi tên dự án **SchoolFund / Quỹ Lớp → ClassFund** (2026-11-06): thư mục `ClassFundAPI/`, `ClassFundUI/`, namespace `ClassFund.*`, cookie `cf_session` / `cf_xsrf` (TD-112). Canvas ghi là "D-37" | – | Đã thực hiện |
| D-39 | **Đưa backend về codebase Foundation và triển khai theo module trọn luồng** (2026-11-09, yêu cầu của lead): bỏ code nghiệp vụ backend đã làm (tham khảo tại commit `6b6645d`); hoàn thiện Foundation BE + FE dùng chung một chuẩn; mỗi module làm FE → BE → DB → Integration Test → E2E và chỉ nghiệm thu khi đạt Definition of Done ([module-plan.md](../implementation/module-plan.md), [foundation-guide.md](../architecture/foundation-guide.md)) | Thay D-35 (giai đoạn "chỉ Docs") | Foundation: Implemented; module: Not Implemented |
| D-40 | **Quy ước API / DB chung** là một phần của Foundation ([api-conventions.md](../api/api-conventions.md)): đổi quy ước phải làm PR Foundation riêng, không đổi trong PR module | – | Implemented (Foundation) |

### 1c. Đối chiếu D-11 (yêu cầu của PO)

- **D-11 đã duyệt** (mục 1) chỉ gồm: một quỹ có thể có **nhiều thủ quỹ**; mỗi thao tác ghi xác định đúng người thực hiện; không khóa / hạ quyền thủ quỹ hoạt động cuối cùng; lưu lịch sử. **Không** có quy tắc "mỗi tài khoản thuộc đúng một quỹ".
- Quy tắc "mỗi tài khoản thuộc đúng 1 quỹ với 1 vai trò" chỉ nằm ở **D-11 bản tạm** (mục 2, trước khi duyệt) và bị dẫn sai nguồn là "D-11" ở: `functional-requirements.md` §3, `business-rules-and-processes.md` BR-13, `data-dictionary.md` (`FundMember.UserId UNIQUE`), `database-design-decisions.md` DD-16. **Mâu thuẫn tài liệu** – đã sửa nguồn trong các file đó.
- Áp dụng quyết định 2026-11-05: **giữ** phần "nhiều thủ quỹ / không thu hồi thủ quỹ cuối cùng / lưu lịch sử" (D-11 → D-29); quy tắc "một tài khoản một quỹ" **chỉ còn** cho học sinh (D-31).
- Mâu thuẫn khác được xử lý: D-02 (đăng ký) → D-24; D-10 (ai cấp tài khoản) → D-27/D-28; D-13 (GVCN chỉ xem) → D-26 (GVCN quản lý con người nhưng không ghi thu chi); D-16 (đổi vai trò) → D-29.

### 1d. Cần PO quyết định (Pending Decision)

| Mã | Vấn đề | Đang làm | Phương án |
|---|---|---|---|
| PD-01 | Đăng ký giáo viên **chưa xác minh email**: người khác có thể đăng ký trước bằng email của một phụ huynh → GVCN không tạo được tài khoản phụ huynh cho email đó (`parent.email_in_use`) | Chấp nhận rủi ro, ghi nhận | Thêm xác minh email khi có Email Service |
| PD-02 | Phụ huynh quên mật khẩu | Chỉ người vận hành (`reset-password`); GVCN **không** đặt lại được vì phụ huynh có thể thuộc quỹ của giáo viên khác | GVCN của học sinh liên kết được đặt lại khi tài khoản chưa kích hoạt |
| PD-03 | GVCN khóa / mở khóa tài khoản học sinh | Chưa có trong ứng dụng (chỉ lệnh server) | Thêm thao tác trên Chi tiết học sinh |
| PD-04 | Học sinh rời lớp / chuyển lớp | Chưa có; không xóa học sinh | Đánh dấu "đã rời", thu hồi thủ quỹ, khóa tài khoản |
| PD-05 | Chuyển quỹ cho giáo viên khác (đổi GVCN) | Chưa có | Lệnh vận hành hoặc màn hình chuyển giao |
| PD-06 | Thu hồi thủ quỹ khi tài khoản học sinh đó bị khóa / đặt lại mật khẩu (thủ quỹ "tạm không hoạt động") | Phân công vẫn còn; học sinh không thao tác được tới khi đổi mật khẩu / mở khóa; quy tắc thủ quỹ cuối cùng chỉ đếm phân công | Đếm cả trạng thái tài khoản như BR-19 cũ |

## 2. Quyết định tạm – áp dụng phương án đề xuất, chờ PO xác nhận

Người dùng chưa chọn phương án cho các câu hỏi dưới đây (2026-11-04), nên thiết kế đang dùng phương án đề xuất. **Cập nhật 2026-11-04 (lượt 2):** D-10, D-11, D-12 đã được duyệt và chuyển lên mục 1 (giữ dòng gốc dưới đây để lưu lịch sử). D-09 vẫn chờ, gắn với B1.

| Mã | Quyết định tạm | Phương án khác | Ảnh hưởng nếu đổi |
|---|---|---|---|
| D-09 | **Công khai theo từng giao dịch**: cờ `IsPublic`, mặc định bật; dữ liệu đã có được coi là công khai. Phụ huynh thấy số dư và tổng thật; khoản không công khai gộp thành dòng "Khoản khác (không công khai chi tiết)" | (a) Công khai tất cả, chỉ ẩn ghi chú; (b) Công bố theo tháng; (c) Kết hợp (b) với cờ từng khoản | Cột `IsPublic`, ô tick trên T-01, truy vấn phạm vi công khai |
| D-10 | **Thủ quỹ tạo tài khoản** GVCN / phụ huynh / thủ quỹ khác. Hệ thống sinh mật khẩu tạm hiển thị **một lần**; người dùng bắt buộc đổi mật khẩu khi đăng nhập lần đầu. Không gửi email | (a) Liên kết mời có hạn; (b) Chỉ tạo bằng lệnh server | Màn hình T-02, cột `MustChangePassword`, API `/members` |
| D-11 | **Một quỹ có thể có nhiều thủ quỹ**, luôn còn ít nhất 1 thủ quỹ đang hoạt động; **mỗi tài khoản thuộc đúng 1 quỹ** với 1 vai trò | Đúng 1 thủ quỹ; hoặc 1 tài khoản thuộc nhiều quỹ | Ràng buộc `UQ_FundMember_UserId`, quy tắc `member.last_treasurer` |
| D-12 | Phụ huynh xem được **báo cáo của mọi tháng** (bản công khai); không có thao tác "công bố tháng" | Chỉ tháng thủ quỹ đã công bố | Thêm bảng / cờ công bố tháng |

## 3. Câu hỏi còn mở

| Mã | Câu hỏi | Đang làm | Phương án khác |
|---|---|---|---|
| Q-01 | Quỹ đã có tiền trước khi dùng ứng dụng: nhập **số dư ban đầu** thế nào? | Ghi một khoản thu "Số dư chuyển sang", danh mục "Thu khác" | Thêm trường số dư ban đầu |
| Q-02 | Có cho ghi giao dịch **ngày trong tương lai**? | Không | Cho phép |
| Q-03 | Có cần **khóa tháng** đã báo cáo? | Không khóa; sửa tháng cũ thì các tháng sau tự tính lại | Khóa theo tháng |
| Q-04 | Có cần danh sách **học sinh đã đóng quỹ**? | Không; ghi trong nội dung / ghi chú | Danh sách học sinh + đánh dấu theo đợt |
| Q-06 | **Đính kèm ảnh hóa đơn**? | Không | 1 ảnh / giao dịch |
| Q-07 | **Quên mật khẩu** tự phục vụ? | Thủ quỹ đặt lại mật khẩu tạm cho thành viên; thủ quỹ quên mật khẩu → thủ quỹ khác hoặc người vận hành (`reset-password`) đặt lại | Email đặt lại |
| Q-08 | **Xuất báo cáo** Excel / PDF / in? | Không | Xuất báo cáo tháng |
| Q-09 | Danh mục mặc định có phù hợp? | Thu: "Đóng quỹ", "Thu khác". Chi: "Văn phòng phẩm", "Sinh hoạt, liên hoan", "Khen thưởng", "Chi khác" | Danh sách khác |
| Q-10 | Có **khôi phục** giao dịch đã hủy? | Không | Cho khôi phục |
| Q-11 | Có cần **đổi vai trò** của một thành viên (ví dụ phụ huynh thành thủ quỹ)? | ~~Không trong v1.1~~ → **Đã trả lời: có** (D-16) | – |
| Q-12 | Phụ huynh có được thấy **tên thủ quỹ / GVCN** trong thông tin quỹ? | **Đã trả lời** (D-17): họ tên + vai trò, không liên hệ | – |

## 4. Chờ PO xác nhận (lượt 2026-11-04)

> **Cập nhật 2026-11-05:** PO đã chốt **B1 = P1** (bảo vệ nội dung, chấp nhận số tiền suy ra được; cảnh báo trên form – D-09 chốt theo P1), **X-01** = phạm vi toàn bộ lịch sử quỹ, **X-02** = không hiện lý do / người hủy cho phụ huynh (và học sinh chưa phân công). Phần dưới giữ để lưu lịch sử.

### B1 – Không để lộ số tiền khoản ẩn duy nhất trong tháng

**Vì sao chưa áp dụng được.**
- Phụ huynh được xem (đã chốt):
  - số dư hiện tại chính xác;
  - số dư đầu / cuối từng tháng (D-12);
  - từng khoản công khai.
- Từ đó luôn tính được tổng khoản ẩn của tháng: `(cuối tháng − đầu tháng) − (thu công khai − chi công khai)`.
- Nếu tháng chỉ có **một** khoản ẩn, tổng này chính là số tiền của khoản đó.
- Ngoài ra, phụ huynh xem số dư trước và sau thời điểm thủ quỹ ghi cũng biết ngay số tiền.
- ⇒ Không thể vừa giữ số dư chính xác theo thời gian thực, vừa bảo đảm tuyệt đối không suy ra được số tiền.

| Phương án | Riêng tư | Đối chiếu báo cáo |
|---|---|---|
| **P1 (đề xuất)**: Bảo vệ *nội dung*, chấp nhận *số tiền* suy ra được. <br>• Giữ dòng "Khoản khác" theo tháng, không có số lượng, ngày, danh mục. <br>• Form thủ quỹ cảnh báo khi tắt công khai: "Số tiền vẫn tính vào số dư phụ huynh thấy; đây là khoản không công khai duy nhất trong tháng nên phụ huynh có thể suy ra số tiền". | Nội dung, ghi chú, ngày, danh mục, số lượng được bảo vệ. Số tiền suy ra được nếu chỉ có 1 khoản; thủ quỹ được báo trước | Đầy đủ: mọi số tổng của phụ huynh khớp báo cáo thủ quỹ |
| **P2**: Ngưỡng k = 2. <br>• Tháng có < 2 khoản ẩn cùng loại thì phần ẩn **dồn sang** dòng "Khoản khác (gộp nhiều tháng)" cho tới khi đủ ngưỡng. <br>• Tổng thu / chi tháng của phụ huynh chỉ gồm khoản công khai + phần đã đủ ngưỡng. | Không suy ra được qua báo cáo tháng. **Vẫn suy ra được** qua số dư hiện tại (xem trước / sau) | Tổng tháng của phụ huynh ≠ báo cáo thủ quỹ; chỉ số dư cộng dồn khớp. Cần giải thích trên giao diện |
| **P3**: Phụ huynh chỉ thấy số dư và báo cáo đến **hết tháng trước**, kết hợp P2. | Mạnh nhất: không xem được biến động trong tháng | Số liệu trễ tối đa 1 tháng; trái với "số dư hiện tại" của phạm vi |

Đề xuất **P1**: đơn giản, minh bạch, đối chiếu được, thủ quỹ chủ động quyết định. Chờ PO chọn; D-09 chỉ chốt sau B1.

### X-01 – Phạm vi "có giao dịch công khai" trong B6
Đang hiểu là "trong toàn bộ lịch sử quỹ" (danh sách ổn định). Phương án khác: chỉ trong kỳ đang lọc.

### X-02 – Lý do hủy với phụ huynh (B2)
B2 không nói rõ. Đang **không** hiện lý do / người hủy cho phụ huynh, vì lý do có thể chứa thông tin nội bộ. Nếu muốn hiện, cần quy ước lý do hủy là thông tin công khai.

## 5. Lịch sử thay đổi tài liệu

| Ngày | Thay đổi |
|---|---|
| 2026-11-04 | v1.0 – D-01 → D-08, Q-01 → Q-10 |
| 2026-11-04 | v1.1 – thêm D-13 → D-15 (chốt), D-09 → D-12 (tạm), Q-11, Q-12; Q-05 đã trả lời |
| 2026-11-04 | v1.2 – D-10, D-11, D-12 duyệt; Q-11 → D-16; Q-12 → D-17; B2 → B7 → D-18 → D-23; thêm mục 4 (B1, X-01, X-02 chờ xác nhận). Không xóa dòng cũ |
| 2026-11-05 | v2.0 – mô hình Giáo viên · Học sinh · Phụ huynh: D-24 → D-33 (mục 1b), đối chiếu D-11 (mục 1c), PD-01 → PD-06 (mục 1d); ghi nhận B1 = P1, X-01, X-02 đã chốt; đánh dấu D-01, D-02, D-10, D-13, D-16, D-23 bị thay / sửa. Không xóa dòng cũ |
| 2026-11-05 | v2.1 – thêm D-34 (đăng nhập không chọn vai trò), D-35 (xóa mã nguồn); trạng thái D-24 → D-33 → Not Implemented |
| 2026-11-05 | v2.2 – thêm D-36; D-30 không áp dụng |
| 2026-11-09 | v2.3 – thêm D-37 (Desktop Web only), D-38 (ClassFund), D-39 (đưa backend về Foundation, làm theo module), D-40 (quy ước API chung); D-35 kết thúc |
