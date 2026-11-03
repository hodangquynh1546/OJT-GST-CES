# Database Design Decisions

> Phiên bản: 0.1 (bản nháp, chờ duyệt) · Ngày: 2026-11-03
> DD-xx = quyết định thiết kế (BA đề xuất, chờ chủ dự án duyệt) · DQ-xx = câu hỏi kỹ thuật · Q-xx = câu hỏi nghiệp vụ trong [open-questions.md](../requirements/open-questions.md).

## 1. Quyết định thiết kế

| Mã | Quyết định | Lý do | Phương án đã cân nhắc |
|---|---|---|---|
| DD-01 | **Sổ cái append-only + số dư cache** cập nhật cùng transaction | Sổ cái cho lịch sử, số dư sau mỗi giao dịch, đánh dấu sau khóa sổ, đối soát; cache cho khóa dòng và `CHECK` bất biến | (a) Chỉ tính số dư bằng `SUM` mỗi lần: không khóa dòng được để chống race, chậm dần. (b) Chỉ lưu số dư cache: mất vết, không đối soát được. (c) Event sourcing đầy đủ: quá phức tạp cho nghiệp vụ |
| DD-02 | `Class` thuộc `SchoolYear`; `ClassFund` 1-1 với `Class` | "Quỹ lớp – năm học" là đơn vị nghiệp vụ (D-02, D-25); lên lớp = lớp mới, không tự động chuyển số dư | Lớp dùng chung qua nhiều năm + bảng quỹ theo năm: phức tạp hơn khi lớp đổi tên / tách / gộp |
| DD-03 | Phiếu thu = tiêu đề + `ReceiptAllocation`; phần thừa = tổng − Σ phân bổ; tự trừ số dư cá nhân cũng là phiếu thu (`Method = StudentBalance`) | Một đường duy nhất cho mọi khoản thanh toán nợ → `PaidAmount` luôn = Σ phân bổ; tiền thừa không có phân bổ nên không thể vào quỹ | Bảng riêng cho tự trừ số dư: thêm một nguồn thanh toán nợ, dễ lệch |
| DD-04 | Giữ chỗ là **cột tổng hợp trên dòng số dư** (`ClassFund`, `StudentDebt`, `StudentBalance`) | Một `CHECK` trên cùng dòng bảo vệ được "đã dùng + giữ chỗ ≤ số dư" ngay cả khi ứng dụng có lỗi | Bảng `Reservation` riêng: linh hoạt hơn nhưng không có CHECK xuyên bảng, phải dựa hoàn toàn vào ứng dụng |
| DD-05 | `FinancialAdjustment` dùng chung cho đảo giao dịch và điều chỉnh chênh lệch, FK tường minh tới chứng từ gốc | D-57 yêu cầu liên kết chứng từ gốc; FK nullable + `num_nonnulls = 1` được DB kiểm tra; partial unique chặn đảo hai lần | Cột `OriginalDocumentType + OriginalDocumentId` (đa hình): không có FK, không chặn được đảo hai lần chắc chắn |
| DD-06 | Tiền `bigint` (đồng) | Không sai số, nhanh, map `long` | `numeric`, `money`, số thực (financial-data-design mục 2) |
| DD-07 | Bảo vệ kỳ đã khóa bằng **trigger** trên sổ cái + trigger chặn sửa chứng từ hoàn tất | Bảo vệ ngay cả khi có truy vấn ngoài ứng dụng; chỉ mở đúng ba ngoại lệ đã chốt (D-30, D-36, D-50) | Chỉ kiểm tra ở ứng dụng: một lỗi code là ghi được vào kỳ đã khóa |
| DD-08 | Phân quyền: `UserRole` (Admin, Parent) + `ClassAssignment` (GVCN, Thủ quỹ, Người giữ quỹ theo lớp) + `UserPermissionGrant` (cấp riêng) | Phản ánh đúng ba nguồn quyền trong BA; vai trò theo lớp có lịch sử thời gian | Một bảng `UserRole` có `ScopeId` đa hình: mất FK tới `Class` |
| DD-09 | Gộp "Collection Target" vào `StudentDebt` | Quan hệ 1-1, không có dữ liệu riêng | Hai bảng: trùng lặp |
| DD-10 | Read Committed + `SELECT … FOR UPDATE` + thứ tự khóa cố định | Đơn giản, dự đoán được, đủ cho các điểm nóng đã xác định | Serializable toàn hệ thống: phải retry mọi transaction, khó kiểm soát với EF Core |
| DD-11 | Liên kết mềm (không FK) cho `AuditLog`, `DocumentStatusHistory`, `Notification` | Log phải tồn tại độc lập, ghi được cho mọi loại thực thể; không ảnh hưởng số liệu | FK nullable cho từng loại chứng từ (như `Attachment`): 10+ cột cho mỗi bảng log |
| DD-12 | `Attachment` dùng FK nullable cho từng loại chứng từ + `num_nonnulls = 1` | Chứng từ bắt buộc (D-16, D-53) là điều kiện tài chính → cần FK thật | Bảng nối riêng cho mỗi loại chứng từ: 10 bảng |
| DD-13 | Khóa chính `uuid` v7 sinh ở ứng dụng; `bigint identity` cho sổ cái / log | uuid: tạo trước khi lưu (idempotency, liên kết trong cùng transaction), không lộ số lượng; v7 có thứ tự thời gian nên index tốt. Identity cho sổ cái: thứ tự tuyệt đối để tính số dư lũy kế | `bigint` cho mọi bảng: lộ số lượng, khó sinh trước |
| DD-14 | Enum = `varchar(30)` + `CHECK` | Dễ thêm giá trị bằng migration; dễ đọc khi truy vấn | PostgreSQL enum type: đổi giá trị khó hơn trong migration |
| DD-15 | Tên bảng / cột PascalCase (mặc định EF Core) | Không cần thêm package naming convention (Tech Stack cố định); nhất quán với entity | snake_case: cần package ngoài Tech Stack hoặc cấu hình tay cho mọi bảng |
| DD-16 | Không có bảng `School` | D-01 một trường | Thêm sẵn `School`: chưa có nghiệp vụ |
| DD-17 | Không lưu cột "người đang giữ tiền" có thể sửa; suy ra từ phiếu thu + bàn giao hoàn tất | Tránh sửa dữ liệu đã hoàn tất; một nguồn sự thật | Cột `CurrentHolderUserId` cập nhật khi bàn giao: phải sửa dòng phân bổ (append-only) |
| DD-18 | Trạng thái "đã bị đảo" của chứng từ gốc **suy ra** từ `FinancialAdjustment`, không sửa cột trạng thái của chứng từ gốc | D-57: chứng từ hoàn tất không sửa trực tiếp | Thêm trạng thái `Reversed`: phải sửa chứng từ hoàn tất |

