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
                    isReverted = p.IsReverted
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
    }
}
