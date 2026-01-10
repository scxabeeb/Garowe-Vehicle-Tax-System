using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;

namespace VehicleTax.Web.Controllers
{
    [ApiController]
    [Route("api/tax")]
    public class TaxController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TaxController(AppDbContext context)
        {
            _context = context;
        }

        // =========================================
        // 🔹 FLUTTER ENDPOINT (STRING MOVEMENT)
        // GET: api/tax/amount?carTypeId=1&movement=Passing
        // =========================================
        [HttpGet("amount")]
        public IActionResult GetTax(int carTypeId, string movement)
        {
            var tax = _context.TaxAmounts
                .Include(t => t.Movement)
                .FirstOrDefault(t =>
                    t.CarTypeId == carTypeId &&
                    t.Movement != null &&
                    t.Movement.Name == movement
                );

            if (tax == null)
                return NotFound();

            return Ok(new
            {
                amount = tax.Amount
            });
        }

        // =========================================
        // 🔹 NEW ENDPOINT (MOVEMENT ID)
        // GET: api/tax/amount-by-movement?carTypeId=1&movementId=2
        // =========================================
        [HttpGet("amount-by-movement")]
        public IActionResult GetTaxByMovement(int carTypeId, int movementId)
        {
            var tax = _context.TaxAmounts
                .FirstOrDefault(t =>
                    t.CarTypeId == carTypeId &&
                    t.MovementId == movementId
                );

            if (tax == null)
                return NotFound();

            return Ok(new
            {
                amount = tax.Amount
            });
        }
    }
}
