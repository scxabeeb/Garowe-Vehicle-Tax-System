using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages;

[Authorize(Policy = "CanViewDashboard")]
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
    public int RevertedPayments { get; set; }
    public int ActiveCollectors { get; set; }
    public decimal AveragePayment { get; set; }

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

    // =========================
    // REVENUE ACCOUNT CHART
    // =========================
    public string RevenueAccountLabels { get; set; } = "[]";
    public string RevenueAccountAmounts { get; set; } = "[]";
    public decimal RevenueAccountGrandTotal { get; set; }

    public void OnGet()
    {
        // TOTAL VEHICLES
        TotalVehicles = _context.Vehicles.Count();

        // TOTAL PAYMENTS
        TotalPayments = _context.Payments
            .Where(p => p.IsPaid && !p.IsReverted)
            .Count();

        // REVERTED PAYMENTS
        RevertedPayments = _context.Payments
            .Where(p => p.IsReverted)
            .Count();

        // TODAY COLLECTION
        var todayRange = AppTime.GetUtcDayRange(AppTime.Today);
        TodayTotal = _context.Payments
            .Where(p => p.IsPaid && !p.IsReverted && p.PaidAt >= todayRange.StartUtc && p.PaidAt < todayRange.EndUtc)
            .Sum(p => (decimal?)p.Amount) ?? 0;

        // AVERAGE PAYMENT (PAID + NOT REVERTED)
        AveragePayment = _context.Payments
            .Where(p => p.IsPaid && !p.IsReverted)
            .Average(p => (decimal?)p.Amount) ?? 0;

        // ACTIVE COLLECTORS
        ActiveCollectors = _context.Payments
            .Include(p => p.Collector)
            .Include(p => p.ReceiptReference)
            .Where(p => p.IsPaid && !p.IsReverted)
            .AsEnumerable()
            .Select(p => p.Collector?.Username ?? p.ReceiptReference?.UsedBy ?? "Unassigned")
            .Distinct()
            .Count();

        // RECENT PAYMENTS (KEEP AS IS)
        RecentPayments = _context.Payments
            .Include(p => p.Vehicle)
            .Include(p => p.Collector)
            .Include(p => p.ReceiptReference)
            .Include(p => p.Checkpoint)
            .Where(p => p.IsPaid && !p.IsReverted)
            .OrderByDescending(p => p.PaidAt)
            .Take(10)
            .ToList();

        // =========================
        // DAILY CHART
        // =========================
        var startDate = AppTime.Today.AddDays(-6);
        var startDateUtc = AppTime.GetUtcDayRange(startDate).StartUtc;

        var dailyRaw = _context.Payments
            .Where(p => p.IsPaid && !p.IsReverted && p.PaidAt >= startDateUtc)
            .Select(p => new
            {
                p.PaidAt,
                p.Amount
            })
            .AsEnumerable()
            .GroupBy(p => AppTime.ToLocal(p.PaidAt).Date)
            .Select(g => new
            {
                Day = g.Key,
                Total = g.Sum(x => x.Amount)
            })
            .OrderBy(x => x.Day)
            .ToList();

        var daily = Enumerable.Range(0, 7)
            .Select(i => startDate.AddDays(i))
            .Select(day => new
            {
                Day = day,
                Total = dailyRaw.FirstOrDefault(d => d.Day == day)?.Total ?? 0
            })
            .ToList();

        DailyLabels = JsonSerializer.Serialize(daily.Select(d => d.Day.ToString("MM-dd")));
        DailyAmounts = JsonSerializer.Serialize(daily.Select(d => d.Total));

        // =========================
        // MOVEMENT (GROUPED, NO DUPLICATES)
        // =========================
        var movementData = _context.Payments
            .Where(p => p.IsPaid && !p.IsReverted)
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
            .Include(p => p.Collector)
            .Include(p => p.ReceiptReference)
            .Where(p => p.IsPaid && !p.IsReverted)
            .AsEnumerable()
            .GroupBy(p => p.Collector?.Username ?? p.ReceiptReference?.UsedBy ?? "Unassigned")
            .Select(g => new
            {
                Collector = g.Key,
                Total = g.Sum(x => x.Amount)
            })
            .ToList();

        var collectorData = collectorTotals
            .OrderByDescending(x => x.Total)
            .ToList();

        CollectorLabels = JsonSerializer.Serialize(collectorData.Select(c => c.Collector));
        CollectorAmounts = JsonSerializer.Serialize(collectorData.Select(c => c.Total));

        // =========================
        // REVENUE ACCOUNT CHART
        // =========================
        var revenueAccountData = _context.Payments
            .Where(p => p.IsPaid && !p.IsReverted)
            .Where(p => p.Movement != null && p.Movement.RevenueAccount != null)
            .GroupBy(p => new
            {
                p.Movement!.RevenueAccount!.AccountCode,
                p.Movement!.RevenueAccount!.AccountName
            })
            .Select(g => new
            {
                Account = g.Key.AccountCode + " - " + g.Key.AccountName,
                Total = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.Total)
            .Take(5)
            .ToList();

        RevenueAccountGrandTotal = revenueAccountData.Sum(x => x.Total);
        RevenueAccountLabels = JsonSerializer.Serialize(revenueAccountData.Select(r => r.Account));
        RevenueAccountAmounts = JsonSerializer.Serialize(revenueAccountData.Select(r => r.Total));
    }
}
