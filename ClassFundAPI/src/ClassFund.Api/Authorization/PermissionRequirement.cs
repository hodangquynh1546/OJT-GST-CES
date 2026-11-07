using Microsoft.AspNetCore.Authorization;

namespace ClassFund.Api.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
