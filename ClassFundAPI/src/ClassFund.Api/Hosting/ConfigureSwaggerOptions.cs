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
                Title = "Quỹ Lớp API",
                Version = description.ApiVersion.ToString(),
            });
        }

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
