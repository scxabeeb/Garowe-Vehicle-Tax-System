using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;

namespace VehicleTax.Web.Controllers;

[ApiController]
[Route("api/movements")]
public class MovementsController : ControllerBase
{
    private readonly AppDbContext _context;

    public MovementsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetMovements()
    {
        var movements = await _context.Movements
            .Select(m => new
            {
                id = m.Id,
                name = m.Name
            })
            .ToListAsync();

        return Ok(movements);
    }
}
