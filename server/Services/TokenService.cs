using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StudyPilot.Api.Models;

namespace StudyPilot.Api.Services;

public class TokenService
{
  private readonly JwtSettings _settings;

  public TokenService(IOptions<JwtSettings> options)
  {
    _settings = options.Value;
  }

  public string CreateToken(User user)
  {
    var key = new SymmetricSecurityKey(Convert.FromBase64String(_settings.Key));

    var descriptor = new SecurityTokenDescriptor
    {
      Subject = new ClaimsIdentity(new[]
        {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.DisplayName)
            }),
      Issuer = _settings.Issuer,
      Audience = _settings.Audience,
      Expires = DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes),
      SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
    };

    return new JsonWebTokenHandler().CreateToken(descriptor);
  }
}