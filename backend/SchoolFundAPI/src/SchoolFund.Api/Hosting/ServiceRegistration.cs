using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Formatting.Compact;
using SchoolFund.Api.Authorization;
using SchoolFund.Api.ErrorHandling;
using SchoolFund.Api.Filters;
using SchoolFund.Api.Health;
using SchoolFund.Application.Common.Auditing;
using SchoolFund.Application.Common.Security;
using SchoolFund.Application.Features.Auth;
using SchoolFund.Application.Features.Categories;
using SchoolFund.Application.Features.Funds;
using SchoolFund.Application.Features.Students;
using SchoolFund.Application.Features.Transactions;
using SchoolFund.Infrastructure;
using SchoolFund.Infrastructure.Security;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SchoolFund.Api.Hosting;

public static class ServiceRegistration
{
    public const string SessionCookieName = "sf_session";
    public const string AntiforgeryCookieName = "sf_xsrf";
    public const string AntiforgeryHeaderName = "X-XSRF-TOKEN";

    /// <summary>Khóa bí mật (pepper) cho băm mật khẩu – biến môi trường / user-secrets <c>SECRET_KEY</c>.</summary>
    public const string PasswordPepperSetting = "SECRET_KEY";

    public static WebApplicationBuilder AddSchoolFundServices(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var connectionString = builder.Configuration.GetConnectionString("App");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Thiếu ConnectionStrings:App. Đặt bằng user-secrets hoặc biến môi trường ConnectionStrings__App (file .env).");
        }

        // Pepper trộn vào mật khẩu trước khi băm. Không có hoặc quá ngắn → không khởi động, để không sinh hash sai khóa.
        var passwordPepper = builder.Configuration[PasswordPepperSetting];
        if (string.IsNullOrWhiteSpace(passwordPepper) || passwordPepper.Length < AspNetPasswordHasher.MinimumPepperLength)
        {
            throw new InvalidOperationException(
                $"Thiếu {PasswordPepperSetting} (tối thiểu {AspNetPasswordHasher.MinimumPepperLength} ký tự). "
                + "Đặt trong file .env hoặc user-secrets; tạo ngẫu nhiên bằng: openssl rand -base64 48");
        }

        services.AddSerilog((provider, logger) => logger
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(provider)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .WriteTo.Async(sink => sink.Console(new CompactJsonFormatter())));

        services.AddInfrastructure(connectionString, passwordPepper);
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddHttpContextAccessor();
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", timeout: TimeSpan.FromSeconds(5));
        services.AddValidatorsFromAssemblyContaining<ValidationFilter>();

        services.AddControllers(options =>
            {
                // Mặc định mọi endpoint cần đăng nhập; endpoint công khai phải khai báo [AllowAnonymous].
                options.Filters.Add(new AuthorizeFilter());
                options.Filters.Add<PasswordChangeRequiredFilter>();
                options.Filters.Add<AntiforgeryFilter>();
                options.Filters.Add<ValidationFilter>();
            })
            // Enum trong API là chuỗi (Income, Expense), không nhận số.
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter(allowIntegerValues: false)))
            .ConfigureApiBehaviorOptions(options =>
                options.InvalidModelStateResponseFactory = context =>
                    ValidationFilter.ToResult(context.HttpContext, context.ModelState));

        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1);
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'V";
                options.SubstituteApiVersionInUrl = true;
            });

        services.AddSwaggerGen();
        services.AddSingleton<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

        var secure = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

        services.AddAntiforgery(options =>
        {
            options.HeaderName = AntiforgeryHeaderName;
            options.Cookie.Name = AntiforgeryCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = secure;
        });

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = SessionCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = secure;
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.Events.OnRedirectToLogin = context => ApiProblems.WriteAsync(
                    context.HttpContext, StatusCodes.Status401Unauthorized, "unauthorized", "Bạn chưa đăng nhập.");
                options.Events.OnRedirectToAccessDenied = context => ApiProblems.WriteAsync(
                    context.HttpContext, StatusCodes.Status403Forbidden, "forbidden", "Bạn không có quyền thực hiện thao tác này.");
            });

        services.AddAuthorization();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddRateLimiter(options => ConfigureRateLimiting(options, builder.Configuration));

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IRequestInfo, HttpRequestInfo>();
        services.AddScoped<AuditTrail>();
        services.AddScoped<AuthService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IFundService, FundService>();
        services.AddScoped<IStudentService, StudentService>();
        services.AddScoped<TransactionService>();

        services.AddScoped<CurrentUser>();
        services.AddScoped<ICurrentUser>(provider => provider.GetRequiredService<CurrentUser>());

        return builder;
    }

    private static void ConfigureRateLimiting(RateLimiterOptions options, IConfiguration configuration)
    {
        // Để trống hoặc sai định dạng (ví dụ biến rỗng trong .env) → dùng mặc định.
        var permitLimit = int.TryParse(configuration[RateLimitPolicies.AuthPermitLimitSetting], out var configured) && configured > 0
            ? configured
            : RateLimitPolicies.DefaultAuthPermitLimit;

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = (context, _) => new ValueTask(ApiProblems.WriteAsync(
            context.HttpContext,
            StatusCodes.Status429TooManyRequests,
            RateLimitPolicies.RejectedCode,
            "Bạn đã thử quá nhiều lần. Vui lòng chờ 1 phút rồi thử lại."));
        options.AddPolicy(RateLimitPolicies.Auth, httpContext => RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    }
}
