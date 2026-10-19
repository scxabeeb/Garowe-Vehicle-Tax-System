using System.IdentityModel.Tokens.Jwt;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Services;

/// <summary>Who / where of the current request, for activity entries.</summary>
public static class ActivityContext
{
    /// <summary>App request whose user could not be identified.</summary>
    public const string UnknownApp = "Unknown (mobile app)";

    /// <summary>Where an action came from, from its request path.</summary>
    public static string SourceOf(string? path) =>
        string.IsNullOrEmpty(path) ? "System" :
        path.StartsWith("/api/golis", StringComparison.OrdinalIgnoreCase) ? "Golis" :
        path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ? "Mobile app" : "Web";

    public static (string User, string? Ip, string? Path) Of(HttpContext? http)
    {
        if (http == null) return ("system", null, null);
        var name = http.User.Identity?.IsAuthenticated == true ? http.User.Identity!.Name : AppUser(http);
        var path = http.Request.Path.Value ?? "";
        var user = name
            ?? (path.StartsWith("/api/golis", StringComparison.OrdinalIgnoreCase) ? "Golis" :
                path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ? UnknownApp : "system");
        var ip = http.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                 ?? http.Connection.RemoteIpAddress?.ToString();
        return (user, ip, path.Length > 200 ? path[..200] : path);
    }

    /// <summary>
    /// The mobile app's user, from its signed bearer token. The API does not
    /// sign requests in, so the token is validated here only to name who
    /// made the change.
    /// </summary>
    private const string SessionKey = "activity.appSession";

    /// <summary>Session number in the app's token (0 for tokens from before it existed).</summary>
    public static int AppSessionVersion(HttpContext http) =>
        AppUser(http) != null && http.Items.TryGetValue(SessionKey, out var v) && v is int i ? i : 0;

    public static string? AppUser(HttpContext http)
    {
        const string cacheKey = "activity.appUser";
        if (http.Items.TryGetValue(cacheKey, out var cached)) return cached as string;

        string? name = null;
        var header = http.Request.Headers.Authorization.ToString();
        var jwt = http.RequestServices.GetService<IOptions<JwtSettings>>()?.Value;
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(jwt?.Key))
        {
            try
            {
                var principal = new JwtSecurityTokenHandler().ValidateToken(header[7..].Trim(), new TokenValidationParameters
                {
                    ValidateIssuer = !string.IsNullOrEmpty(jwt.Issuer),
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = !string.IsNullOrEmpty(jwt.Audience),
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    // The app keeps people signed in longer than the token
                    // lifetime; an expired but genuinely signed token still
                    // says who it is. Used only to name the user in the log.
                    ValidateLifetime = false,
                }, out _);
                name = principal.FindFirst(ClaimTypes.Name)?.Value ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                http.Items[SessionKey] = SessionClaims.Of(principal);
            }
            catch (Exception)
            {
                // No valid signature: the change is logged as UnknownApp.
            }
        }
        http.Items[cacheKey] = name;
        return name;
    }
}

/// <summary>Writes sign-in events to the activity log.</summary>
public interface IActivityLogger
{
    Task LogAsync(string action, string? username, string entity = "Account", string? entityId = null, string? details = null);
}

public sealed class ActivityLogger : IActivityLogger
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<ActivityLogger> _logger;

    public ActivityLogger(AppDbContext context, IHttpContextAccessor http, ILogger<ActivityLogger> logger)
    {
        _context = context;
        _http = http;
        _logger = logger;
    }

    public async Task LogAsync(string action, string? username, string entity = "Account", string? entityId = null, string? details = null)
    {
        try
        {
            var (user, ip, path) = ActivityContext.Of(_http.HttpContext);
            _context.ActivityLogs.Add(new ActivityLog
            {
                Username = Trim(string.IsNullOrWhiteSpace(username) ? user : username, 100),
                Action = action,
                Entity = entity,
                EntityId = entityId,
                Details = Trim(details, 2000),
                IpAddress = ip,
                Path = path,
            });
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Logging must never break signing in.
            _logger.LogWarning(ex, "Could not write activity log entry");
        }
    }

    private static string Trim(string? s, int max) => s == null ? "" : s.Length > max ? s[..max] : s;
}

