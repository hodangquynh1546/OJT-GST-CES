# Features (Application)

Mỗi module một thư mục `Features/<Module>/`: `I<Module>Service`, `<Module>Service`, `<Module>Models` (DTO), `<Module>Errors`, `<Module>AuditActions`.

- Class kết thúc bằng `Service` trong namespace `ClassFund.Application.Features.*` được tự đăng ký DI (scoped) – không sửa file DI.
- Truy cập dữ liệu qua `IAppDb`; ghi nhiều bảng trong `InTransactionAsync`.
- Mọi thao tác ghi gọi `AuditTrail.Record(...)` cùng transaction; thông báo gọi `Notifier.Send(...)`.
- Quyền nghiệp vụ chi tiết (ví dụ thủ quỹ không được tự quản lý thủ quỹ khác) kiểm tra trong service bằng `ICurrentUser` + `IAccessService`.
