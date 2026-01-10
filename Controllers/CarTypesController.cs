using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;

namespace VehicleTax.Web.Controllers;

[ApiController]
[Route("api/cartypes")]
public class CarTypesController : ControllerBase
{
    private readonly AppDbContext _context;

    public CarTypesController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/cartypes
    [HttpGet]
    public async Task<IActionResult> GetCarTypes()
    {
        var carTypes = await _context.CarTypes
            .Select(c => new
            {
                id = c.Id,
                name = c.Name
            })
            .ToListAsync();

        return Ok(carTypes);
    }
}
