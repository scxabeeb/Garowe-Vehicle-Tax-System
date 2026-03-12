using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages;

[Authorize(Roles = "Admin,Auditor")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    // =========================
    // DASHBOARD CARDS
    // =========================
    public int TotalVehicles { get; set; }
    public int TotalPayments { get; set; }
    public decimal TodayTotal { get; set; }

    // =========================
    // RECENT PAYMENTS (UNCHANGED)
    // =========================
    public List<Payment> RecentPayments { get; set; } = new();

    // =========================
    // DAILY CHART
    // =========================
    public string DailyLabels { get; set; } = "[]";
    public string DailyAmounts { get; set; } = "[]";

    // =========================
    // MOVEMENT (NO DUPLICATES)
    // =========================
    public string MovementLabels { get; set; } = "[]";
    public string MovementCounts { get; set; } = "[]";

    public List<MovementRow> MovementTable { get; set; } = new();

    public class MovementRow
    {
        public string Movement { get; set; } = "";
        public int Count { get; set; }
    }

    // =========================
    // COLLECTOR PERFORMANCE
    // =========================
    public string CollectorLabels { get; set; } = "[]";
    public string CollectorAmounts { get; set; } = "[]";

    public void OnGet()
    {
        // TOTAL VEHICLES
        TotalVehicles = _context.Vehicles.Count();

        // TOTAL PAYMENTS
        TotalPayments = _context.Payments
            .Where(p => !p.IsReverted)
            .Count();

        // TODAY COLLECTION
        var today = DateTime.UtcNow.Date;
        TodayTotal = _context.Payments
            .Where(p => !p.IsReverted && p.PaidAt.Date == today)
            .Sum(p => (decimal?)p.Amount) ?? 0;

        // RECENT PAYMENTS (KEEP AS IS)
        RecentPayments = _context.Payments
            .Include(p => p.Vehicle)
            .Include(p => p.Collector)
            .Where(p => !p.IsReverted)
            .OrderByDescending(p => p.PaidAt)
            .Take(10)
            .ToList();

        // =========================
        // DAILY CHART
        // =========================
        var startDate = DateTime.UtcNow.Date.AddDays(-6);

        var daily = _context.Payments
            .Where(p => !p.IsReverted && p.PaidAt >= startDate)
            .GroupBy(p => p.PaidAt.Date)
            .Select(g => new
            {
                Day = g.Key,
                Total = g.Sum(x => x.Amount)
            })
            .OrderBy(x => x.Day)
            .ToList();

        DailyLabels = JsonSerializer.Serialize(daily.Select(d => d.Day.ToString("MM-dd")));
        DailyAmounts = JsonSerializer.Serialize(daily.Select(d => d.Total));

        // =========================
        // MOVEMENT (GROUPED, NO DUPLICATES)
        // =========================
        var movementData = _context.Payments
            .Where(p => !p.IsReverted)
            .GroupBy(p => p.MovementType)
            .Select(g => new MovementRow
            {
                Movement = g.Key!,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        MovementTable = movementData;
        MovementLabels = JsonSerializer.Serialize(movementData.Select(m => m.Movement));
        MovementCounts = JsonSerializer.Serialize(movementData.Select(m => m.Count));

        // =========================
        // COLLECTOR PERFORMANCE
        // =========================
        var collectorTotals = _context.Payments
            .Where(p => !p.IsReverted && p.CollectorId != null)
            .GroupBy(p => p.CollectorId)
            .Select(g => new
            {
                CollectorId = g.Key!.Value,
                Total = g.Sum(x => x.Amount)
            })
            .ToList();

        var collectorData = collectorTotals
            .Join(_context.Users,
                  p => p.CollectorId,
                  u => u.Id,
                  (p, u) => new
                  {
                      Collector = u.Username,
                      Total = p.Total
                  })
            .OrderByDescending(x => x.Total)
            .ToList();

        CollectorLabels = JsonSerializer.Serialize(collectorData.Select(c => c.Collector));
        CollectorAmounts = JsonSerializer.Serialize(collectorData.Select(c => c.Total));
    }
}
