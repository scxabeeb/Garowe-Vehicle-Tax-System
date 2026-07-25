using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Services.Golis;

namespace VehicleTax.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MobileMoneyController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IGolisApiService _golisApi;

    public MobileMoneyController(AppDbContext context, IGolisApiService golisApi)
    {
        _context = context;
        _golisApi = golisApi;
    }

    [HttpPost("authenticate")]
    public async Task<IActionResult> Authenticate()
    {
        var token = await _golisApi.AuthenticateAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { status = "error", message = "Golis authentication failed." });
        }

        return Ok(new { status = "success", accessToken = token });
    }

    [HttpPost("pay")]
    public async Task<IActionResult> Pay([FromBody] MobileMoneyPaymentDto dto)
    {
        if (dto == null)
        {
            return BadRequest(new { status = "error", message = "Invalid request data." });
        }

        var vehicle = await _context.Vehicles.FindAsync(dto.VehicleId);
        if (vehicle == null)
        {
            return BadRequest(new { status = "error", message = "Vehicle not found." });
        }

        ReceiptReference? receipt = null;
        if (!string.IsNullOrWhiteSpace(dto.ReferenceNumber))
        {
            receipt = await _context.ReceiptReferences.FirstOrDefaultAsync(r => r.ReferenceNumber == dto.ReferenceNumber);
            if (receipt == null || receipt.IsUsed)
            {
                return BadRequest(new { status = "error", message = "Invalid or already used receipt reference." });
            }
        }

        var movement = dto.MovementId > 0
            ? await _context.Movements.FindAsync(dto.MovementId)
            : await _context.Movements.FirstOrDefaultAsync(m => m.Name == dto.Movement);

        if (movement == null)
        {
            return BadRequest(new { status = "error", message = "Invalid movement." });
        }

        var collector = await _context.Users.FindAsync(dto.CollectorId);
        if (collector == null)
        {
            return BadRequest(new { status = "error", message = "Collector not found." });
        }

        var tax = await _context.TaxAmounts
            .Include(t => t.Movement)
            .FirstOrDefaultAsync(t => t.CarTypeId == vehicle.CarTypeId && t.MovementId == movement.Id);

        if (tax == null)
        {
            return BadRequest(new { status = "error", message = "Tax configuration not found for this movement and car type." });
        }

        var expectedAmount = tax.Amount * Math.Max(dto.Quantity, 1);
        if (dto.Amount <= 0m)
        {
            dto.Amount = expectedAmount;
        }

        if (dto.Amount != expectedAmount)
        {
            return BadRequest(new { status = "error", message = "Amount does not match the configured fee." });
        }

        var apiRequest = new GolisPaymentRequest
        {
            PhoneNumber = dto.PhoneNumber,
            Amount = dto.Amount,
            Currency = dto.Currency ?? "SOS",
            Description = $"Vehicle tax payment for {vehicle.PlateNumber}",
            ReceiptReference = receipt.ReferenceNumber,
            ClientReference = $"vehicle-{vehicle.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}"
        };

        var apiResponse = await _golisApi.SendPaymentAsync(apiRequest);
        if (!apiResponse.Success)
        {
            return StatusCode(502, new
            {
                status = "error",
                message = apiResponse.Message ?? "Failed to process Golis mobile money payment."
            });
        }

        var payment = new Payment
        {
            VehicleId = vehicle.Id,
            MovementId = movement.Id,
            MovementType = movement.Name,
            Amount = dto.Amount,
            PaidAt = DateTime.UtcNow,
            ReceiptReferenceId = receipt?.Id,
            CollectorId = collector.Id
        };

        _context.Payments.Add(payment);

        if (receipt != null)
        {
            receipt.IsUsed = true;
            receipt.UsedAt = DateTime.UtcNow;
            receipt.UsedBy = collector.Username;
            receipt.VehicleId = vehicle.Id;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            status = "success",
            message = "Mobile money payment completed.",
            transactionId = apiResponse.TransactionId,
            receipt = receipt.ReferenceNumber
        });
    }
}

public class MobileMoneyPaymentDto
{
    public int VehicleId { get; set; }
    public string Movement { get; set; } = string.Empty;
    public int MovementId { get; set; }
    public decimal Amount { get; set; }
    public int Quantity { get; set; } = 1;
    public string ReferenceNumber { get; set; } = string.Empty;
    public int CollectorId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Currency { get; set; } = "SOS";
}
