using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using SchoolFund.Api.Authorization;
using SchoolFund.Api.Filters;
using SchoolFund.Application.Common.Security;

namespace SchoolFund.UnitTests.Api;

public class PasswordChangeRequiredFilterTests
{
    private static AuthorizationFilterContext Run(bool mustChangePassword, params object[] endpointMetadata)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.MustChangePassword).Returns(mustChangePassword);
        var action = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor { EndpointMetadata = endpointMetadata });
        var context = new AuthorizationFilterContext(action, []);

        new PasswordChangeRequiredFilter(currentUser.Object).OnAuthorization(context);
        return context;
    }

    [Fact]
    public void Blocks_other_endpoints_while_password_is_temporary()
    {
        var result = Assert.IsType<ObjectResult>(Run(mustChangePassword: true).Result);

        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("password.change_required", problem.Extensions["code"]);
    }

    [Fact]
    public void Allows_me_change_password_and_logout()
    {
        Assert.Null(Run(mustChangePassword: true, new AllowTemporaryPasswordAttribute()).Result);
    }

    [Fact]
    public void Allows_anonymous_endpoints()
    {
        Assert.Null(Run(mustChangePassword: true, new AllowAnonymousAttribute()).Result);
    }

    [Fact]
    public void Does_nothing_once_password_is_changed()
    {
        Assert.Null(Run(mustChangePassword: false).Result);
    }
}
