using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public int TotalVehicles { get; set; }
    public int TotalPayments { get; set; }
    public decimal TodayTotal { get; set; }

    public List<Payment> RecentPayments { get; set; } = new();

    public string DailyLabels { get; set; } = "[]";
    public string DailyAmounts { get; set; } = "[]";

    public int PassingCount { get; set; }
    public int StayingCount { get; set; }

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public void OnGet()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            Response.Redirect("/Account/Login");
            return;
        }

        TotalVehicles = _context.Vehicles.Count();
        TotalPayments = _context.Payments.Count();

        TodayTotal = _context.Payments
            .Where(p => p.PaidAt.Date == DateTime.Today)
            .Sum(p => (decimal?)p.Amount) ?? 0;

        RecentPayments = _context.Payments
            .Include(p => p.Vehicle)
            .OrderByDescending(p => p.PaidAt)
            .Take(10)
            .ToList();

        var daily = _context.Payments
            .Where(p => p.PaidAt >= DateTime.Today.AddDays(-6))
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

        PassingCount = _context.Payments.Count(p => p.MovementType == "Passing");
        StayingCount = _context.Payments.Count(p => p.MovementType == "Staying");
    }
}
