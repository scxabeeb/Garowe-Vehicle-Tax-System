using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Reports;

/// <summary>
/// Every sign-in (and failed attempt), sign-out, and record created, updated
/// or deleted, with who did it, when and from where.
/// </summary>
public class ActivityLogModel : PageModel
{
    private readonly AppDbContext _context;

    public ActivityLogModel(AppDbContext context) => _context = context;

    [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
    [BindProperty(SupportsGet = true)] public string? Username { get; set; }
    [BindProperty(SupportsGet = true)] public string? Action { get; set; }
    [BindProperty(SupportsGet = true)] public string? Entity { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;

    public List<string> Usernames { get; set; } = new();
    public List<string> Entities { get; set; } = new();

    public List<ActivityLog> Rows { get; set; } = new();
    public Pager Pager { get; set; } = new(0, 1, 20);

    // Summary
    public Dictionary<string, int> ByAction { get; set; } = new();
    public List<UserActivity> ByUser { get; set; } = new();
    public List<EntityActivity> ByEntity { get; set; } = new();
    public List<ActivityLog> RecentLogins { get; set; } = new();
    public int Total => ByAction.Values.Sum();
    public int Count(string action) => ByAction.TryGetValue(action, out var n) ? n : 0;

    public record UserActivity(string Username, int Logins, int Failed, int Created, int Updated, int Deleted, DateTime Last)
    {
        public int Total => Logins + Failed + Created + Updated + Deleted;
    }

    public record EntityActivity(string Entity, int Created, int Updated, int Deleted)
    {
        public int Total => Created + Updated + Deleted;
    }

    public async Task OnGetAsync()
    {
        // Default to the last 7 days so the page opens fast.
        if (!FromDate.HasValue && !ToDate.HasValue && !Request.Query.ContainsKey("FromDate"))
        {
            ToDate = AppTime.Now.Date;
            FromDate = ToDate.Value.AddDays(-6);
        }

        Usernames = await _context.ActivityLogs.AsNoTracking()
            .Select(a => a.Username).Distinct().OrderBy(u => u).ToListAsync();
        Entities = await _context.ActivityLogs.AsNoTracking()
            .Where(a => a.Entity != "Account")
            .Select(a => a.Entity).Distinct().OrderBy(e => e).ToListAsync();

        var query = Filtered();

        ByAction = await query.GroupBy(a => a.Action)
            .Select(g => new { g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.N);

        var perUser = await query
            .GroupBy(a => new { a.Username, a.Action })
            .Select(g => new { g.Key.Username, g.Key.Action, N = g.Count(), Last = g.Max(a => a.At) })
            .ToListAsync();
        ByUser = perUser.GroupBy(x => x.Username)
            .Select(g => new UserActivity(g.Key,
                g.Where(x => x.Action == ActivityActions.Login).Sum(x => x.N),
                g.Where(x => x.Action == ActivityActions.LoginFailed).Sum(x => x.N),
                g.Where(x => x.Action == ActivityActions.Created).Sum(x => x.N),
                g.Where(x => x.Action == ActivityActions.Updated).Sum(x => x.N),
                g.Where(x => x.Action == ActivityActions.Deleted).Sum(x => x.N),
                g.Max(x => x.Last)))
            .OrderByDescending(u => u.Total)
            .ToList();

        var perEntity = await query
            .Where(a => a.Action == ActivityActions.Created || a.Action == ActivityActions.Updated || a.Action == ActivityActions.Deleted)
            .GroupBy(a => new { a.Entity, a.Action })
            .Select(g => new { g.Key.Entity, g.Key.Action, N = g.Count() })
            .ToListAsync();
        ByEntity = perEntity.GroupBy(x => x.Entity)
            .Select(g => new EntityActivity(g.Key,
                g.Where(x => x.Action == ActivityActions.Created).Sum(x => x.N),
                g.Where(x => x.Action == ActivityActions.Updated).Sum(x => x.N),
                g.Where(x => x.Action == ActivityActions.Deleted).Sum(x => x.N)))
            .OrderByDescending(e => e.Total)
            .ToList();

        RecentLogins = await query
            .Where(a => a.Action == ActivityActions.Login || a.Action == ActivityActions.LoginFailed || a.Action == ActivityActions.Logout)
            .OrderByDescending(a => a.At).ThenByDescending(a => a.Id)
            .Take(10)
            .AsNoTracking()
            .ToListAsync();

        Pager = new Pager(Total, CurrentPage, PageSize);
        CurrentPage = Pager.Page;
        Rows = await Pager.Apply(query.OrderByDescending(a => a.At).ThenByDescending(a => a.Id))
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IActionResult> OnGetExportCsvAsync()
    {
        var rows = await Filtered()
            .OrderByDescending(a => a.At).ThenByDescending(a => a.Id)
            .AsNoTracking()
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("System ID,Date,User,Action,Record,Record ID,Details,IP Address,Page");
        foreach (var a in rows)
        {
            sb.AppendLine(string.Join(",",
                a.Id,
                Csv(AppTime.ToLocal(a.At).ToString("yyyy-MM-dd HH:mm:ss")),
                Csv(a.Username), Csv(a.Action), Csv(a.Entity), Csv(a.EntityId),
                Csv(a.Details), Csv(a.IpAddress), Csv(a.Path)));
        }
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(),
            "text/csv", $"ActivityLog_{AppTime.Now:yyyyMMdd_HHmm}.csv");
    }

    private IQueryable<ActivityLog> Filtered()
    {
        var q = _context.ActivityLogs.AsQueryable();

        // Dates are picked in local time; the log is stored in UTC.
        if (FromDate.HasValue)
        {
            var from = AppTime.ToUtc(FromDate.Value.Date);
            q = q.Where(a => a.At >= from);
        }
        if (ToDate.HasValue)
        {
            var to = AppTime.ToUtc(ToDate.Value.Date.AddDays(1));
            q = q.Where(a => a.At < to);
        }
        if (!string.IsNullOrWhiteSpace(Username)) q = q.Where(a => a.Username == Username);
        if (!string.IsNullOrWhiteSpace(Action))
        {
            q = Action == "SignIns"
                ? q.Where(a => a.Action == ActivityActions.Login || a.Action == ActivityActions.LoginFailed || a.Action == ActivityActions.Logout)
                : q.Where(a => a.Action == Action);
        }
        if (!string.IsNullOrWhiteSpace(Entity)) q = q.Where(a => a.Entity == Entity);
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim();
            q = q.Where(a => a.Username.Contains(s) || (a.EntityId != null && a.EntityId == s) ||
                             (a.Details != null && a.Details.Contains(s)) || (a.IpAddress != null && a.IpAddress.Contains(s)));
        }
        return q;
    }

    private static string Csv(string? v) =>
        string.IsNullOrEmpty(v) ? "" :
        v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
}
