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

        // Everything the payment screen needs in one call: the tariffs that
        // can be collected for this car type (with amounts) and whether the
        // plate was already paid today.
        var tariffs = await MovementsController.WithAmountsAsync(_context, vehicle.CarTypeId);
        var (dayStart, dayEnd) = AppTime.GetUtcDayRange(AppTime.Today);
        var paidToday = await _context.Payments.AsNoTracking()
            .Where(p => p.VehicleId == vehicle.Id && p.IsPaid && !p.IsReverted && p.PaidAt >= dayStart && p.PaidAt < dayEnd)
            .OrderByDescending(p => p.PaidAt)
            .Select(p => (DateTime?)p.PaidAt)
            .FirstOrDefaultAsync();

        // Recent invoices for this vehicle, so the collector can reopen one
        // (check whether the customer paid it with the short code, print it).
        var invoices = (await _context.Payments.AsNoTracking()
                .Include(p => p.Movement)
                .Include(p => p.Collector)
                .Where(p => p.VehicleId == vehicle.Id && !p.IsReverted)
                .OrderByDescending(p => p.Id)
                .Take(10)
                .ToListAsync())
            .Select(p => new
            {
                paymentId = p.Id,
                invoiceNumber = p.InvoiceNumber,
                shortCode = p.ShortCode,
                amount = p.Amount,
                movementId = p.MovementId,
                movement = p.Movement?.Name ?? p.MovementType,
                isPaid = p.IsPaid,
                status = p.IsPaid ? "Paid" : "Pending",
                paidAt = DateTime.SpecifyKind(p.PaidAt, DateTimeKind.Utc),
                golisBillNo = p.TransactionId,
                paidBy = p.PaidBy,
                collector = p.Collector?.Username,
            })
            .ToList();

        // Latest collected (paid, not reverted) payment so the plate checker
        // can show paid / not-paid status.
        var lastPayment = await _context.Payments
            .Include(p => p.Movement)
            .Include(p => p.Checkpoint)
            .Include(p => p.Collector).ThenInclude(c => c!.Checkpoint)
            .Where(p => p.VehicleId == vehicle.Id && p.IsPaid && !p.IsReverted)
            .OrderByDescending(p => p.PaidAt)
            .FirstOrDefaultAsync();

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
            },
            isPaid = lastPayment != null,
            paymentId = lastPayment?.Id,
            // Stored as UTC; mark the kind so it serializes with a trailing "Z".
            paidAt = lastPayment == null
                ? (DateTime?)null
                : DateTime.SpecifyKind(lastPayment.PaidAt, DateTimeKind.Utc),
            amount = lastPayment?.Amount,
            movement = lastPayment?.Movement?.Name ?? lastPayment?.MovementType,
            invoiceNumber = lastPayment?.InvoiceNumber,
            golisBillNo = lastPayment?.TransactionId,
            // Number that paid the last payment (Golis sender).
            paidBy = lastPayment?.PaidBy,
            // Where it was paid: the payment's checkpoint, else its collector's.
            checkpoint = lastPayment?.Checkpoint?.Name ?? lastPayment?.Collector?.Checkpoint?.Name,
            paidToday = paidToday != null,
            paidTodayAt = paidToday == null ? (DateTime?)null : DateTime.SpecifyKind(paidToday.Value, DateTimeKind.Utc),
            tariffs,
            invoices
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
        var mobile = VehicleTax.Web.Services.SomaliPhone.Normalize(model.Mobile);
        if (mobile == null)
            return BadRequest(new { message = VehicleTax.Web.Services.SomaliPhone.Error });
        model.Mobile = mobile;

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

        var mobile = VehicleTax.Web.Services.SomaliPhone.Normalize(model.Mobile);
        if (mobile == null)
            return BadRequest(new { message = VehicleTax.Web.Services.SomaliPhone.Error });

        vehicle.PlateNumber = model.PlateNumber.Trim().ToUpper();
        vehicle.OwnerName = model.OwnerName.Trim();
        vehicle.Mobile = mobile;
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
