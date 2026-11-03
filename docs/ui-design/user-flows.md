# User Flows

> Phiên bản: 0.1 (bản nháp, chờ duyệt) · Ngày: 2026-11-03
> Mỗi luồng tương ứng quy trình nghiệp vụ P-xx trong [business-rules-and-processes.md](../business-analysis/business-rules-and-processes.md). Mã màn hình: [screen-specifications.md](screen-specifications.md).

## Danh sách luồng

| Mã | Luồng | Vai trò | Quy trình BA |
|---|---|---|---|
| UF-01 | Thiết lập năm học & mời tài khoản | Admin → GVCN → Phụ huynh | P-01 |
| UF-02 | Tạo đợt thu | GVCN / Thủ quỹ | P-02 |
| UF-03 | Thủ quỹ ghi thu tiền mặt | Thủ quỹ | P-02 |
| UF-04 | Phụ huynh báo chuyển khoản → thủ quỹ xác nhận | Phụ huynh → Thủ quỹ | P-02 |
| UF-05 | Miễn giảm | Thủ quỹ / GVCN → GVCN | P-03 |
| UF-06 | Lập & duyệt phiếu chi | Thủ quỹ → GVCN | P-04 |
| UF-07 | Hoàn trả tiền thừa | Thủ quỹ → GVCN → Thủ quỹ | P-06 |
| UF-08 | Hủy / đảo chứng từ | Người tạo / người có quyền | P-07 |
| UF-09 | Chốt sổ năm học | GVCN | P-08 |
| UF-10 | Chuyển lớp / lên lớp & bàn giao số dư cá nhân | Admin → Thủ quỹ lớp cũ → Thủ quỹ lớp mới | P-09, P-12 |
| UF-11 | Thu hộ nợ cũ & bàn giao tiền thu hộ | Thủ quỹ lớp mới → Người giữ quỹ lớp gốc | P-11, P-14, P-15 |
| UF-12 | Xử lý chênh lệch bàn giao | Người nhận → người đề xuất → GVCN / Admin | P-17 |
| UF-13 | Chuyển quỹ giữa các lớp (kể cả sau khóa sổ) | GVCN → Admin | P-10, P-13 |
| UF-14 | Phân công người giữ quỹ lớp cũ | Admin | P-16 |

---

## UF-01 Thiết lập năm học & mời tài khoản
```text
[Admin] ADM-02 tạo năm học → ADM-03 tạo lớp → ADM-04 mời GVCN (email + lớp)
   → email → [GVCN] COM-02 kích hoạt → TCH-01
[GVCN] TCH-02 thêm học sinh → DLG-01 mời phụ huynh
   → email → [Phụ huynh] COM-02 kích hoạt → PAR-01
[GVCN] TCH-03 chọn một phụ huynh làm thủ quỹ → [Phụ huynh đó] có thêm phạm vi "Thủ quỹ · lớp"
```

## UF-02 Tạo đợt thu
```text
FND-01 "Tạo đợt thu" → kendo-window: tên, số tiền, hạn, chọn học sinh
  → xem trước "N học sinh được tự trừ số dư cá nhân"
  → Lưu → FND-02 (khoản nợ đã tạo; tự trừ đã hiển thị dưới dạng phiếu thu "Từ số dư cá nhân")
  → (tùy chọn) DLG-09 gửi nhắc nộp
```

## UF-03 Thủ quỹ ghi thu tiền mặt (mobile, ≤ 3 bước)
```text
TRS-01 nút "Ghi thu" → TRS-02
  1. Tìm học sinh
  2. Chọn khoản nợ (nợ hiện tại + nợ cũ có nhãn) → nhập số tiền → hệ thống gợi ý phân bổ
  3. Xem trước: "Trả nợ … · Vào số dư cá nhân …" → Lưu
  → toast "Đã ghi phiếu thu" → FND-06 phiếu thu
  (Nếu là thu hộ: liên kết "Cần bàn giao … về lớp …" → UF-11)
```
Nhánh lỗi: số còn có thể thu thay đổi do người khác vừa thu → hộp thoại xung đột → tải lại bước 2.

