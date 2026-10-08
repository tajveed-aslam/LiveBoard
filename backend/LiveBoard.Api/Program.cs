using System.Text;
using LiveBoard.Api.Data;
using LiveBoard.Api.Infrastructure;
using LiveBoard.Api.Options;
using LiveBoard.Api.Services;
using LiveBoard.Api.Services.Llm;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwtOptions.Key) < 32)
    throw new InvalidOperationException(
        "Jwt:Key must be a secret of at least 32 characters. Copy appsettings.Development.example.json " +
        "to appsettings.Development.json and fill it in.");

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<OpenAiOptions>(builder.Configuration.GetSection(OpenAiOptions.SectionName));
builder.Services.Configure<AnalysisOptions>(builder.Configuration.GetSection(AnalysisOptions.SectionName));
builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection(DemoOptions.SectionName));
builder.Services.AddAppRateLimiting(
    builder.Configuration.GetSection(DemoOptions.SectionName).Get<DemoOptions>() ?? new DemoOptions());

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(ConnectionStrings.NormalizePostgres(builder.Configuration.GetConnectionString("Default"))));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));
var llmProvider = builder.Configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>()?.Provider ?? "Gemini";
if (llmProvider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddHttpClient<ILlmClient, GeminiClient>(client => client.Timeout = TimeSpan.FromMinutes(3));
else if (llmProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddHttpClient<ILlmClient, OpenAiChatClient>(client => client.Timeout = TimeSpan.FromMinutes(3));
else
    throw new InvalidOperationException($"Unknown Llm:Provider '{llmProvider}'. Use \"Gemini\" or \"OpenAI\".");
builder.Services.AddScoped<IAnalysisService, AnalysisService>();
builder.Services.AddSingleton<ITokenService, TokenService>();

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddControllers()
    .AddJsonOptions(o => JsonDefaults.Configure(o.JsonSerializerOptions));

// Browsers send Origin without a trailing slash; tolerate one (or stray spaces) in the configured value.
var corsOrigins = (builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .Select(o => o.Trim().TrimEnd('/'))
    .Where(o => o.Length > 0)
    .ToArray();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "LiveBoard API", Version = "v1" });
    var bearer = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    c.AddSecurityDefinition("Bearer", bearer);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearer] = [] });
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
// After authentication so the generation limit can be partitioned by user (and guest vs. registered).
app.UseRateLimiter();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program { }
