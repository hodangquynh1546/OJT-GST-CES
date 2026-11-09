# Features

Mỗi module nghiệp vụ một thư mục (`Auth/`, `Funds/`, `Students/`, `Treasurers/`, `Categories/`, `Transactions/`, `Reports/`…), chứa service, model (DTO), interface và hằng số audit / mã lỗi riêng của module.

- Class tên kết thúc bằng `Service` trong namespace `ClassFund.Application.Features.*` được **tự đăng ký DI** (scoped, kèm interface của nó) – không sửa `ServiceRegistration.cs`.
- Quy ước: [docs/architecture/foundation-guide.md](../../../../docs/architecture/foundation-guide.md), [docs/api/api-conventions.md](../../../../docs/api/api-conventions.md).
