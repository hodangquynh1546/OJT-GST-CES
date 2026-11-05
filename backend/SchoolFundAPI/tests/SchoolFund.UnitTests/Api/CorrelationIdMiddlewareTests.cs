using Microsoft.AspNetCore.Http;
using SchoolFund.Api.Middleware;

namespace SchoolFund.UnitTests.Api;

public class CorrelationIdMiddlewareTests
{
    private static async Task<string> RunAsync(string? supplied)
    {
        var context = new DefaultHttpContext();
        if (supplied is not null)
        {
            context.Request.Headers[CorrelationIdMiddleware.HeaderName] = supplied;
        }

        await new CorrelationIdMiddleware(_ => Task.CompletedTask).InvokeAsync(context);
        return (string)context.Items[CorrelationIdMiddleware.ItemKey]!;
    }

    [Fact]
    public async Task Valid_client_value_is_kept()
    {
        Assert.Equal("req-123", await RunAsync("req-123"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("có dấu và khoảng trắng")]
    public async Task Missing_or_unsafe_value_is_replaced(string? supplied)
    {
        var value = await RunAsync(supplied);

        Assert.NotEqual(supplied, value);
        Assert.Equal(32, value.Length);
    }
}