## UF-04 Phụ huynh báo chuyển khoản → thủ quỹ xác nhận
```text
[Phụ huynh] PAR-01 "Báo chuyển khoản" → PAR-03: chọn khoản nợ, số tiền, ngày, chụp biên lai → Gửi
   → khoản nợ được giữ chỗ; PAR-01 hiện "Chờ thủ quỹ xác nhận"
   → thông báo cho thủ quỹ
[Thủ quỹ] COM-06 / TRS-03 → xem biên lai → (đối chiếu tài khoản ngân hàng ngoài hệ thống)
   ├─ Xác nhận → phiếu Confirmed → thông báo phụ huynh "Đã xác nhận"
   └─ Từ chối (DLG-07, lý do) → giải phóng giữ chỗ → thông báo phụ huynh kèm lý do
[Phụ huynh] có thể hủy báo chuyển khoản khi còn chờ (DLG-08)
```

## UF-05 Miễn giảm
```text
FND-02 / FND-03 → DLG-02 (loại, số tiền, lý do) → Exemption chờ duyệt
[GVCN] COM-06 tab "Miễn giảm" → FND-06 → Duyệt (khoản nợ giảm) / Từ chối (DLG-07)
(GVCN tự lập đề nghị → người duyệt: Q-55)
```

## UF-06 Lập & duyệt phiếu chi
```text
[Thủ quỹ] TRS-05 "Lập phiếu chi" → nhập + đính chứng từ → thấy khả dụng còn lại → Gửi (giữ chỗ)
   → thông báo GVCN
[GVCN] COM-06 tab "Phiếu chi" → FND-06 (xem chứng từ) → Duyệt (thành bút toán) / Từ chối (DLG-07, giải phóng)
[Thủ quỹ] bị từ chối → sửa → gửi lại (giữ chỗ lại); hoặc hủy (DLG-08)
```

## UF-07 Hoàn trả tiền thừa
```text
[Thủ quỹ] TRS-06 "Lập phiếu" → chọn học sinh có số dư khả dụng → số tiền → Gửi (giữ chỗ số dư cá nhân)
[GVCN] COM-06 → Duyệt → phiếu "Chờ thực hiện hoàn trả" (vẫn giữ chỗ)
[Thủ quỹ] trả tiền cho phụ huynh → COM-06 tab "Hoàn trả chờ thực hiện" → DLG-05 (minh chứng) → Hoàn tất
```

## UF-08 Hủy / đảo chứng từ
```text
FND-06:
 ├─ Còn chờ + tôi là người tạo → "Hủy" (DLG-08, lý do) → Cancelled, giải phóng giữ chỗ
 ├─ Còn chờ + tôi là người duyệt → "Từ chối" (DLG-07)
 ├─ Đã duyệt, chưa hoàn tất (hoàn trả chờ thực hiện) → "Hủy sau duyệt" (quy trình: Q-49)
 └─ Đã hoàn tất → "Đảo giao dịch" (DLG-06: ảnh hưởng ngược, lý do) → đảo chờ duyệt (Q-49)
        → duyệt → chứng từ gốc mang nhãn "Đã đảo", có liên kết hai chiều
```

## UF-09 Chốt sổ năm học
```text
[GVCN] TCH-05
  Bước 1 Kiểm tra chứng từ chờ → còn chứng từ? → liên kết đi xử lý → quay lại
  Bước 2 Đối chiếu (số dư quỹ, tổng số dư cá nhân, tiền thu hộ chưa bàn giao) → tích "Đã đối chiếu"
  Bước 3 Xem hệ quả → "Chốt sổ" → xác nhận lần 2 → quỹ "Đã khóa sổ"
  → thông báo Quản trị: lớp cần người giữ quỹ khi năm học kết thúc (→ UF-14)
```

