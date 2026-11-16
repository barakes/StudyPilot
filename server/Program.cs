using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StudyPilot.Api.Data;
using StudyPilot.Api.Services;
using StudyPilot.Api.Services.Ai;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// JWT settings
var jwtSection = builder.Configuration.GetSection(JwtSettings.SectionName);
builder.Services.Configure<JwtSettings>(jwtSection);

var jwtSettings = jwtSection.Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings were not found.");

if (string.IsNullOrWhiteSpace(jwtSettings.Key))
{
    throw new InvalidOperationException("JWT key is missing. Set it with: dotnet user-secrets set \"Jwt:Key\" <value>");
}

// Authentication
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwtSettings.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name"
        };
    });

builder.Services.AddAuthorization();

// Application services
builder.Services.AddScoped<TokenService>();
builder.Services.AddSingleton<TextExtractionService>();

// AI question generation: the provider is chosen in configuration ("Ai:Provider")
var aiProvider = builder.Configuration["Ai:Provider"] ?? "Fake";

switch (aiProvider.ToLowerInvariant())
{
    case "fake":
        builder.Services.AddSingleton<IQuestionGenerator, FakeQuestionGenerator>();
        break;

    case "gemini":
        var geminiSection = builder.Configuration.GetSection(GeminiSettings.SectionName);
        builder.Services.Configure<GeminiSettings>(geminiSection);

        if (string.IsNullOrWhiteSpace(geminiSection["ApiKey"]))
        {
            throw new InvalidOperationException(
                "Gemini API key is missing. Set it with: dotnet user-secrets set \"Gemini:ApiKey\" <value>");
        }

        builder.Services.AddHttpClient<IQuestionGenerator, GeminiQuestionGenerator>(client =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(90);
        });
        break;

    default:
        throw new InvalidOperationException($"Unknown AI provider '{aiProvider}'. Supported providers: Fake, Gemini.");
}

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();