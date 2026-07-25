using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;

namespace VehicleTax.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GolisWebhookController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly GolisWebhookSettings _settings;

    public GolisWebhookController(AppDbContext context, IOptions<GolisWebhookSettings> options)
    {
        _context = context;
        _settings = options.Value;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] GolisWebhookPayload payload)
    {
        // ── Basic Auth check ──────────────────────────────────────────────
        if (!string.IsNullOrEmpty(_settings.ApiUsername))
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
                return Unauthorized(new { message = "Invalid credentials format." });

            var username = credentials[..colonIndex];
            var password = credentials[(colonIndex + 1)..];

            if (username != _settings.ApiUsername || password != _settings.ApiPassword)
            {
                Response.Headers["WWW-Authenticate"] = "Basic realm=\"GolisAPI\"";
                return Unauthorized(new { message = "Invalid username or password." });
            }
        }
        // ─────────────────────────────────────────────────────────────────
        if (payload == null)
        {
            return BadRequest(new { message = "Invalid webhook payload." });
        }

        if (!Request.Headers.TryGetValue("X-Golis-Secret", out var secret) || secret != _settings.Secret)
        {
            return Unauthorized(new { message = "Invalid webhook secret." });
        }

        if (string.IsNullOrWhiteSpace(payload.PlateNumber) || string.IsNullOrWhiteSpace(payload.Movement))
        {
            return BadRequest(new { message = "PlateNumber and Movement are required." });
        }

        Payment? payment = null;
        if (!string.IsNullOrWhiteSpace(payload.InvoiceNumber))
        {
            var invoiceId = ParseInvoiceId(payload.InvoiceNumber);
            if (invoiceId.HasValue)
            {
                payment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.Id == invoiceId.Value && !p.IsPaid && !p.IsReverted);
            }
        }

        if (payment == null)
        {
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.PlateNumber.Trim().ToUpper() == payload.PlateNumber.Trim().ToUpper());

            if (vehicle == null)
            {
                return NotFound(new { message = "Vehicle not found." });
            }

            var movement = await _context.Movements
                .FirstOrDefaultAsync(m => m.Name == payload.Movement);

            if (movement == null)
            {
                return NotFound(new { message = "Movement not found." });
            }

            payment = new Payment
            {
                VehicleId = vehicle.Id,
                MovementId = movement.Id,
                MovementType = movement.Name,
                Amount = payload.Amount,
                PaidAt = DateTime.UtcNow,
                ReceiptReferenceId = null,
                CollectorId = null,
                IsPaid = true
            };

            _context.Payments.Add(payment);
        }
        else
        {
            payment.IsPaid = true;
            payment.PaidAt = DateTime.UtcNow;
            payment.Amount = payload.Amount;
        }

        if (!string.IsNullOrWhiteSpace(payload.ReferenceNumber))
        {
            var receipt = await _context.ReceiptReferences
                .FirstOrDefaultAsync(r => r.ReferenceNumber == payload.ReferenceNumber && !r.IsUsed);

            if (receipt != null)
            {
                payment.ReceiptReferenceId = receipt.Id;
                receipt.IsUsed = true;
                receipt.UsedAt = DateTime.UtcNow;
                receipt.UsedBy = "GolisWebhook";
                receipt.VehicleId = payment.VehicleId;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new { status = "success", message = "Payment recorded from Golis webhook." });
    }

    private static int? ParseInvoiceId(string invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            return null;

        invoiceNumber = invoiceNumber.Trim();

        // Backward compatibility: INV000123
        if (invoiceNumber.StartsWith("INV", StringComparison.OrdinalIgnoreCase))
        {
            var digits = invoiceNumber.Substring(3);
            if (int.TryParse(digits, out var id))
                return id;
        }

        // New format: YYYYMM + Serial (e.g. 202606000123)
        if (invoiceNumber.Length > 6 &&
            invoiceNumber.All(char.IsDigit) &&
            int.TryParse(invoiceNumber.Substring(6), out var serialId))
        {
            return serialId;
        }

        // Fallback: plain numeric id
        if (int.TryParse(invoiceNumber, out var fallbackId))
            return fallbackId;

        return null;
    }
}

public class GolisWebhookPayload
{
    public string PlateNumber { get; set; } = string.Empty;
    public string Movement { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? TransactionId { get; set; }
}
