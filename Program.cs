using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using MySqlConnector;
using VehicleTax.Web;
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
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
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
    options.AddPolicy("CanViewDashboard", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("Admin") ||
            context.User.IsInRole("Auditor") ||
            context.User.HasClaim("permission", "Dashboard.View")
        )
    );
});

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
// Map API Controllers
// =========================
app.MapControllers();

// =========================
// Razor Pages
// =========================
app.MapRazorPages();

app.Run();

static bool IsMeaningfulCredential(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return false;
    }

    var trimmed = value.Trim();
    return !trimmed.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
}
