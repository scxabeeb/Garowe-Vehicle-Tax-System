using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiclesController : ControllerBase
{
    private readonly AppDbContext _context;

    public VehiclesController(AppDbContext context)
    {
        _context = context;
    }

    // =========================================
    // 🔍 SEARCH VEHICLE
    // =========================================
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { message = "Search query required" });

        q = q.Trim().ToUpper();

        var vehicles = await _context.Vehicles
            .Include(v => v.CarType)
            .Where(v =>
                v.PlateNumber.ToUpper().Contains(q) ||
                v.Mobile.Contains(q))
            .Select(v => new
            {
                id = v.Id,
                plateNumber = v.PlateNumber,
                ownerName = v.OwnerName,
                mobile = v.Mobile,
                carTypeId = v.CarTypeId,
                carType = new
                {
                    id = v.CarType!.Id,
                    name = v.CarType.Name
                }
            })
            .ToListAsync();

        return Ok(vehicles);
    }

    // =========================================
    // 🔍 GET VEHICLE BY PLATE
    // =========================================
    [HttpGet("by-plate/{plate}")]
    public async Task<IActionResult> GetByPlate(string plate)
    {
        var cleanPlate = plate.Trim().ToUpper();

        var vehicle = await _context.Vehicles
            .Include(v => v.CarType)
            .FirstOrDefaultAsync(v => v.PlateNumber.ToUpper() == cleanPlate);

        if (vehicle == null)
            return NotFound(new { message = "Vehicle not found" });

        return Ok(new
        {
            id = vehicle.Id,
            plateNumber = vehicle.PlateNumber,
            ownerName = vehicle.OwnerName,
            mobile = vehicle.Mobile,
            carTypeId = vehicle.CarTypeId,
            carType = new
            {
                id = vehicle.CarType!.Id,
                name = vehicle.CarType.Name
            }
        });
    }

    // =========================================
    // 📝 REGISTER VEHICLE (WITH MOBILE)
    // =========================================
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] Vehicle model)
    {
        if (model == null ||
            string.IsNullOrWhiteSpace(model.PlateNumber) ||
            string.IsNullOrWhiteSpace(model.OwnerName) ||
            string.IsNullOrWhiteSpace(model.Mobile))
        {
            return BadRequest(new { message = "Plate, Owner and Mobile required" });
        }

        model.PlateNumber = model.PlateNumber.Trim().ToUpper();
        model.OwnerName = model.OwnerName.Trim();
        model.Mobile = model.Mobile.Trim();

        var exists = await _context.Vehicles
            .AnyAsync(v => v.PlateNumber == model.PlateNumber);

        if (exists)
            return Conflict(new { message = "Vehicle already exists" });

        _context.Vehicles.Add(model);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            id = model.Id,
            plateNumber = model.PlateNumber,
            ownerName = model.OwnerName,
            mobile = model.Mobile,
            carTypeId = model.CarTypeId
        });
    }

    // =========================================
    // ✏️ UPDATE VEHICLE (WITH MOBILE)
    // =========================================
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Vehicle model)
    {
        var vehicle = await _context.Vehicles.FindAsync(id);
        if (vehicle == null)
            return NotFound(new { message = "Vehicle not found" });

        vehicle.PlateNumber = model.PlateNumber.Trim().ToUpper();
        vehicle.OwnerName = model.OwnerName.Trim();
        vehicle.Mobile = model.Mobile.Trim();
        vehicle.CarTypeId = model.CarTypeId;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Vehicle updated successfully" });
    }

    // =========================================
    // 🗑️ DELETE VEHICLE
    // =========================================
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var vehicle = await _context.Vehicles.FindAsync(id);
        if (vehicle == null)
            return NotFound(new { message = "Vehicle not found" });

        _context.Vehicles.Remove(vehicle);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Vehicle deleted successfully" });
    }
}