## UF-10 Chuyển lớp / lên lớp & bàn giao số dư cá nhân
```text
[Admin] ADM-05: chọn lớp nguồn → chọn học sinh → lớp đích, loại, ngày
   → kiểm tra ảnh hưởng (nợ cũ vẫn thuộc lớp gốc; số dư cá nhân → chờ bàn giao; hoàn trả đang chờ – Q-56)
   → DLG-10 Xác nhận (D-56)
   → thông báo thủ quỹ lớp cũ (cần lập bàn giao) và thủ quỹ lớp mới
[Thủ quỹ lớp cũ] TRS-08 tab "Cần lập bàn giao" → lập (số tiền, người nhận điền sẵn)
[Thủ quỹ lớp mới] COM-06 / TRS-08 "Chờ tôi nhận" → DLG-03
   ├─ Khớp → hoàn tất → số dư cá nhân khả dụng ở lớp mới
   └─ Không khớp → biên bản → UF-12 (số dư vẫn chưa khả dụng)
```

## UF-11 Thu hộ nợ cũ & bàn giao tiền thu hộ
```text
[Thủ quỹ lớp mới] TRS-02: học sinh → thấy "Nợ cũ – 6A / 2026-2027" (+ nhãn "Sau khóa sổ" nếu lớp gốc đã khóa)
   → ghi thu → tiền vào quỹ lớp gốc; lớp mình chỉ thấy "Đã thu hộ"
   → nếu tôi chính là người giữ quỹ lớp gốc → kết thúc (D-58)
   → ngược lại → TRS-07 tab "Tôi cần bàn giao" → chọn khoản của lớp gốc → lập bàn giao
        (người nhận: thủ quỹ lớp gốc / người giữ quỹ được phân công; chưa có người giữ quỹ → chặn, báo Quản trị)
[Người nhận] CUS-01 / TRS-07 → DLG-03 → khớp: hoàn tất / không khớp: UF-12
```

## UF-12 Xử lý chênh lệch bàn giao
```text
[Người nhận] DLG-03 nhập số thực nhận ≠ dự kiến → lý do + minh chứng → biên bản FND-07 (bàn giao "Có chênh lệch")
[Người đề xuất – Q-47] FND-07 → DLG-04: chọn loại phương án, số tiền, mô tả → Đề xuất
[Người duyệt] COM-06 tab "Phương án chênh lệch" → FND-07
   (GVCN lớp gốc; Quản trị nếu lớp gốc đã khóa sổ / không còn GVCN; không phải người đề xuất)
   ├─ Duyệt → ghi chứng từ theo loại (ảnh hưởng: Q-48) → tiến độ xử lý tăng
   │     → đủ chênh lệch → biên bản "Đã xử lý", bàn giao "Hoàn tất"
   └─ Từ chối (DLG-07) → biên bản vẫn mở, số dư không đổi → đề xuất phương án khác
```

## UF-13 Chuyển quỹ giữa các lớp
```text
[GVCN lớp chuyển] TCH-04 "Lập đề nghị" → lớp nhận, số tiền (≤ khả dụng), lý do → Gửi (giữ chỗ)
   (quỹ đã khóa sổ → tự thành "Chứng từ điều chỉnh sau khóa sổ")
[Admin] COM-06 tab "Chuyển quỹ" → FND-06 → Duyệt → hai bút toán; thông báo GVCN lớp nhận
                                          → Từ chối (DLG-07) → giải phóng giữ chỗ
```

## UF-14 Phân công người giữ quỹ lớp cũ
```text
[Admin] ADM-01 cảnh báo "Lớp đã kết thúc chưa có người giữ quỹ" → ADM-06
   → chọn lớp – năm học → chọn người (thủ quỹ cũ / người khác) → (tùy chọn) bật quyền cấp riêng + lý do → Lưu
   → thông báo người được phân công → [Người giữ quỹ] có phạm vi "Giữ quỹ · lớp cũ" → CUS-01
```
