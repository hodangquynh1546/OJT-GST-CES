using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.ErrorHandling;

namespace ClassFund.Api.Common;

/// <summary>
/// Lớp cha cho mọi controller nghiệp vụ (docs/api/api-conventions.md): lỗi chung khai báo sẵn cho Swagger.
/// Không đặt <c>[Produces]</c> ở đây: nó ghi đè content type <c>application/problem+json</c> của lỗi.
/// Mỗi controller vẫn tự khai báo <c>[ApiVersion(1)]</c> và <c>[Route(ApiRoutes.Prefix + "/…")]</c>.
/// </summary>
[ApiController]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ApiProblems.ContentType)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ApiProblems.ContentType)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ApiProblems.ContentType)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, ApiProblems.ContentType)]
public abstract class ApiControllerBase : ControllerBase;

public static class ApiRoutes
{
    /// <summary>Tiền tố mọi route: <c>/api/v1/…</c>.</summary>
    public const string Prefix = "api/v{version:apiVersion}";

    /// <summary>Route theo quỹ: <c>/api/v1/funds/{fundId}/…</c>; <c>fundId</c> chỉ chọn quỹ, quyền do backend suy ra.</summary>
    public const string Fund = Prefix + "/funds/{fundId:guid}";
}
