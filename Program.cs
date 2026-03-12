using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using VehicleTax.Web.Data;
<<<<<<< HEAD

// ✅ QuestPDF license (VERY IMPORTANT)
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ✅ REQUIRED FOR PDF TO WORK
QuestPDF.Settings.License = LicenseType.Community;

=======
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
// ==================================
// Read Railway Environment Variables
// ==================================
builder.Configuration.AddEnvironmentVariables();

// =========================
// Database
// =========================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var serverVersion = new MySqlServerVersion(new Version(8, 0, 34));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion)
);

// =========================
<<<<<<< HEAD
// Authentication (COOKIE)
=======
// 🔐 Authentication (COOKIE BASED)
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
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
<<<<<<< HEAD
// Authorization POLICIES
// =========================
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CollectPolicy", policy =>
        policy.RequireRole("Admin", "Collector","Viewer"));

    options.AddPolicy("ReportPolicy", policy =>
        policy.RequireRole("Admin", "Auditor", "Viewer"));

    options.AddPolicy("SetupPolicy", policy =>
        policy.RequireRole("Admin", "Viewer"));

    options.AddPolicy("AdminOnlyPolicy", policy =>
        policy.RequireRole("Admin"));
});

// =========================
// Razor Pages ROLE SECURITY
=======
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
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
// =========================
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
<<<<<<< HEAD

    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/Logout");
    options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");

    options.Conventions.AuthorizeFolder("/Payments", "CollectPolicy");
    options.Conventions.AuthorizeFolder("/Reports", "ReportPolicy");
    options.Conventions.AuthorizePage("/Index", "ReportPolicy");
    options.Conventions.AuthorizeFolder("/ReceiptReferences", "SetupPolicy");

    options.Conventions.AuthorizeFolder("/Account", "AdminOnlyPolicy");
});

// =========================
// Controllers
=======
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/Logout");
    options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
});

// =========================
// API Controllers
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
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
<<<<<<< HEAD
// SMART DATABASE INIT (Railway safe)
=======
// Auto migrate DB
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
// =========================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
<<<<<<< HEAD
    var connection = db.Database.GetDbConnection();

    connection.Open();

    using var command = connection.CreateCommand();
    command.CommandText = @"
        SELECT COUNT(*)
        FROM information_schema.tables
        WHERE table_schema = DATABASE()
        AND table_name = 'CarTypes';";

    var tableExists = Convert.ToInt32(command.ExecuteScalar()) > 0;

    if (!tableExists)
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();

    connection.Close();
=======
    db.Database.Migrate();
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
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

<<<<<<< HEAD
=======
// 🔥 GLOBAL CACHE + HISTORY KILLER
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
app.Use(async (context, next) =>
{
    context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
    context.Response.Headers["Pragma"] = "no-cache";
    context.Response.Headers["Expires"] = "0";
    await next();
});

app.UseAuthorization();

<<<<<<< HEAD
app.MapControllers();
=======
// =========================
// Map API Controllers
// =========================
app.MapControllers();

// =========================
// Razor Pages
// =========================
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
app.MapRazorPages();

app.Run();
