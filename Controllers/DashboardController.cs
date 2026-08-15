using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using VehicleTax.Web;
using VehicleTax.Web.Data;

namespace VehicleTax.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]   // Flutter access
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GetDashboard([FromQuery] int? collectorId = null)
        {
            var todayRange = AppTime.GetUtcDayRange(AppTime.Today);

            // 🔴 Only valid (not reverted) payments
            var validPayments = _context.Payments
                .Include(p => p.Vehicle)
                .Include(p => p.Collector)
                .Include(p => p.ReceiptReference)
                .Where(p => p.IsPaid && !p.IsReverted);

            // 🔵 Filter by collector if provided
            if (collectorId.HasValue)
            {
                validPayments = validPayments
                    .Where(p => p.CollectorId == collectorId.Value);
            }

            var totalVehicles = _context.Vehicles.Count();

            var totalPayments = validPayments.Count();

            var todayTotal = validPayments
                .Where(p => p.PaidAt >= todayRange.StartUtc && p.PaidAt < todayRange.EndUtc)
                .Sum(p => (decimal?)p.Amount) ?? 0;

            // 🔥 IMPORTANT:
            // Match the exact keys Flutter is using:
            // plateNumber, movementType, amount, paidAt, isReverted
            var todaysPayments = validPayments
                .Where(p => p.PaidAt >= todayRange.StartUtc && p.PaidAt < todayRange.EndUtc)
                .OrderByDescending(p => p.PaidAt)
                .Take(50)
                .AsEnumerable()
                .Select(p => new
                {
                    plateNumber = p.Vehicle != null ? p.Vehicle.PlateNumber : "Unknown",
                    movementType = p.MovementType,
                    amount = p.Amount,
                    paidAt = AppTime.ToLocal(p.PaidAt),
                    isReverted = p.IsReverted,
                    collector = p.Collector != null
                        ? p.Collector.Username
                        : (p.ReceiptReference != null && !string.IsNullOrWhiteSpace(p.ReceiptReference.UsedBy)
                            ? p.ReceiptReference.UsedBy
                            : "Unassigned")
                })
                .ToList();

            return Ok(new
            {
                collectorId,
                totalVehicles,
                totalPayments,
                todayTotal,
                todaysPayments
            });
        }

        [HttpGet("top-checkpoints")]
        public IActionResult GetTopCheckpoints(
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int limit = 5)
        {
            if (limit < 1)
            {
                limit = 5;
            }

            if (limit > 20)
            {
                limit = 20;
            }

            var query = _context.Payments
                .Include(p => p.Checkpoint)
                .Include(p => p.Collector)
                    .ThenInclude(c => c!.Checkpoint)
                .Where(p => p.IsPaid && !p.IsReverted)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(p => p.PaidAt >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                query = query.Where(p => p.PaidAt < toDate.Value.Date.AddDays(1));
            }

            // Use the snapshot Payment.Checkpoint first; if null, fall back
            // to the collector's current checkpoint so historical payments
            // without a snapshot are still attributed.
            var items = query
                .AsEnumerable()
                .GroupBy(p =>
                {
                    if (p.Checkpoint != null) return p.Checkpoint.Name;
                    if (p.Collector?.Checkpoint != null) return p.Collector.Checkpoint.Name;
                    return "Unassigned";
                })
                .Select(g => new
                {
                    checkpoint = g.Key,
                    totalPayments = g.Count(),
                    totalAmount = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.totalAmount)
                .Take(limit)
                .ToList();

            return Ok(new
            {
                status = "success",
                fromDate,
                toDate,
                limit,
                items
            });
        }

        // =======================
        // REVENUE ACCOUNT SUMMARY
        // =======================
        // Returns the top revenue accounts by total collection amount for
        // the given date range.  Uses the snapshot Payment.Checkpoint so
        // reassignment does not affect historical attribution.
        [HttpGet("revenue-account-summary")]
        public IActionResult GetRevenueAccountSummary(
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int limit = 5)
        {
            if (limit < 1)
            {
                limit = 5;
            }

            if (limit > 20)
            {
                limit = 20;
            }

            var query = _context.Payments
                .Include(p => p.Movement)
                    .ThenInclude(m => m.RevenueAccount)
                .Where(p => p.IsPaid && !p.IsReverted)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(p => p.PaidAt >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                query = query.Where(p => p.PaidAt < toDate.Value.Date.AddDays(1));
            }

            var items = query
                .Where(p => p.Movement != null && p.Movement.RevenueAccount != null)
                .AsEnumerable()
                .GroupBy(p => new
                {
                    p.Movement!.RevenueAccount!.Id,
                    label = p.Movement!.RevenueAccount!.AccountCode + " - " + p.Movement!.RevenueAccount!.AccountName
                })
                .Select(g => new
                {
                    revenueAccountId = g.Key.Id,
                    revenueAccount = g.Key.label,
                    totalPayments = g.Count(),
                    totalAmount = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.totalAmount)
                .Take(limit)
                .ToList();

            var grandTotal = items.Sum(x => x.totalAmount);

            return Ok(new
            {
                status = "success",
                fromDate,
                toDate,
                limit,
                grandTotal,
                items
            });
        }
    }
}