using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;

namespace VehicleTax.Web.Controllers
{
    [ApiController]
    [Route("api/movements")]
    public class MovementsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MovementsController(AppDbContext context)
        {
            _context = context;
        }

        // ======================================
        // GET: api/movements
        // Returns all movements with CarTypeId
        // Flutter uses this to filter movements
        // ======================================
        [HttpGet]
        public async Task<IActionResult> GetMovements()
        {
            // Only active tariffs can be collected.
            var movements = await _context.Movements
                .Where(m => m.IsActive)
                .GroupBy(m => new { m.CarTypeId, m.Name })
                .Select(g => new
                {
                    id = g.Min(m => m.Id),
                    name = g.Key.Name,
                    carTypeId = g.Key.CarTypeId   // 🔑 Critical for Flutter filtering
                })
                .OrderBy(m => m.carTypeId)
                .ThenBy(m => m.name)
                .ToListAsync();

            return Ok(new
            {
                status = "success",
                items = movements
            });
        }

        // ======================================
        // GET: api/movements/with-amounts/{carTypeId}
        // Active tariffs that can be collected for this car type, with their
        // tax amount, in one call (the app used one call per tariff).
        // ======================================
        [HttpGet("with-amounts/{carTypeId}")]
        public async Task<IActionResult> GetWithAmounts(int carTypeId) =>
            Ok(new { status = "success", items = await WithAmountsAsync(_context, carTypeId) });

        public static async Task<List<object>> WithAmountsAsync(AppDbContext context, int carTypeId) =>
            (await context.TaxAmounts.AsNoTracking()
                .Where(t => t.CarTypeId == carTypeId && t.Movement != null && t.Movement.IsActive)
                .OrderBy(t => t.Movement!.Name)
                .Select(t => new { id = t.MovementId, name = t.Movement!.Name, carTypeId, taxAmount = t.Amount })
                .ToListAsync())
            .GroupBy(t => t.id).Select(g => (object)g.First())
            .ToList();

        // ======================================
        // GET: api/movements/by-car-type/{carTypeId}
        // Backend filtering (optional but powerful)
        // ======================================
        [HttpGet("by-car-type/{carTypeId}")]
        public async Task<IActionResult> GetByCarType(int carTypeId)
        {
            var movements = await _context.Movements
                .Where(m => m.CarTypeId == carTypeId && m.IsActive)
                .GroupBy(m => m.Name)
                .Select(g => new
                {
                    id = g.Min(m => m.Id),
                    name = g.Key,
                    carTypeId = carTypeId
                })
                .OrderBy(m => m.name)
                .ToListAsync();

            return Ok(new
            {
                status = "success",
                items = movements
            });
        }
    }
}
