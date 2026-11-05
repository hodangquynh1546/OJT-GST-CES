using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using SchoolFund.Api.ErrorHandling;
using SchoolFund.Api.Middleware;

namespace SchoolFund.Api.Hosting;

public static class PipelineRegistration
{
    /// <summary>Thứ tự theo training-doc.md mục 2d: CorrelationId, xác thực, CurrentUser, RequirePermission.</summary>
    public static WebApplication UseSchoolFundPipeline(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging();
        app.UseExceptionHandler(_ => { });

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                foreach (ApiVersionDescription description in app.DescribeApiVersions())
                {
                    options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName);
                }
            });
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseMiddleware<CurrentUserMiddleware>();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHealthChecks("/api/health", new HealthCheckOptions()).AllowAnonymous();

        // API không khớp route phải trả 404 ProblemDetails, không rơi vào trang SPA.
        app.MapFallback("/api/{**path}", context => ApiProblems.WriteAsync(
            context, StatusCodes.Status404NotFound, "not_found", "Không tìm thấy dữ liệu."));
        app.MapFallbackToFile("index.html");

        return app;
    }
}
