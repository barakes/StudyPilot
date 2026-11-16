using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using StudyPilot.Api.Data;
using StudyPilot.Api.Dtos;
using StudyPilot.Api.Models;
using StudyPilot.Api.Services;

namespace StudyPilot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
  private static readonly PasswordHasher<User> _passwordHasher = new();

  private readonly AppDbContext _db;
  private readonly TokenService _tokenService;

  public AuthController(AppDbContext db, TokenService tokenService)
  {
    _db = db;
    _tokenService = tokenService;
  }

  [HttpPost("register")]
  public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
  {
    var email = NormalizeEmail(request.Email);

    if (await _db.Users.AnyAsync(u => u.Email == email))
    {
      return Conflict(new { message = "An account with this email already exists." });
    }

    var user = new User
    {
      Email = email,
      DisplayName = request.DisplayName.Trim()
    };
    user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

    _db.Users.Add(user);

    try
    {
      await _db.SaveChangesAsync();
    }
    catch (DbUpdateException)
    {
      return Conflict(new { message = "An account with this email already exists." });
    }

    return Ok(CreateAuthResponse(user));
  }

  [HttpPost("login")]
  public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
  {
    var email = NormalizeEmail(request.Email);
    var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == email);

    if (user is null)
    {
      return Unauthorized(new { message = "Invalid email or password." });
    }

    var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

    if (result == PasswordVerificationResult.Failed)
    {
      return Unauthorized(new { message = "Invalid email or password." });
    }

    if (result == PasswordVerificationResult.SuccessRehashNeeded)
    {
      user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
      await _db.SaveChangesAsync();
    }

    return Ok(CreateAuthResponse(user));
  }

  [Authorize]
  [HttpGet("me")]
  public async Task<ActionResult<UserDto>> Me()
  {
    var userIdValue = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

    if (!int.TryParse(userIdValue, out var userId))
    {
      return Unauthorized();
    }

    var user = await _db.Users.FindAsync(userId);

    if (user is null)
    {
      return Unauthorized();
    }

    return Ok(ToDto(user));
  }

  private AuthResponse CreateAuthResponse(User user) =>
      new(_tokenService.CreateToken(user), ToDto(user));

  private static UserDto ToDto(User user) =>
      new(user.Id, user.Email, user.DisplayName);

  private static string NormalizeEmail(string email) =>
      email.Trim().ToLowerInvariant();
}