## 2. Giả định tạm cho câu hỏi nghiệp vụ còn mở

Thiết kế dùng các giả định dưới đây **để không dừng thiết kế**; mỗi giả định ảnh hưởng ít hoặc có thể đổi bằng migration nhỏ. **Không** coi là quyết định nghiệp vụ.

| Câu hỏi | Giả định đang dùng | Ảnh hưởng nếu đổi |
|---|---|---|
| Q-47 Ai đề xuất phương án chênh lệch | Bất kỳ người có `HandoverDiscrepancy.Propose`; DB chỉ ép người duyệt ≠ người đề xuất | Chỉ thay seed `RolePermission` |
| Q-48 Ảnh hưởng số dư của 4 phương án | Bảng ở financial-data-design mục 5.7 | Đổi logic Domain; cấu trúc bảng giữ nguyên (`Direction`, `TargetType`) |
| Q-49 Người tạo / duyệt đảo giao dịch, hủy sau duyệt | DB chỉ ép người duyệt ≠ người tạo; quyền cụ thể qua permission | Chỉ thay seed |
| Q-50 Phiếu chi có bước "đã chi" | Không có: `Approved` là hoàn tất | Có: thêm trạng thái `AwaitingPayment` + cột `CompletedAt/By` cho `ExpenseVoucher` (như `RefundVoucher`) |
| Q-51 Miễn giảm khi đã nộp | Không cho miễn giảm vượt nợ chưa thanh toán (CHECK của `StudentDebt`) | Nếu cho: cần luồng chuyển phần đã nộp vượt sang số dư cá nhân |
| Q-52 Số dư âm do đảo / thiếu hụt | Không cho (`CHECK BalanceAmount >= 0`, `ActualAmount >= 0`) | Nếu cho: bỏ / nới CHECK, thêm cảnh báo |
| Q-53 Phiếu thu nhiều học sinh | Một phiếu – một học sinh (`CollectionReceipt.StudentId`) | Nếu cho: chuyển `StudentId` xuống `ReceiptAllocation`, phần thừa phải chỉ định học sinh |
| Q-54 Kết hợp nhiều phương án | Cho phép: biên bản đóng khi Σ phương án `Approved` = \|chênh lệch\| | Nếu không: thêm unique một phương án `Approved` / biên bản |
| Q-55 Miễn giảm do GVCN đề nghị ai duyệt | DB ép người duyệt ≠ người đề nghị | Chỉ thay quyền |
| Q-56 Phiếu hoàn trả đang chờ khi học sinh chuyển lớp | Vẫn giữ chỗ và xử lý ở lớp cũ; phần còn lại mới chờ bàn giao | Đổi logic Domain khi chuyển lớp |

