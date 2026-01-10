using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;

namespace VehicleTax.Web.Controllers
{
    [ApiController]
    [Route("api/references")]
    public class ReferencesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReferencesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/references/ABC123
        [HttpGet("{refNumber}")]
        public async Task<IActionResult> Check(string refNumber)
        {
            if (string.IsNullOrWhiteSpace(refNumber))
            {
                return BadRequest(new
                {
                    exists = false,
                    isUsed = false
                });
            }

            var reference = await _context.ReceiptReferences
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ReferenceNumber == refNumber);

            if (reference == null)
            {
                return Ok(new
                {
                    exists = false,
                    isUsed = false
                });
            }

            return Ok(new
            {
                exists = true,
                isUsed = reference.IsUsed
            });
        }
    }
}
