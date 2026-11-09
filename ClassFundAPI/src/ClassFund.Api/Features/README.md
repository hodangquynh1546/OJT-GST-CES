# Features (Api)

Mỗi module một thư mục: `<Module>Controller.cs`, `<Module>Requests.cs` (request record + FluentValidation validator). Controller kế thừa `ClassFund.Api.Common.ApiControllerBase`.

Validator được tự đăng ký (quét assembly). Quy ước: [docs/architecture/foundation-guide.md](../../../../docs/architecture/foundation-guide.md).
