using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using VehicleTax.Web.Data;
using System;
using System.Linq;

namespace VehicleTax.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]   // <<< IMPORTANT: Allow Flutter access
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
            // Use UTC to avoid date mismatches
            var today = DateTime.UtcNow.Date;

            var totalVehicles = _context.Vehicles.Count();

            var totalPayments = _context.Payments.Count();

            var todayTotal = _context.Payments
                .Where(p => p.PaidAt >= today && p.PaidAt < today.AddDays(1))
                .Sum(p => (decimal?)p.Amount) ?? 0;

            var todaysPayments = _context.Payments
                .Include(p => p.Vehicle)
                .Where(p => p.PaidAt >= today && p.PaidAt < today.AddDays(1))
                .OrderByDescending(p => p.PaidAt)
                .Take(10)
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
