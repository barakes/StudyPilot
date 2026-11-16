using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StudyPilot.Api.Models;
using StudyPilot.Api.Services;

namespace StudyPilot.Api.Tests;

public class TokenServiceTests
{
  private static readonly JwtSettings Settings = new()
  {
    Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
    Issuer = "test-issuer",
    Audience = "test-audience",
    ExpiryMinutes = 30
  };

  private static readonly User TestUser = new()
  {
    Id = 42,
    Email = "bar@example.com",
    DisplayName = "Bar"
  };

  private static string CreateToken() =>
      new TokenService(Options.Create(Settings)).CreateToken(TestUser);

  private static TokenValidationParameters ValidationParameters(string base64Key) => new()
  {
    ValidIssuer = Settings.Issuer,
    ValidAudience = Settings.Audience,
    IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(base64Key))
  };

  [Fact]
  public void CreateToken_ContainsUserDetails()
  {
    var token = new JsonWebToken(CreateToken());

    Assert.Equal("42", token.Subject);
    Assert.Equal("bar@example.com", token.GetClaim(JwtRegisteredClaimNames.Email).Value);
    Assert.Equal("Bar", token.GetClaim(JwtRegisteredClaimNames.Name).Value);
  }

  [Fact]
  public void CreateToken_ContainsIssuerAndAudience()
  {
    var token = new JsonWebToken(CreateToken());

    Assert.Equal("test-issuer", token.Issuer);
    Assert.Contains("test-audience", token.Audiences);
  }

  [Fact]
  public void CreateToken_ExpiresAfterConfiguredMinutes()
  {
    var now = DateTime.UtcNow;

    var token = new JsonWebToken(CreateToken());

    Assert.InRange(token.ValidTo, now.AddMinutes(29), now.AddMinutes(31));
  }

  [Fact]
  public async Task CreateToken_WithCorrectKey_IsValid()
  {
    var result = await new JsonWebTokenHandler()
        .ValidateTokenAsync(CreateToken(), ValidationParameters(Settings.Key));

    Assert.True(result.IsValid);
  }

  [Fact]
  public async Task CreateToken_WithDifferentKey_IsRejected()
  {
    var attackerKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    var result = await new JsonWebTokenHandler()
        .ValidateTokenAsync(CreateToken(), ValidationParameters(attackerKey));

    Assert.False(result.IsValid);
  }

  [Fact]
  public void CreateToken_NeverContainsThePasswordHash()
  {
    TestUser.PasswordHash = "secret-hash-value";

    var token = new JsonWebToken(CreateToken());

    Assert.DoesNotContain(token.Claims, claim => claim.Value.Contains("secret-hash-value"));
  }
}