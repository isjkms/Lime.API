using System.Text;
using Lime.Api.Data;
using Lime.Api.Features.Auth;
using Lime.Api.Features.Auth.Models;
using Lime.Api.Features.Auth.Services;
using Lime.Api.Features.Catalog;
using Lime.Api.Features.Notifications;
using Lime.Api.Features.Points;
using Lime.Api.Features.Reviews;
using Lime.Api.Features.Social;
using Lime.Api.Features.Spotify;
using Lime.Api.Features.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Configure database options
var dbOptions = builder.Configuration
    .GetSection(DatabaseOptions.SectionName)
    .Get<DatabaseOptions>() ?? new DatabaseOptions();

var dataSourceBuilder = new NpgsqlDataSourceBuilder(dbOptions.BuildConnectionString());
dataSourceBuilder.EnableDynamicJson();
var dataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(dataSource);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(dataSource));

// Auth options
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();

// OAuth providers
builder.Services.AddHttpClient<GoogleOAuthProvider>();
builder.Services.AddHttpClient<KakaoOAuthProvider>();
builder.Services.AddHttpClient<NaverOAuthProvider>();
builder.Services.AddScoped<IOAuthProvider>(sp => sp.GetRequiredService<GoogleOAuthProvider>());
builder.Services.AddScoped<IOAuthProvider>(sp => sp.GetRequiredService<KakaoOAuthProvider>());
builder.Services.AddScoped<IOAuthProvider>(sp => sp.GetRequiredService<NaverOAuthProvider>());
builder.Services.AddScoped<OAuthProviderRegistry>();
builder.Services.AddScoped<IUserLinker, UserLinker>();
builder.Services.AddScoped<ISessionService, SessionService>();

// Spotify integration
builder.Services.Configure<SpotifyOptions>(builder.Configuration.GetSection(SpotifyOptions.SectionName));
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ISpotifyTokenProvider, SpotifyTokenProvider>();
builder.Services.AddHttpClient<SpotifyClient>();
builder.Services.AddScoped<ISpotifyUserTokenService, SpotifyUserTokenService>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<IPointsService, PointsService>();
var redisOptions = builder.Configuration
    .GetSection(RedisOptions.SectionName)
    .Get<RedisOptions>() ?? new RedisOptions();
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisOptions.ToConfigurationOptions()));
builder.Services.AddSingleton<NotificationStream>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<INotificationService>(sp => sp.GetRequiredService<NotificationService>());

// JWT auth — token read from cookie
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = authOptions.Jwt.Issuer,
            ValidAudience = authOptions.Jwt.Audience,
            IssuerSigningKey = string.IsNullOrEmpty(authOptions.Jwt.SigningKey)
                ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('0', 32)))
                : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.Jwt.SigningKey)),
        };
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (ctx.Request.Cookies.TryGetValue(authOptions.Cookie.AccessName, out var token))
                    ctx.Token = token;
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

// CORS for Web
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
    {
        if (!string.IsNullOrWhiteSpace(authOptions.WebBaseUrl))
            p.WithOrigins(authOptions.WebBaseUrl.TrimEnd('/'));
        p.AllowCredentials().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/db", async (AppDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    return Results.Ok(new { ok = canConnect });
});

app.MapAuthEndpoints();
app.MapSpotifyEndpoints();
app.MapSpotifyConnectEndpoints();
app.MapReviewEndpoints();
app.MapCatalogEndpoints();
app.MapUserEndpoints();
app.MapSocialEndpoints();
app.MapPointsEndpoints();
app.MapNotificationEndpoints();

app.Run();
