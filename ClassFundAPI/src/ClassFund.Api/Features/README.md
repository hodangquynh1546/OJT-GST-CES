# Features (Api)

Mỗi module một thư mục `Features/<Module>/` gồm `<Module>Controller.cs` và `<Module>Requests.cs` (request + FluentValidation).

- Kế thừa `ApiControllerBase`; khai báo `[ApiVersion(1)]` và `[Route(ApiRoutes.Fund + "/...")]` (hoặc `ApiRoutes.Prefix`).
- Mỗi action có `[RequirePermission(Permissions.X)]` (hoặc `[AllowAnonymous]` rõ ràng). Không kiểm tra theo role.
- Controller mỏng: chỉ gọi service trong `Application/Features/<Module>/`. Không truy cập DbContext.
- Chi tiết quy ước: `docs/api/api-conventions.md`, quy trình làm module: `docs/architecture/foundation-guide.md`.
