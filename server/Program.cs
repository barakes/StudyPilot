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

// AI services: the provider is chosen in configuration ("Ai:Provider")
var aiProvider = builder.Configuration["Ai:Provider"] ?? "Fake";

switch (aiProvider.ToLowerInvariant())
{
    case "fake":
        builder.Services.AddSingleton<IQuestionGenerator, FakeQuestionGenerator>();
        builder.Services.AddSingleton<IAnswerGrader, FakeAnswerGrader>();
        break;

    case "gemini":
        var geminiSection = builder.Configuration.GetSection(GeminiSettings.SectionName);
        builder.Services.Configure<GeminiSettings>(geminiSection);

        if (string.IsNullOrWhiteSpace(geminiSection["ApiKey"]))
        {
            throw new InvalidOperationException(
                "Gemini API key is missing. Set it with: dotnet user-secrets set \"Gemini:ApiKey\" <value>");
        }

        builder.Services.AddHttpClient<GeminiClient>(client =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(90);
        });

        builder.Services.AddScoped<IQuestionGenerator, GeminiQuestionGenerator>();
        builder.Services.AddScoped<IAnswerGrader, GeminiAnswerGrader>();
        break;

    default:
        throw new InvalidOperationException($"Unknown AI provider '{aiProvider}'. Supported providers: Fake, Gemini.");
}

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply any pending database migrations on startup, so a fresh database is ready to use.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// In production, the built React client is served from wwwroot by this same server.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Unknown API addresses return 404, instead of falling through to the client page below.
app.Map("/api/{**path}", () => Results.NotFound());

// Any other address belongs to the React app (for example /quizzes/5), so return its index.html.
app.MapFallbackToFile("index.html");

app.Run();