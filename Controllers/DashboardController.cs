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
                    // IDs for the payment detail sheet, so the app doesn't
                    // need a per-payment receipt request.
                    paymentId = p.Id,
                    invoiceId = p.InvoiceNumber,
                    invoiceNumber = p.InvoiceNumber,
                    shortCode = p.ShortCode,
                    golisBillNo = p.TransactionId,
                    // The web receipt uses the invoice number as the receipt number.
                    receiptNumber = p.IsReverted ? null : p.InvoiceNumber,
                    referenceNo = p.ReferenceNo,
                    paymentMethod = p.PaymentMethod,
                    paidBy = p.PaidBy,
                    collectorId = p.CollectorId,
                    referenceNumber = p.ReceiptReference != null ? p.ReceiptReference.ReferenceNumber : null,
                    ownerName = p.Vehicle != null ? p.Vehicle.OwnerName : null,
                    isPaid = p.IsPaid,
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

        // =========================================
        // 📊 DASHBOARD SUMMARY CARDS
        // =========================================
        // Aggregated figures for the mobile dashboard cards. With collectorId
        // the figures cover that collector only; without it, all collectors.
        [HttpGet("summary")]
        public IActionResult GetSummary([FromQuery] int? collectorId = null)
        {
            var today = AppTime.Today;
            var todayRange = AppTime.GetUtcDayRange(today);
            var yesterdayRange = AppTime.GetUtcDayRange(today.AddDays(-1));
            var weekStartUtc = AppTime.GetUtcDayRange(today.AddDays(-6)).StartUtc;
            var monthStartUtc = AppTime.GetUtcDayRange(new DateTime(today.Year, today.Month, 1)).StartUtc;

            var payments = _context.Payments.Where(p => !p.IsReverted);
            if (collectorId.HasValue)
                payments = payments.Where(p => p.CollectorId == collectorId.Value);
            // A collector's figures cover all their invoices, including those
            // their customers paid through Golis.
            var paid = payments.Where(p => p.IsPaid);

            object Totals(DateTime fromUtc, DateTime toUtc)
            {
                var q = paid.Where(p => p.PaidAt >= fromUtc && p.PaidAt < toUtc);
                return new
                {
                    amount = q.Sum(p => (decimal?)p.Amount) ?? 0,
                    count = q.Count()
                };
            }

            // Today's rows are few, so group them in memory.
            var todayRows = paid
                .Where(p => p.PaidAt >= todayRange.StartUtc && p.PaidAt < todayRange.EndUtc)
                .Select(p => new
                {
                    p.Amount,
                    p.VehicleId,
                    CollectorId = (p.TransactionId != null && p.TransactionId != "") ? null : p.CollectorId,
                    Movement = p.Movement != null ? p.Movement.Name : p.MovementType,
                    Collector = (p.TransactionId != null && p.TransactionId != "") ? VehicleTax.Web.Services.PaymentSource.GolisLabel : p.Collector != null ? p.Collector.Username : null
                })
                .ToList();

            var pendingToday = payments
                .Where(p => !p.IsPaid &&
                            p.PaidAt >= todayRange.StartUtc && p.PaidAt < todayRange.EndUtc);

            return Ok(new
            {
                scope = collectorId.HasValue ? "collector" : "all",
                collectorId,
                today = new
                {
                    amount = todayRows.Sum(r => r.Amount),
                    count = todayRows.Count,
                    vehicles = todayRows.Select(r => r.VehicleId).Distinct().Count()
                },
                yesterday = Totals(yesterdayRange.StartUtc, yesterdayRange.EndUtc),
                week = Totals(weekStartUtc, todayRange.EndUtc),
                month = Totals(monthStartUtc, todayRange.EndUtc),
                pendingToday = new
                {
                    amount = pendingToday.Sum(p => (decimal?)p.Amount) ?? 0,
                    count = pendingToday.Count()
                },
                activeCollectors = todayRows
                    .Where(r => r.CollectorId.HasValue)
                    .Select(r => r.CollectorId)
                    .Distinct()
                    .Count(),
                registeredVehicles = _context.Vehicles.Count(),
                byMovement = todayRows
                    .GroupBy(r => string.IsNullOrWhiteSpace(r.Movement) ? "Other" : r.Movement)
                    .Select(g => new { name = g.Key, count = g.Count(), amount = g.Sum(r => r.Amount) })
                    .OrderByDescending(g => g.amount)
                    .Take(5)
                    .ToList(),
                topCollectors = todayRows
                    .GroupBy(r => r.Collector ?? "Unassigned")
                    .Select(g => new { name = g.Key, count = g.Count(), amount = g.Sum(r => r.Amount) })
                    .OrderByDescending(g => g.amount)
                    .Take(5)
                    .ToList()
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
                query = query.Where(p => p.PaidAt >= AppTime.ToUtc(fromDate.Value.Date));
            }

            if (toDate.HasValue)
            {
                query = query.Where(p => p.PaidAt < AppTime.ToUtc(toDate.Value.Date.AddDays(1)));
            }

            // Use the snapshot Payment.Checkpoint first; if null, fall back
            // to the collector's current checkpoint so historical payments
            // without a snapshot are still attributed.
            // Only registered checkpoints; payments with no checkpoint are not grouped.
            var totals = query
                .AsEnumerable()
                .Select(p => new { CheckpointId = p.CheckpointId ?? p.Collector?.CheckpointId, p.Amount })
                .Where(x => x.CheckpointId.HasValue)
                .GroupBy(x => x.CheckpointId!.Value)
                .ToDictionary(g => g.Key, g => (Count: g.Count(), Amount: g.Sum(x => x.Amount)));
            var items = _context.Checkpoints.AsNoTracking().ToList()
                .Select(c => new
                {
                    checkpoint = c.Name,
                    totalPayments = totals.TryGetValue(c.Id, out var t) ? t.Count : 0,
                    totalAmount = totals.TryGetValue(c.Id, out var t2) ? t2.Amount : 0m
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
                query = query.Where(p => p.PaidAt >= AppTime.ToUtc(fromDate.Value.Date));
            }

            if (toDate.HasValue)
            {
                query = query.Where(p => p.PaidAt < AppTime.ToUtc(toDate.Value.Date.AddDays(1)));
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