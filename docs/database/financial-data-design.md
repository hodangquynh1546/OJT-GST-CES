# Số liệu tài chính

## 1. Công thức (BR-02, BR-10)

```text
Số dư hiện tại        = Σ Amount(Income, chưa hủy) − Σ Amount(Expense, chưa hủy)
Số dư đầu tháng T     = Σ có dấu của giao dịch chưa hủy có OccurredOn < ngày 1 tháng T
Tổng thu / chi tháng T = Σ Amount theo loại, OccurredOn trong tháng T, chưa hủy
Số dư cuối tháng T    = Số dư đầu tháng T + Tổng thu T − Tổng chi T
⇒ Số dư cuối tháng T  = Số dư đầu tháng T+1 (luôn đúng, vì cùng một công thức)
```

## 2. Vì sao không lưu số dư
Mọi con số đều tính trực tiếp từ bảng giao dịch: sửa số tiền, đổi ngày sang tháng khác hay hủy giao dịch đều tự phản ánh vào số dư và mọi báo cáo – không có bảng tổng hợp phải cập nhật song song, nên không thể lệch. Với quy mô một lớp (vài trăm giao dịch / năm) truy vấn tổng có index `(FundId, OccurredOn)` là đủ nhanh.

## 3. Ví dụ
| Tháng | Giao dịch | Đầu tháng | Thu | Chi | Cuối tháng |
|---|---|---|---|---|---|
| 9 | Thu 1.000.000; chi 200.000 | 0 | 1.000.000 | 200.000 | 800.000 |
| 10 | Thu 300.000; chi 450.000 | 800.000 | 300.000 | 450.000 | 650.000 |
| 11 | – | 650.000 | 0 | 0 | 650.000 |

Sửa khoản thu tháng 9 thành 1.100.000 → cuối tháng 9 = 900.000, đầu tháng 10 = 900.000, cuối tháng 10 = 750.000.

## 4. Số dư âm
Được phép (D-03). API trả `isNegative = true` kèm số dư để giao diện cảnh báo.

## 5. Bản công khai cho phụ huynh và học sinh chưa phân công (v1.1, D-09; 2.0 – thiết kế hiện hành, Not Implemented)
```text
Số dư, tổng thu, tổng chi, số liệu tháng = công thức mục 1 (KHÔNG lọc IsPublic)
Danh sách khoản            = giao dịch IsPublic (trạng thái theo bộ lọc; mặc định chưa hủy – D-18)
Khoản khác (thu / chi) tháng T = Σ Amount theo loại, chưa hủy, NOT IsPublic, trong tháng T
Theo danh mục (bản công khai)  = danh mục của khoản công khai + 1 dòng "Khoản khác" cho mỗi loại
⇒ Σ dòng hiển thị = tổng thu / chi tháng (luôn khớp)
```
Ví dụ tháng 11: thu 3.200.000 (công khai) + 300.000 (không công khai) → phụ huynh thấy dòng "Đóng quỹ 3.200.000" và dòng "Khoản khác 300.000"; tổng thu 3.500.000 ở cả 3 vai trò.

## 6. Giới hạn riêng tư của "Khoản khác" (B1 = **P1** đã chốt 2026-11-05)
- Phụ huynh xem được số dư chính xác và số dư đầu / cuối mỗi tháng.
- Vì vậy luôn tính được **tổng** khoản ẩn của tháng.
- Nếu tháng chỉ có một khoản ẩn, số tiền của khoản đó suy ra được.
- Phương án xử lý và ảnh hưởng tới đối chiếu: [open-questions.md](../requirements/open-questions.md) mục 4.
- Thiết kế hiện tại không hiển thị số lượng khoản ẩn.
- **Đã chốt P1:** bảo vệ nội dung, ngày, danh mục, số lượng; chấp nhận số tiền gộp có thể suy ra; form thủ quỹ cảnh báo khi tắt công khai. API trả dòng "Khoản khác" với `categoryId = null`, `count = null`.
