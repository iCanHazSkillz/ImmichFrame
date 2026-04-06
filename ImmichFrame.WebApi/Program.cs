using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.WebApi.Models;
using Microsoft.AspNetCore.Authentication;
using System.Reflection;
using ImmichFrame.Core.Logic;
using ImmichFrame.Core.Logic.AccountSelection;
using ImmichFrame.WebApi.Helpers;
using ImmichFrame.WebApi.Helpers.Config;
using ImmichFrame.WebApi.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    var root = Directory.GetCurrentDirectory();
    var dotenv = Path.Combine(root, "..", "docker", ".env");

    dotenv = Path.GetFullPath(dotenv);
    DotEnv.Load(dotenv);
}

if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("http://+:8080");
}

//log the version number
var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
Console.WriteLine($@"
 _                     _      _    ______                        
(_)                   (_)    | |   |  ___|                       
 _ _ __ ___  _ __ ___  _  ___| |__ | |_ _ __ __ _ _ __ ___   ___ 
| | '_ ` _ \| '_ ` _ \| |/ __| '_ \|  _| '__/ _` | '_ ` _ \ / _ \
| | | | | | | | | | | | | (__| | | | | | | | (_| | | | | | |  __/
|_|_| |_| |_|_| |_| |_|_|\___|_| |_\_| |_|  \__,_|_| |_| |_|\___| Version {version}");
Console.WriteLine();

// Add services to the container.
builder.Services.AddLogging(builder =>
{
    LogLevel level = LogLevel.Information;
    var logLevel = Environment.GetEnvironmentVariable("LOG_LEVEL");
    if (!string.IsNullOrWhiteSpace(logLevel))
    {
        Enum.TryParse(logLevel, true, out level);
    }

    Console.WriteLine($"LogLevel: {level}");
    builder.SetMinimumLevel(level);
    builder.AddSimpleConsole(options =>
    {
        // Customizing the log output format
        options.TimestampFormat = "yy-MM-dd HH:mm:ss "; // Custom timestamp format
        options.SingleLine = true;
    });

    // Disable SpaProxy info logs
    builder.AddFilter("Microsoft.AspNetCore.SpaProxy", LogLevel.Warning);
    // Disable AspNetCore info logs
    builder.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
});


// Setup Config
var configPath = Environment.GetEnvironmentVariable("IMMICHFRAME_CONFIG_PATH") ??
        Directory.EnumerateDirectories(AppDomain.CurrentDomain.BaseDirectory, "*", SearchOption.TopDirectoryOnly)
        .FirstOrDefault(d => string.Equals(Path.GetFileName(d), "Config", StringComparison.OrdinalIgnoreCase))
        ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
var appDataPath = Environment.GetEnvironmentVariable("IMMICHFRAME_APP_DATA_PATH") ??
    Path.Combine(builder.Environment.ContentRootPath, "App_Data");
builder.Services.AddTransient<ConfigLoader>();
builder.Services.AddSingleton(srv =>
    new BootstrapServerSettingsHolder(ServerSettingsFactory.Clone(srv.GetRequiredService<ConfigLoader>().LoadConfig(configPath))));
builder.Services.AddSingleton(new AdminManagedSettingsStoreOptions
{
    StorePath = Path.Combine(appDataPath, "admin-settings.json")
});
builder.Services.AddSingleton(new CustomCssStoreOptions
{
    StorePath = Path.Combine(appDataPath, "custom.css"),
    FallbackPath = Path.Combine(builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot"), "static", "custom.css")
});
builder.Services.AddSingleton<IAdminManagedSettingsStore, AdminManagedSettingsStore>();
builder.Services.AddSingleton<ICustomCssStore, CustomCssStore>();
builder.Services.AddSingleton<ICustomCssValidator, CustomCssValidator>();
builder.Services.AddSingleton<EffectiveSettingsProvider>();
builder.Services.AddSingleton<IWritableEffectiveSettingsProvider>(srv => srv.GetRequiredService<EffectiveSettingsProvider>());
builder.Services.AddSingleton<ISettingsSnapshotProvider>(srv => srv.GetRequiredService<EffectiveSettingsProvider>());
builder.Services.AddSingleton<DynamicGeneralSettings>();
builder.Services.AddSingleton<DynamicServerSettings>();
builder.Services.AddSingleton<IGeneralSettings>(srv => srv.GetRequiredService<DynamicGeneralSettings>());
builder.Services.AddSingleton<IServerSettings>(srv => srv.GetRequiredService<DynamicServerSettings>());

// Register services
builder.Services.AddSingleton<IWeatherService, OpenWeatherMapService>();
builder.Services.AddSingleton<ICalendarService, IcalCalendarService>();
builder.Services.AddTransient<IAssetAccountTracker, BloomFilterAssetAccountTracker>();
builder.Services.AddTransient<IAccountSelectionStrategy, TotalAccountImagesSelectionStrategy>();
builder.Services.AddSingleton<Func<IAccountSelectionStrategy>>(srv =>
    () => srv.GetRequiredService<IAccountSelectionStrategy>());
builder.Services.AddHttpClient(); // Ensures IHttpClientFactory is available

builder.Services.AddTransient<Func<IAccountSettings, IAccountImmichFrameLogic>>(srv =>
    account => ActivatorUtilities.CreateInstance<PooledImmichFrameLogic>(srv, account));

builder.Services.AddSingleton<IImmichFrameLogic, MultiImmichFrameLogicDelegate>();
builder.Services.AddSingleton(new FrameSessionRegistryOptions
{
    DisplayNameStorePath = Path.Combine(appDataPath, "frame-session-display-names.json")
});
builder.Services.AddSingleton<IFrameSessionRegistry>(srv =>
    new FrameSessionRegistry(
        srv.GetRequiredService<FrameSessionRegistryOptions>(),
        null,
        srv.GetService<ILogger<FrameSessionRegistry>>()));
builder.Services.AddSingleton<IAdminBasicAuthService, AdminBasicAuthService>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("AdminLogin", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));
});

