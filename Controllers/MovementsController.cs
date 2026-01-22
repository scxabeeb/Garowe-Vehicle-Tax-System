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
            var movements = await _context.Movements
                .Select(m => new
                {
                    id = m.Id,
                    name = m.Name,
                    carTypeId = m.CarTypeId   // 🔑 Critical for Flutter filtering
                })
                .ToListAsync();

            return Ok(new
            {
                status = "success",
                items = movements
            });
        }

        // ======================================
        // GET: api/movements/by-car-type/{carTypeId}
        // Backend filtering (optional but powerful)
        // ======================================
        [HttpGet("by-car-type/{carTypeId}")]
        public async Task<IActionResult> GetByCarType(int carTypeId)
        {
            var movements = await _context.Movements
                .Where(m => m.CarTypeId == carTypeId)
                .Select(m => new
                {
                    id = m.Id,
                    name = m.Name,
                    carTypeId = m.CarTypeId
                })
                .ToListAsync();

            return Ok(new
            {
                status = "success",
                items = movements
            });
        }
    }
}