## 3. Câu hỏi kỹ thuật cần xác nhận

| Mã | Câu hỏi | Đề xuất |
|---|---|---|
| DQ-01 | Phiên bản PostgreSQL (`training-doc.md` ghi "tự chọn") | **PostgreSQL 16 hoặc 17**. Tối thiểu 15 vì dùng `UNIQUE … NULLS NOT DISTINCT` |
| DQ-02 | Lưu file đính kèm ở đâu (đĩa máy chủ, object storage, …)? | Database chỉ lưu metadata + `StorageKey` + `Sha256`; nơi lưu chốt khi thiết kế hạ tầng |
| DQ-03 | Có cần **số chứng từ** dễ đọc (ví dụ `PT-6A-2026-0001`) không? BA chưa yêu cầu | Chưa thêm. Nếu cần: bảng đếm theo (quỹ, loại chứng từ) khóa dòng khi cấp số, cột `DocumentNo` unique |
| DQ-04 | Tài khoản database của ứng dụng có quyền gì? | Chỉ DML trên schema ứng dụng, không `TRUNCATE`, không tắt trigger; migration chạy bằng tài khoản riêng |

## 4. Rủi ro về tính toàn vẹn tài chính

| # | Rủi ro | Mức | Giảm thiểu |
|---|---|---|---|
| 1 | **Cache lệch sổ cái** do code cập nhật số dư không qua service chuẩn | Cao | Chỉ một service Domain được đổi số liệu tổng hợp; `CHECK` chặn giá trị bất hợp lệ; truy vấn đối soát (financial-data-design mục 9) chạy định kỳ và trước khóa sổ |
| 2 | **Bỏ qua trigger** (tài khoản có quyền DDL, `session_replication_role`) | Cao | DQ-04: tách tài khoản migration và tài khoản ứng dụng |
| 3 | **Giả định Q-48 / Q-52 / Q-54 sai** làm logic điều chỉnh sai | Trung bình | Đã khoanh vùng trong `FinancialAdjustment` + Domain; cấu trúc bảng không đổi |
| 4 | **Tiền mặt thực tế khác sổ** trong thời gian chờ bàn giao | Trung bình | Báo cáo "tiền ghi sổ và người đang giữ tiền" (FR-RPT-13); biên bản chênh lệch |
| 5 | **Đảo phiếu thu có phần thừa đã dùng / đã hoàn trả** | Trung bình | `CHECK ActualAmount >= 0` chặn; cần quyết định Q-52 |
| 6 | **Liên kết mềm** trong `AuditLog` / `DocumentStatusHistory` trỏ tới Id không tồn tại | Thấp | Chỉ ghi trong cùng transaction với chứng từ; không xóa chứng từ |
| 7 | **Deadlock** khi khóa nhiều quỹ / nhiều khoản nợ | Thấp | Thứ tự khóa cố định; retry có giới hạn |
| 8 | **Ngày nghiệp vụ vs thời điểm hệ thống** (khóa sổ theo `ClosedAt`, chứng từ có `ReceivedOn` trước ngày khóa nhưng nhập sau) | Thấp | Trạng thái khóa dựa trên `ClassFund.Status` tại thời điểm ghi, không dựa trên ngày nghiệp vụ; ghi rõ `IsPostClose` |
