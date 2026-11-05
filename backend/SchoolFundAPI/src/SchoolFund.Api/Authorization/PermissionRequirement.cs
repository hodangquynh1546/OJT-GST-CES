using Microsoft.AspNetCore.Authorization;

namespace SchoolFund.Api.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
