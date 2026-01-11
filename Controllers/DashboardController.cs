using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
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
        public IActionResult GetDashboard()
        {
            // Always use UTC for consistency
            var today = DateTime.UtcNow.Date;

            // 🔴 IMPORTANT: Only VALID (not reverted) payments
            var validPayments = _context.Payments
                .Where(p => !p.IsReverted);

            var totalVehicles = _context.Vehicles.Count();

            var totalPayments = validPayments.Count();

            var todayTotal = validPayments
                .Where(p => p.PaidAt >= today && p.PaidAt < today.AddDays(1))
                .Sum(p => (decimal?)p.Amount) ?? 0;

            var todaysPayments = validPayments
                .Include(p => p.Vehicle)
                .Where(p => p.PaidAt >= today && p.PaidAt < today.AddDays(1))
                .OrderByDescending(p => p.PaidAt)
                .Take(50)
                .Select(p => new
                {
                    plate = p.Vehicle != null ? p.Vehicle.PlateNumber : "N/A",
                    movement = p.MovementType,
                    amount = p.Amount,
                    time = p.PaidAt.ToString("HH:mm")
                })
                .ToList();

            return Ok(new
            {
                totalVehicles,
                totalPayments,
                todayTotal,
                todaysPayments
            });
        }
    }
}
