using System.Text;
using Ascend.Api.Data;
using Ascend.Api.Events;
using Ascend.Api.Services;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);

// Storage backend: "Server=tcp:..." -> Azure SQL Database, otherwise a local SQLite
// file. On Azure the connection string arrives as the app setting
// ConnectionStrings__Default; locally it falls back to appsettings.json
// (SQLITE_PATH can still override for a custom file path).
var connectionString = Environment.GetEnvironmentVariable("SQLITE_PATH")
    ?? builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=ascend.db";
Db.Configure(connectionString);

// JWT bearer auth. Tokens are issued by /api/auth/* alongside the legacy
// ax_session cookie, so old cookie clients keep working untouched.
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwtKey = builder.Configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_KEY") ?? "";
if (!string.IsNullOrWhiteSpace(jwtKey))
    builder.Configuration["Jwt:Key"] = jwtKey;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "Ascend",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "Ascend",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                builder.Configuration["Jwt:Key"] ?? "dev-only-key-please-override-via-Jwt-Key")),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();

builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Ascend API", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT returned by /api/auth/login or /api/auth/register.",
    });
    o.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer")] = new List<string>(),
    });
});

builder.Services.AddCors(o => o.AddPolicy("Frontend", p => p
    .WithOrigins("http://localhost:3008", "http://localhost:3000")
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials()));

// Azure Kafka (Event Hubs Kafka surface), same pattern as MongoStudentApi:
// EVENT_HUB_CONNECTION_STRING env var wins over appsettings Kafka:ConnectionString.
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
var kafkaConnection = Environment.GetEnvironmentVariable("EVENT_HUB_CONNECTION_STRING");
if (!string.IsNullOrWhiteSpace(kafkaConnection))
    builder.Configuration[$"{KafkaOptions.SectionName}:ConnectionString"] = kafkaConnection;
builder.Services.AddSingleton<IAscendEventPublisher, KafkaAscendEventPublisher>();

var app = builder.Build();

await Db.InitAsync();

// Last-chance exception logger: any unhandled exception gets a full stack trace
// in the server error log and a JSON body clients can parse.
app.Use(async (ctx, next) =>
{
    try { await next(ctx); }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[unhandled] {ctx.Request.Method} {ctx.Request.Path} -> {ex}");
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync("{\"error\":\"Internal server error. See server logs.\"}");
    }
});

// Serve the React app from wwwroot (frontend-react/dist is mapped there at
// publish time — see the Content rule in Ascend.Api.csproj), so the UI and the
// API share one origin on Azure and no CORS or base-URL config is needed.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/swagger/v1/swagger.json", "Ascend API v1");
});

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// SPA fallback: unknown client-side routes resolve to index.html, but unknown
// /api paths must stay 404 instead of being swallowed by the SPA.
var webRoot = app.Environment.WebRootPath;
app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var index = webRoot is null ? null : Path.Combine(webRoot, "index.html");
    if (index is null || !File.Exists(index))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(index);
});

app.Run();

// Exposes the top-level Program type to WebApplicationFactory<Program> in tests.
public partial class Program { }

