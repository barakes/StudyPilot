using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace StudyPilot.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
  public static int GetUserId(this ClaimsPrincipal principal)
  {
    var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

    return int.TryParse(value, out var userId)
        ? userId
        : throw new InvalidOperationException("The user id claim is missing or invalid.");
  }
}