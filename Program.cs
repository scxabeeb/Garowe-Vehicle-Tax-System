using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using VehicleTax.Web.Data;

var builder = WebApplication.CreateBuilder(args);

// ==================================
// Load Environment Variables
// ==================================
builder.Configuration.AddEnvironmentVariables();

// ==================================
// DATABASE CONFIGURATION
// ==================================

string? connectionString;

// Try Railway environment variables first
var host = Environment.GetEnvironmentVariable("mysql.railway.internal");
var port = Environment.GetEnvironmentVariable("3306");
var database = Environment.GetEnvironmentVariable("railway");
var user = Environment.GetEnvironmentVariable("root");
var password = Environment.GetEnvironmentVariable("nxqImsdGufzdXNBuqjlbTwKRBKqCyoQc");

if (!string.IsNullOrEmpty(host) &&
    !string.IsNullOrEmpty(database) &&
    !string.IsNullOrEmpty(user))
{
    connectionString =
        $"server={host};" +
        $"port={port};" +
        $"database={database};" +
        $"user={user};" +
        $"password={password};" +
        $"SslMode=Preferred;";
}
else
{
    // Fallback for local development
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
}

if (string.IsNullOrEmpty(connectionString))
{
    throw new Exception("Database connection string is not configured properly.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString),
        mysqlOptions =>
        {
            mysqlOptions.EnableRetryOnFailure();
        }
    )
);

// ==================================
// AUTHENTICATION (Cookie Based)
// ==================================

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// ==================================
// AUTHORIZATION
// ==================================

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

// ==================================
// Razor Pages
// ==================================

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/Logout");
    options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
});

// ==================================
// API Controllers
// ==================================

builder.Services.AddControllers();

// ==================================
// Session
// ==================================

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// ==================================
// AUTO MIGRATE DATABASE
// ==================================

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// ==================================
// PIPELINE
// ==================================

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

// Disable caching globally
app.Use(async (context, next) =>
{
    context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
    context.Response.Headers["Pragma"] = "no-cache";
    context.Response.Headers["Expires"] = "0";
    await next();
});

app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

app.Run();