builder.Services.AddAuthorization();

builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, ImmichFrameAuthenticationHandler>(AuthSchemes.Frame, options => { })
    .AddCookie(AuthSchemes.Admin, options =>
    {
        options.Cookie.Name = "ImmichFrame.Admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = context =>
            {
                var adminBasicAuthService = context.HttpContext.RequestServices.GetRequiredService<IAdminBasicAuthService>();
                var username = context.Principal?.FindFirstValue(ClaimTypes.Name);
                var credentialVersion = context.Principal?.FindFirst("immichframe_admin_credential_version")?.Value;
                var currentVersion = string.IsNullOrWhiteSpace(username)
                    ? null
                    : adminBasicAuthService.GetCredentialVersion(username);

                if (string.IsNullOrWhiteSpace(username) ||
                    string.IsNullOrWhiteSpace(credentialVersion) ||
                    string.IsNullOrWhiteSpace(currentVersion) ||
                    !string.Equals(credentialVersion, currentVersion, StringComparison.Ordinal))
                {
                    context.RejectPrincipal();
                    return context.HttpContext.SignOutAsync(AuthSchemes.Admin);
                }

                return Task.CompletedTask;
            },
            OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
        };
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    if (!context.Request.Path.Equals("/static/custom.css", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    var customCssStore = context.RequestServices.GetRequiredService<ICustomCssStore>();
    context.Response.ContentType = "text/css; charset=utf-8";
    context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
    context.Response.Headers.Pragma = "no-cache";
    context.Response.Headers.Expires = "0";
    await context.Response.WriteAsync(customCssStore.LoadStylesheetContent());
});
app.UseStaticFiles();
if (app.Environment.IsProduction())
{
    app.UseDefaultFiles();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

app.Run();

// Make Program public for WebApplicationFactory
public partial class Program { }