/// <summary>
/// Records every insert, update and delete made through EF Core in the
/// activity log, after the save succeeds. Password values are never stored.
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<AuditSaveChangesInterceptor> _logger;
    private readonly ConditionalWeakTable<DbContext, List<Pending>> _pending = new();

    /// <summary>Tables not audited (the log itself and existing audit trails).</summary>
    private static readonly HashSet<string> Skip = new(StringComparer.Ordinal)
    {
        nameof(ActivityLog), nameof(PermissionAuditLog), nameof(RvAuditLog),
        "DataProtectionKey", // sign-in key storage, not user activity
    };

    private sealed class Pending
    {
        public required EntityEntry Entry { get; init; }
        public required string Action { get; init; }
        public string? Details { get; init; }
        public string? KeyBeforeSave { get; init; }
    }

    public AuditSaveChangesInterceptor(IHttpContextAccessor http, ILogger<AuditSaveChangesInterceptor> logger)
    {
        _http = http;
        _logger = logger;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData e, InterceptionResult<int> result)
    {
        Collect(e.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e,
        InterceptionResult<int> result, CancellationToken ct = default)
    {
        Collect(e.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData e, int result)
    {
        if (Stage(e.Context))
        {
            try { e.Context!.SaveChanges(); }
            catch (Exception ex) { Discard(e.Context!, ex); }
        }
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData e, int result, CancellationToken ct = default)
    {
        if (Stage(e.Context))
        {
            try { await e.Context!.SaveChangesAsync(ct); }
            catch (Exception ex) { Discard(e.Context!, ex); }
        }
        return result;
    }

    /// <summary>
    /// The real save already succeeded; a failed log write must not turn it
    /// into an error (e.g. a Golis payment callback), so drop the log rows.
    /// </summary>
    private void Discard(DbContext context, Exception ex)
    {
        foreach (var entry in context.ChangeTracker.Entries<ActivityLog>().Where(x => x.State == EntityState.Added).ToList())
            entry.State = EntityState.Detached;
        _logger.LogWarning(ex, "Could not write activity log entries");
    }

    public override void SaveChangesFailed(DbContextErrorEventData e)
    {
        if (e.Context != null) _pending.Remove(e.Context);
    }

    private void Collect(DbContext? context)
    {
        if (context == null) return;
        var list = new List<Pending>();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (Skip.Contains(entry.Metadata.ClrType.Name)) continue;
            switch (entry.State)
            {
                case EntityState.Added:
                    list.Add(new Pending { Entry = entry, Action = ActivityActions.Created, Details = Snapshot(entry, current: true) });
                    break;
                case EntityState.Modified:
                    var changes = Changes(entry);
                    if (changes.Length > 0)
                        list.Add(new Pending { Entry = entry, Action = ActivityActions.Updated, Details = changes, KeyBeforeSave = Key(entry) });
                    break;
                case EntityState.Deleted:
                    list.Add(new Pending { Entry = entry, Action = ActivityActions.Deleted, Details = Snapshot(entry, current: false), KeyBeforeSave = Key(entry) });
                    break;
            }
        }
        if (list.Count > 0) _pending.AddOrUpdate(context, list);
        else _pending.Remove(context);
    }

    /// <summary>Adds the log rows for the save that just finished; true when there are any to save.</summary>
    private bool Stage(DbContext? context)
    {
        if (context == null || !_pending.TryGetValue(context, out var list)) return false;
        _pending.Remove(context);

        var (user, ip, path) = ActivityContext.Of(_http.HttpContext);
        var logs = context.Set<ActivityLog>();
        var signedIn = _http.HttpContext?.User.Identity?.IsAuthenticated == true;
        foreach (var p in list)
        {
            // While signing in (no user yet) a change to a User row, such as
            // upgrading a legacy password, is made on that user's behalf.
            var by = !signedIn && p.Entry.Entity is User u && !string.IsNullOrEmpty(u.Username) ? u.Username : user;
            // A payment sent without a usable sign-in names its collector.
            if ((by == ActivityContext.UnknownApp || by == "system") && p.Entry.Entity is Payment pay && pay.CollectorId is int cid)
                by = context.Set<User>().Find(cid)?.Username ?? by;
            logs.Add(new ActivityLog
            {
                Username = by.Length > 100 ? by[..100] : by,
                Action = p.Action,
                Entity = p.Entry.Metadata.ClrType.Name,
                // New records only get their ID once saved.
                EntityId = p.KeyBeforeSave ?? Key(p.Entry),
                Details = Cap(p.Details),
                IpAddress = ip,
                Path = path,
            });
        }
        // Only ActivityLog rows are pending now, and Collect skips them.
        return true;
    }

    private static string? Key(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key == null) return null;
        return string.Join("/", key.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? ""));
    }

    private static bool IsSecret(string name) =>
        name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Token", StringComparison.OrdinalIgnoreCase);

    private static string Changes(EntityEntry entry) =>
        string.Join("; ", entry.Properties
            .Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
            .Select(p => IsSecret(p.Metadata.Name)
                ? $"{p.Metadata.Name}: (changed)"
                : $"{p.Metadata.Name}: {Show(p.OriginalValue)} → {Show(p.CurrentValue)}"));

    private static string Snapshot(EntityEntry entry, bool current) =>
        string.Join("; ", entry.Properties
            .Where(p => !p.Metadata.IsPrimaryKey())
            .Select(p => (p.Metadata.Name, Value: current ? p.CurrentValue : p.OriginalValue))
            .Where(x => x.Value != null && x.Value.ToString() != "")
            .Take(12)
            .Select(x => IsSecret(x.Name) ? $"{x.Name}: (set)" : $"{x.Name}: {Show(x.Value)}"));

    private static string Show(object? v) => v switch
    {
        null => "∅",
        DateTime d => d.ToString("yyyy-MM-dd HH:mm"),
        decimal m => m.ToString("0.##"),
        _ => v.ToString() is { Length: > 80 } s ? s[..80] + "…" : v.ToString() ?? "",
    };

    private static string? Cap(string? s) => s == null ? null : s.Length > 2000 ? s[..1999] + "…" : s;
}
