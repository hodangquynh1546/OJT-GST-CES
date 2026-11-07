using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using ClassFund.Api.ErrorHandling;
using ClassFund.Api.Middleware;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Domain.Exceptions;

namespace ClassFund.UnitTests.Api;

public class GlobalExceptionHandlerTests
{
    public static TheoryData<Exception, int, string> Cases => new()
    {
        { new BusinessRuleException("treasurer.last", "x"), 422, "treasurer.last" },
        { new NotFoundException(), 404, "not_found" },
        { new ForbiddenAccessException(), 403, "forbidden" },
        { new ForbiddenAccessException(ForbiddenAccessException.PasswordChangeRequiredCode), 403, "password.change_required" },
        { new ConflictException("treasurer.already_revoked", "x"), 409, "treasurer.already_revoked" },
        { new InvalidOperationException("bí mật kỹ thuật"), 500, "internal_error" },
    };

    private static async Task<(DefaultHttpContext Context, string Body)> HandleAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Items[CorrelationIdMiddleware.ItemKey] = "abc";
        context.Response.Body = new MemoryStream();

        var handled = await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
            .TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        context.Response.Body.Position = 0;
        return (context, await new StreamReader(context.Response.Body).ReadToEndAsync());
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Exception_maps_to_status_and_code(Exception exception, int status, string code)
    {
        var (context, body) = await HandleAsync(exception);
        var json = JsonDocument.Parse(body).RootElement;

        Assert.Equal(status, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        Assert.Equal(code, json.GetProperty("code").GetString());
        Assert.Equal("abc", json.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task Unexpected_error_does_not_leak_details()
    {
        var (_, body) = await HandleAsync(new InvalidOperationException("bí mật kỹ thuật"));

        Assert.DoesNotContain("bí mật", body);
    }
}
