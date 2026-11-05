using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moq;
using SchoolFund.Api.Authorization;
using SchoolFund.Application.Common.Exceptions;
using SchoolFund.Application.Common.Security;
using SchoolFund.Domain.Authorization;

namespace SchoolFund.UnitTests.Api;

public class PermissionAuthorizationHandlerTests
{
    private static readonly Guid FundId = Guid.CreateVersion7();
    private readonly Mock<ICurrentUser> _user = new();
    private readonly Mock<IAccessService> _access = new();

    public PermissionAuthorizationHandlerTests()
    {
        _user.SetupGet(user => user.IsAuthenticated).Returns(true);
        _access.Setup(access => access.GetAccountPermissionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());
    }

    private void GivenRole(FundRole role) =>
        _access.Setup(access => access.GetFundAccessAsync(FundId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FundAccess(FundId, role, RolePermissions.For(role)));

    private async Task<AuthorizationHandlerContext> AuthorizeAsync(string permission, string? routeFundId)
    {
        var httpContext = new DefaultHttpContext();
        if (routeFundId is not null)
        {
            httpContext.Request.RouteValues[PermissionAuthorizationHandler.FundIdRouteKey] = routeFundId;
        }

        var handler = new PermissionAuthorizationHandler(
            new HttpContextAccessor { HttpContext = httpContext },
            _user.Object,
            _access.Object);
        var context = new AuthorizationHandlerContext(
            [new PermissionRequirement(permission)],
            new ClaimsPrincipal(),
            resource: null);
        await handler.HandleAsync(context);
        return context;
    }

    [Fact]
    public async Task Role_with_the_permission_succeeds()
    {
        GivenRole(FundRole.Treasurer);

        var context = await AuthorizeAsync(Permissions.TransactionManage, FundId.ToString());

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Role_without_the_permission_gets_forbidden()
    {
        GivenRole(FundRole.Teacher);

        var exception = await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => AuthorizeAsync(Permissions.TransactionManage, FundId.ToString()));

        Assert.Equal(ForbiddenAccessException.DefaultCode, exception.Code);
    }

    [Fact]
    public async Task User_outside_the_fund_gets_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => AuthorizeAsync(Permissions.FundView, FundId.ToString()));
    }

    [Fact]
    public async Task Malformed_fund_id_gets_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => AuthorizeAsync(Permissions.FundView, "khong-phai-guid"));
    }

    [Fact]
    public async Task Temporary_password_blocks_with_its_own_code()
    {
        GivenRole(FundRole.Student);
        _user.SetupGet(user => user.MustChangePassword).Returns(true);

        var exception = await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => AuthorizeAsync(Permissions.FundView, FundId.ToString()));

        Assert.Equal(ForbiddenAccessException.PasswordChangeRequiredCode, exception.Code);
    }

    [Fact]
    public async Task Without_fund_in_route_uses_account_permissions()
    {
        _access.Setup(access => access.GetAccountPermissionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { Permissions.FundCreate });

        var context = await AuthorizeAsync(Permissions.FundCreate, routeFundId: null);

        Assert.True(context.HasSucceeded);
        _access.Verify(
            access => access.GetFundAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Anonymous_user_is_left_to_the_401_challenge()
    {
        _user.SetupGet(user => user.IsAuthenticated).Returns(false);

        var context = await AuthorizeAsync(Permissions.FundView, FundId.ToString());

        Assert.False(context.HasSucceeded);
        _access.VerifyNoOtherCalls();
    }
}
