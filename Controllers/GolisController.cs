using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Controllers;

[ApiController]
[Route("api/golis")]
public class GolisController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly GolisWebhookSettings _settings;

    public GolisController(AppDbContext context, IOptions<GolisWebhookSettings> options)
    {
        _context = context;
        _settings = options.Value;
    }

    [HttpGet("queryBillInfo")]
    public async Task<IActionResult> QueryBillInfo([FromQuery] GolisBillQueryRequest request)
    {
        return await QueryCoreAsync(request);
    }

    [HttpPost("queryBillInfo")]
    public async Task<IActionResult> QueryBillInfoPost([FromBody] GolisBillQueryRequest request)
    {
        return await QueryCoreAsync(request);
    }

    private async Task<IActionResult> QueryCoreAsync(GolisBillQueryRequest request)
    {
        var authResult = ValidateGolisAuth();
        if (authResult != null)
        {
            return authResult;
        }

        if (request == null)
        {
            return BadRequest(new { status = "error", message = "Invalid request payload." });
        }

        var invoiceNumber = request.InvoiceNumber?.Trim();
        var plateNumber = request.PlateNumber?.Trim();
        var movementName = request.Movement?.Trim();

        if (string.IsNullOrWhiteSpace(invoiceNumber) &&
            (string.IsNullOrWhiteSpace(plateNumber) || string.IsNullOrWhiteSpace(movementName)))
        {
            return BadRequest(new
            {
                status = "error",
                message = "Provide invoiceNumber, or provide both plateNumber and movement."
            });
        }

        if (!string.IsNullOrWhiteSpace(invoiceNumber))
        {
            var invoiceId = ParseInvoiceId(invoiceNumber);
            if (!invoiceId.HasValue)
            {
                return NotFound(new { status = "error", message = "Invoice not found." });
            }

            var payment = await _context.Payments
                .Include(p => p.Vehicle)
                .Include(p => p.Movement)
                .Include(p => p.Collector)
                .Include(p => p.ReceiptReference)
                .FirstOrDefaultAsync(p => p.Id == invoiceId.Value && !p.IsReverted);

            if (payment == null)
            {
                return NotFound(new { status = "error", message = "Invoice not found." });
            }

            return Ok(new
            {
                status = "success",
                message = "Bill found.",
                bill = new
                {
                    paymentId = payment.Id,
                    invoiceNumber = payment.InvoiceNumber,
                    shortCode = payment.ShortCode,
                    plateNumber = payment.Vehicle?.PlateNumber,
                    ownerName = payment.Vehicle?.OwnerName,
                    movement = payment.Movement?.Name ?? payment.MovementType,
                    amount = payment.Amount,
                    currency = "SOS",
                    isPaid = payment.IsPaid,
                    isReverted = payment.IsReverted,
                    canPay = !payment.IsPaid && !payment.IsReverted,
                    paidAt = payment.IsPaid ? (DateTime?)payment.PaidAt : null,
                    collector = payment.Collector?.Username,
                    referenceNumber = payment.ReceiptReference?.ReferenceNumber
                }
            });
        }

        var normalizedPlate = plateNumber!.ToUpperInvariant();
        var vehicle = await _context.Vehicles
            .Include(v => v.CarType)
            .FirstOrDefaultAsync(v => v.PlateNumber.ToUpper() == normalizedPlate);

        if (vehicle == null)
        {
            return NotFound(new { status = "error", message = "Vehicle not found." });
        }

        var movement = await _context.Movements
            .FirstOrDefaultAsync(m => m.Name.ToUpper() == movementName!.ToUpper());

        if (movement == null)
        {
            return NotFound(new { status = "error", message = "Movement not found." });
        }

        var existingInvoice = await _context.Payments
            .Include(p => p.ReceiptReference)
            .Where(p => p.VehicleId == vehicle.Id && p.MovementId == movement.Id && !p.IsReverted)
            .OrderByDescending(p => p.PaidAt)
            .FirstOrDefaultAsync();

        if (existingInvoice != null)
        {
            return Ok(new
            {
                status = "success",
                message = "Bill found.",
                bill = new
                {
                    paymentId = existingInvoice.Id,
                    invoiceNumber = existingInvoice.InvoiceNumber,
                    shortCode = existingInvoice.ShortCode,
                    plateNumber = vehicle.PlateNumber,
                    ownerName = vehicle.OwnerName,
                    movement = movement.Name,
                    amount = existingInvoice.Amount,
                    currency = "SOS",
                    isPaid = existingInvoice.IsPaid,
                    isReverted = existingInvoice.IsReverted,
                    canPay = !existingInvoice.IsPaid && !existingInvoice.IsReverted,
                    paidAt = existingInvoice.IsPaid ? (DateTime?)existingInvoice.PaidAt : null,
                    referenceNumber = existingInvoice.ReceiptReference?.ReferenceNumber
                }
            });
        }

        var tax = await _context.TaxAmounts
            .FirstOrDefaultAsync(t => t.CarTypeId == vehicle.CarTypeId && t.MovementId == movement.Id);

        if (tax == null)
        {
            return NotFound(new { status = "error", message = "Tax configuration not found." });
        }

        return Ok(new
        {
            status = "success",
            message = "Bill preview found. Invoice has not been generated yet.",
            bill = new
            {
                paymentId = (int?)null,
                invoiceNumber = (string?)null,
                shortCode = (string?)null,
                plateNumber = vehicle.PlateNumber,
                ownerName = vehicle.OwnerName,
                movement = movement.Name,
                amount = tax.Amount,
                currency = "SOS",
                isPaid = false,
                isReverted = false,
                canPay = false
            }
        });
    }

    private IActionResult? ValidateGolisAuth()
    {
        if (!string.IsNullOrWhiteSpace(_settings.ApiUsername))
        {
            if (!Request.Headers.TryGetValue("Authorization", out var authHeader) ||
                !AuthenticationHeaderValue.TryParse(authHeader, out var parsed) ||
                !string.Equals(parsed.Scheme, "Basic", StringComparison.OrdinalIgnoreCase) ||
                parsed.Parameter == null)
            {
                Response.Headers["WWW-Authenticate"] = "Basic realm=\"GolisAPI\"";
                return Unauthorized(new { message = "Basic authentication required." });
            }

            string credentials;
            try
            {
                credentials = Encoding.UTF8.GetString(Convert.FromBase64String(parsed.Parameter));
            }
            catch
            {
                return Unauthorized(new { message = "Invalid credentials encoding." });
            }

            var colonIndex = credentials.IndexOf(':');
            if (colonIndex < 0)
            {
                return Unauthorized(new { message = "Invalid credentials format." });
            }

            var username = credentials[..colonIndex];
            var password = credentials[(colonIndex + 1)..];

            if (username != _settings.ApiUsername || password != _settings.ApiPassword)
            {
                Response.Headers["WWW-Authenticate"] = "Basic realm=\"GolisAPI\"";
                return Unauthorized(new { message = "Invalid username or password." });
            }
        }

        if (!string.IsNullOrWhiteSpace(_settings.Secret))
        {
            if (!Request.Headers.TryGetValue("X-Golis-Secret", out var secret) || secret != _settings.Secret)
            {
                return Unauthorized(new { message = "Invalid webhook secret." });
            }
        }

        return null;
    }

    private static int? ParseInvoiceId(string invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            return null;
        }

        invoiceNumber = invoiceNumber.Trim();

        if (invoiceNumber.StartsWith("INV", StringComparison.OrdinalIgnoreCase))
        {
            var digits = invoiceNumber.Substring(3);
            if (int.TryParse(digits, out var id))
            {
                return id;
            }
        }

        if (invoiceNumber.Length > 6 &&
            invoiceNumber.All(char.IsDigit) &&
            int.TryParse(invoiceNumber.Substring(6), out var serialId))
        {
            return serialId;
        }

        if (int.TryParse(invoiceNumber, out var fallbackId))
        {
            return fallbackId;
        }

        return null;
    }
}

public class GolisBillQueryRequest
{
    public string? InvoiceNumber { get; set; }
    public string? PlateNumber { get; set; }
    public string? Movement { get; set; }
}