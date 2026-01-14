using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

// ==================================
// Read Railway Environment Variables
// ==================================
builder.Configuration.AddEnvironmentVariables();

// =========================
// Database
// =========================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Railway uses MySQL 8.x
var serverVersion = new MySqlServerVersion(new Version(8, 0, 34));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion)
);

// =========================
// Identity (Users + Roles)
// =========================
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
    options.SlidingExpiration = true;
});

// =========================
// Razor Pages secured by default
// =========================
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
});

// =========================
// API Controllers
// =========================
builder.Services.AddControllers();

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

// =========================
// Auto migrate database (optional but recommended)
// =========================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// =========================
// Middleware Pipeline
// =========================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Railway already handles HTTPS
// app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();

// ==========================================
// Prevent cached pages appearing after logout
// ==========================================
app.Use(async (context, next) =>
{
    context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    context.Response.Headers["Pragma"] = "no-cache";
    context.Response.Headers["Expires"] = "0";
    await next();
});

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();
