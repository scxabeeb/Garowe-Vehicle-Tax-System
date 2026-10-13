using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using MySqlConnector;
using VehicleTax.Web;
using VehicleTax.Web.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// ==================================
// Read Railway Environment Variables
// ==================================
builder.Configuration.AddEnvironmentVariables();
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));
// =========================
// Database
// =========================
var connectionString = ResolveConnectionString(builder.Configuration);
var serverVersion = new MySqlServerVersion(new Version(8, 0, 34));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion)
);

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// =========================
// 🔐 Authentication (COOKIE BASED)
// =========================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
// =========================

// =========================
// 🔑 Authorization (Roles + Permissions)
// =========================
builder.Services.AddAuthorization(options =>
{
    // Default "perm:*" policy — dynamically resolves any permission key
    // from HasPermissionAttribute (which sets Policy = "perm:<key>").
    options.AddPolicy("PermissionPolicy", policy =>
        policy.AddRequirements(new PermissionRequirement("")));

    // Backward-compatible named policies
    options.AddPolicy("CanViewDashboard", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("Admin") ||
            context.User.IsInRole("Auditor") ||
            context.User.HasClaim("permission", "Dashboard.View")
        )
    );
});

// Register the permission authorization handler (scoped so it gets IPermissionService).
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
// Register the custom policy provider for "perm:*" policies.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

// =========================
// Razor Pages secured by default
// =========================
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/Logout");
    options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
});

// =========================
// API Controllers
// =========================
builder.Services.AddControllers();

// =========================
// CORS (allow browser apps, e.g. Flutter web / admin frontends)
// =========================
const string CorsPolicy = "AppCors";
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// =========================
// Golis Mobile Money API
// =========================
builder.Services.Configure<VehicleTax.Web.Services.Golis.GolisApiOptions>(
    builder.Configuration.GetSection("GolisApi"));

builder.Services.Configure<GolisWebhookSettings>(
    builder.Configuration.GetSection("GolisWebhook"));

builder.Services.Configure<VehicleTax.Web.JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

builder.Services.PostConfigure<GolisWebhookSettings>(options =>
{
    var golisApiKey = builder.Configuration["GolisApi:ApiKey"];
    var golisApiPassword = builder.Configuration["GolisApi:Password"];

    // Fallback to GolisApi creds when webhook-specific creds are not set.
    if (string.IsNullOrWhiteSpace(options.ApiUsername) && IsMeaningfulCredential(golisApiKey))
    {
        options.ApiUsername = golisApiKey!.Trim();
    }

    if (string.IsNullOrWhiteSpace(options.ApiPassword) && IsMeaningfulCredential(golisApiPassword))
    {
        options.ApiPassword = golisApiPassword!.Trim();
    }
});

builder.Services.AddHttpClient<VehicleTax.Web.Services.Golis.IGolisApiService, VehicleTax.Web.Services.Golis.GolisApiService>();

// Audit Reference Number service — transaction-safe sequential number generation
builder.Services.AddScoped<VehicleTax.Web.Services.IPaymentReferenceService, VehicleTax.Web.Services.PaymentReferenceService>();

// ── RF / FMIS (Finance) services ──────────────────────────────────
builder.Services.Configure<VehicleTax.Web.Services.FmisOptions>(builder.Configuration.GetSection("Fmis"));
builder.Services.AddHttpClient("Fmis");
builder.Services.AddScoped<VehicleTax.Web.Services.IRvNumberService, VehicleTax.Web.Services.RvNumberService>();
builder.Services.AddScoped<VehicleTax.Web.Services.IFmisTransferService, VehicleTax.Web.Services.FmisTransferService>();

// ── Permission management (RBAC) ──────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<VehicleTax.Web.Services.Lang>();
builder.Services.AddScoped<VehicleTax.Web.Services.IPermissionService, VehicleTax.Web.Services.PermissionService>();

// =========================
// Session
// =========================
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// ==================================
// Forwarded Headers (for reverse proxy / Railway)
// ==================================
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

var allowRunWithoutDatabase = builder.Configuration.GetValue<bool>("Startup:AllowRunWithoutDatabase");

// =========================
// Auto migrate DB
// =========================
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}
catch (Exception ex) when (allowRunWithoutDatabase)
{
    app.Logger.LogWarning(ex, "Database migration failed. Continuing startup because Startup:AllowRunWithoutDatabase is enabled.");
}

// =========================
// Pipeline
// =========================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Answer CORS preflight (OPTIONS) before routing/authorization reject it.
app.UseCors(CorsPolicy);

app.UseSession();
app.UseAuthentication();

// 🔥 GLOBAL CACHE + HISTORY KILLER
app.Use(async (context, next) =>
{
    context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
    context.Response.Headers["Pragma"] = "no-cache";
    context.Response.Headers["Expires"] = "0";
    await next();
});

app.UseAuthorization();

// =========================
// Map API Controllers (require CORS so preflight succeeds)
// =========================
app.MapControllers().RequireCors(CorsPolicy);

// =========================
// Razor Pages
// =========================
app.MapRazorPages();

app.Run();

// Builds the connection string from Railway's MySQL environment variables
// (MYSQL_URL / MYSQLHOST / MYSQLPORT / ...) so credential rotation on
// Railway never breaks the deployed app. Falls back to the configured
// ConnectionStrings:DefaultConnection for local development.
static string ResolveConnectionString(IConfiguration config)
{
    var mysqlUrl = config["MYSQL_URL"] ?? config["MYSQLURL"] ?? config["DATABASE_URL"];
    if (!string.IsNullOrWhiteSpace(mysqlUrl) &&
        Uri.TryCreate(mysqlUrl, UriKind.Absolute, out var uri) &&
        !string.IsNullOrWhiteSpace(uri.Host))
    {
        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var database = uri.AbsolutePath.Trim('/');
        return $"Server={uri.Host};Port={uri.Port};Database={database};User={user};Password={pass};SslMode=Preferred;";
    }

    var host = config["MYSQLHOST"];
    if (!string.IsNullOrWhiteSpace(host))
    {
        var port = config["MYSQLPORT"] ?? "3306";
        var database = config["MYSQLDATABASE"] ?? config["MYSQL_DATABASE"] ?? "railway";
        var user = config["MYSQLUSER"] ?? "root";
        var pass = config["MYSQLPASSWORD"] ?? "";
        return $"Server={host};Port={port};Database={database};User={user};Password={pass};SslMode=Preferred;";
    }

    return config.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("No database connection string configured.");
}

static bool IsMeaningfulCredential(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return false;
    }

    var trimmed = value.Trim();
    return !trimmed.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
}
