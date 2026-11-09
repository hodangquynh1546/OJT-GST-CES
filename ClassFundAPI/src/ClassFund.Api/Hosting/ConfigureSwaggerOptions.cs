using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ClassFund.Api.Hosting;

/// <summary>Sinh một tài liệu OpenAPI cho mỗi phiên bản API.</summary>
public sealed class ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider) : IConfigureOptions<SwaggerGenOptions>
{
    private const string AntiforgeryScheme = "Antiforgery";

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "ClassFund API",
                Version = description.ApiVersion.ToString(),
            });
        }

        // Hợp đồng chặt cho FE: thuộc tính không-null là required, không có "nullable" giả.
        options.SupportNonNullableReferenceTypes();
        options.NonNullableReferenceTypesAsRequired();
        options.OperationFilter<JsonOnlyContentFilter>();

        // Nút "Authorize" để dán token antiforgery: Swagger UI tự gửi cookie, còn header X-XSRF-TOKEN phải nhập tay.
        options.AddSecurityDefinition(AntiforgeryScheme, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = ServiceRegistration.AntiforgeryHeaderName,
            Description = "Lấy antiforgeryToken từ GET /auth/antiforgery (hoặc từ response login / register-teacher / change-password) rồi dán vào đây.",
        });
        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(AntiforgeryScheme, document)] = [],
        });
    }
}

/// <summary>API chỉ trao đổi JSON (lỗi: application/problem+json); bỏ text/plain, text/json khỏi Swagger cho gọn hợp đồng.</summary>
public sealed class JsonOnlyContentFilter : IOperationFilter
{
    private static readonly string[] Allowed = ["application/json", "application/problem+json"];

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.RequestBody?.Content is { } request)
        {
            Trim(request);
        }

        foreach (var response in operation.Responses?.Values ?? Enumerable.Empty<IOpenApiResponse>())
        {
            if (response.Content is { } content)
            {
                Trim(content);
            }
        }
    }

    private static void Trim(IDictionary<string, OpenApiMediaType> content)
    {
        foreach (var mediaType in content.Keys.Where(key => !Allowed.Contains(key)).ToList())
        {
            content.Remove(mediaType);
        }
    }
}
