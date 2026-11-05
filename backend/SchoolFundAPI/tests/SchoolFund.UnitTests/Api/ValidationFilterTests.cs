using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SchoolFund.Api.Filters;

namespace SchoolFund.UnitTests.Api;

public class ValidationFilterTests
{
    public sealed record SampleRequest(string FullName);

    private sealed class SampleValidator : AbstractValidator<SampleRequest>
    {
        public SampleValidator() => RuleFor(request => request.FullName).NotEmpty().WithMessage("Nhập họ tên.");
    }

    private static async Task<(ActionExecutingContext Context, bool NextCalled)> RunAsync(SampleRequest request)
    {
        var services = new ServiceCollection()
            .AddScoped<IValidator<SampleRequest>, SampleValidator>()
            .BuildServiceProvider();
        var action = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var context = new ActionExecutingContext(
            action,
            [],
            new Dictionary<string, object?> { ["request"] = request },
            controller: new object());

        var nextCalled = false;
        await new ValidationFilter(services).OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(action, [], new object()));
        });
        return (context, nextCalled);
    }

    [Fact]
    public async Task Valid_request_reaches_the_action()
    {
        var (context, nextCalled) = await RunAsync(new SampleRequest("Nguyễn Minh Khang"));

        Assert.True(nextCalled);
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task Invalid_request_returns_field_errors()
    {
        var (context, nextCalled) = await RunAsync(new SampleRequest(""));

        Assert.False(nextCalled);
        var result = Assert.IsType<BadRequestObjectResult>(context.Result);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(ValidationFilter.FailedCode, problem.Extensions["code"]);
        var errors = Assert.IsAssignableFrom<IDictionary<string, string[]>>(problem.Extensions["errors"]);
        Assert.Equal(["Nhập họ tên."], errors["fullName"]);
    }
}
