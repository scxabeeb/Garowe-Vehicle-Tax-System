using System.Net.Http.Headers;
using System.Text;
using System.Globalization;
using System.Security.Cryptography;
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
        if (request == null)
        {
            return Ok(BuildErrorResponse(null, null, "1", "Invalid request payload."));
        }

        var authResult = ValidateGolisAuth(request);
        if (authResult != null)
        {
            return authResult;
        }

        var invoiceNumber = FirstNonEmpty(
            request.InvoiceNumber,
            request.InvoiceId,
            request.BillNumber,
            request.RequestBody?.InvoiceNumber,
            request.RequestBody?.InvoiceId,
            request.RequestBody?.BillNumber)?.Trim();

        var plateNumber = FirstNonEmpty(
            request.PlateNumber,
            request.RequestBody?.PlateNumber)?.Trim();

        var movementName = FirstNonEmpty(
            request.Movement,
            request.RequestBody?.Movement)?.Trim();

        if (string.IsNullOrWhiteSpace(invoiceNumber) &&
            (string.IsNullOrWhiteSpace(plateNumber) || string.IsNullOrWhiteSpace(movementName)))
        {
            return Ok(BuildErrorResponse(
                request.RequestId,
                request.SchemaVersion,
                "1",
                "Provide invoiceNumber, or provide both plateNumber and movement."));
        }

        if (!string.IsNullOrWhiteSpace(invoiceNumber))
        {
            var invoiceId = ParseInvoiceId(invoiceNumber);
            if (!invoiceId.HasValue)
            {
                return Ok(BuildErrorResponse(request.RequestId, request.SchemaVersion, "1", "Invoice not found."));
            }

            var payment = await _context.Payments
                .Include(p => p.Vehicle)
                .Include(p => p.Movement)
                .Include(p => p.Collector)
                .Include(p => p.ReceiptReference)
                .FirstOrDefaultAsync(p => p.Id == invoiceId.Value && !p.IsReverted);

            if (payment == null)
            {
                return Ok(BuildErrorResponse(request.RequestId, request.SchemaVersion, "1", "Invoice not found."));
            }

            // When caller provides a full invoice number, enforce exact match to prevent accidental ID collisions.
            if (LooksLikeFullInvoiceNumber(invoiceNumber) &&
                !string.Equals(payment.InvoiceNumber, invoiceNumber, StringComparison.OrdinalIgnoreCase))
            {
                return Ok(BuildErrorResponse(request.RequestId, request.SchemaVersion, "1", "Invoice not found."));
            }

            return Ok(BuildSuccessResponse(
                request.RequestId,
                request.SchemaVersion,
                BuildBillInfo(
                    billId: payment.Id.ToString(),
                    billTo: payment.Vehicle?.OwnerName,
                    billAmount: payment.Amount,
                    billNumber: payment.InvoiceNumber,
                    dueDate: payment.PaidAt,
                    status: payment.IsPaid ? "PAID" : "PENDING",
                    description: $"Vehicle tax for plate {payment.Vehicle?.PlateNumber} - {(payment.Movement?.Name ?? payment.MovementType)}")));
        }

        var normalizedPlate = plateNumber!.ToUpperInvariant();
        var vehicle = await _context.Vehicles
            .Include(v => v.CarType)
            .FirstOrDefaultAsync(v => v.PlateNumber.ToUpper() == normalizedPlate);

        if (vehicle == null)
        {
            return Ok(BuildErrorResponse(request.RequestId, request.SchemaVersion, "1", "Vehicle not found."));
        }

        var movement = await _context.Movements
            .FirstOrDefaultAsync(m => m.Name.ToUpper() == movementName!.ToUpper());

        if (movement == null)
        {
            return Ok(BuildErrorResponse(request.RequestId, request.SchemaVersion, "1", "Movement not found."));
        }

        var existingInvoice = await _context.Payments
            .Include(p => p.ReceiptReference)
            .Where(p => p.VehicleId == vehicle.Id && p.MovementId == movement.Id && !p.IsReverted)
            .OrderByDescending(p => p.PaidAt)
            .FirstOrDefaultAsync();

        if (existingInvoice != null)
        {
            return Ok(BuildSuccessResponse(
                request.RequestId,
                request.SchemaVersion,
                BuildBillInfo(
                    billId: existingInvoice.Id.ToString(),
                    billTo: vehicle.OwnerName,
                    billAmount: existingInvoice.Amount,
                    billNumber: existingInvoice.InvoiceNumber,
                    dueDate: existingInvoice.PaidAt,
                    status: existingInvoice.IsPaid ? "PAID" : "PENDING",
                    description: $"Vehicle tax for plate {vehicle.PlateNumber} - {movement.Name}")));
        }

        var tax = await _context.TaxAmounts
            .FirstOrDefaultAsync(t => t.CarTypeId == vehicle.CarTypeId && t.MovementId == movement.Id);

        if (tax == null)
        {
            return Ok(BuildErrorResponse(request.RequestId, request.SchemaVersion, "1", "Tax configuration not found."));
        }

        return Ok(BuildSuccessResponse(
            request.RequestId,
            request.SchemaVersion,
            BuildBillInfo(
                billId: $"PREVIEW-{vehicle.Id}-{movement.Id}",
                billTo: vehicle.OwnerName,
                billAmount: tax.Amount,
                billNumber: string.Empty,
                dueDate: DateTime.UtcNow,
                status: "PENDING",
                description: $"Vehicle tax preview for plate {vehicle.PlateNumber} - {movement.Name}")));
    }

    private IActionResult? ValidateGolisAuth(GolisBillQueryRequest request)
    {
        var expectedApiKey = _settings.ApiUsername?.Trim();
        var expectedApiPassword = _settings.ApiPassword?.Trim();
        var billerCode = string.IsNullOrWhiteSpace(_settings.BillerCode) ? "173311" : _settings.BillerCode.Trim();

        var hasExpectedRequestHeaderCredentials =
            !string.IsNullOrWhiteSpace(expectedApiKey) ||
            !string.IsNullOrWhiteSpace(expectedApiPassword);

        var providedApiKey = request.RequestHeader?.ApiKey?.Trim();
        var providedApiPassword = request.RequestHeader?.ApiPassword?.Trim();
        var providedInRequestHeader =
            !string.IsNullOrWhiteSpace(providedApiKey) ||
            !string.IsNullOrWhiteSpace(providedApiPassword);

        if (hasExpectedRequestHeaderCredentials && providedInRequestHeader)
        {
            var timestamp = request.RequestHeader?.Timestamp?.Trim();
            if (string.IsNullOrWhiteSpace(timestamp))
            {
                return Unauthorized(BuildErrorResponse(request.RequestId, request.SchemaVersion, "401", "Timestamp is required."));
            }

            if (!string.Equals(providedApiKey, expectedApiKey, StringComparison.Ordinal))
            {
                return Unauthorized(BuildErrorResponse(request.RequestId, request.SchemaVersion, "401", "Invalid username or password."));
            }

            var expectedHash = ComputeMd5Hex($"{timestamp}{expectedApiPassword}{billerCode}");
            if (!string.Equals(providedApiPassword, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(BuildErrorResponse(request.RequestId, request.SchemaVersion, "401", "Invalid username or password."));
            }

            // Request-header credentials passed and validated.
            if (string.IsNullOrWhiteSpace(_settings.Secret))
            {
                return null;
            }
        }

        if (!string.IsNullOrWhiteSpace(_settings.ApiUsername))
        {
            if (!Request.Headers.TryGetValue("Authorization", out var authHeader) ||
                !AuthenticationHeaderValue.TryParse(authHeader, out var parsed) ||
                !string.Equals(parsed.Scheme, "Basic", StringComparison.OrdinalIgnoreCase) ||
                parsed.Parameter == null)
            {
                Response.Headers["WWW-Authenticate"] = "Basic realm=\"GolisAPI\"";
                return Unauthorized(BuildErrorResponse(request.RequestId, request.SchemaVersion, "401", "Basic authentication required."));
            }

            string credentials;
            try
            {
                credentials = Encoding.UTF8.GetString(Convert.FromBase64String(parsed.Parameter));
            }
            catch
            {
                return Unauthorized(BuildErrorResponse(request.RequestId, request.SchemaVersion, "401", "Invalid credentials encoding."));
            }

            var colonIndex = credentials.IndexOf(':');
            if (colonIndex < 0)
            {
                return Unauthorized(BuildErrorResponse(request.RequestId, request.SchemaVersion, "401", "Invalid credentials format."));
            }

            var username = credentials[..colonIndex];
            var password = credentials[(colonIndex + 1)..];

            if (username != _settings.ApiUsername || password != _settings.ApiPassword)
            {
                Response.Headers["WWW-Authenticate"] = "Basic realm=\"GolisAPI\"";
                return Unauthorized(BuildErrorResponse(request.RequestId, request.SchemaVersion, "401", "Invalid username or password."));
            }
        }

        if (!string.IsNullOrWhiteSpace(_settings.Secret))
        {
            if (!Request.Headers.TryGetValue("X-Golis-Secret", out var secret) || secret != _settings.Secret)
            {
                return Unauthorized(BuildErrorResponse(request.RequestId, request.SchemaVersion, "401", "Invalid webhook secret."));
            }
        }

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static object BuildSuccessResponse(string? requestId, string? schemaVersion, object[] billInfo)
    {
        return new Dictionary<string, object?>
        {
            ["requestId"] = string.IsNullOrWhiteSpace(requestId) ? string.Empty : requestId,
            ["schemaVersion"] = string.IsNullOrWhiteSpace(schemaVersion) ? "1.0" : schemaVersion,
            ["responseHeader"] = new Dictionary<string, object?>
            {
                ["timestamp"] = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture),
                ["resultCode"] = "0",
                ["resultMessage"] = "SUCCESS"
            },
            ["billInfo"] = billInfo,
            ["PayInfo"] = null
        };
    }

    private static object BuildErrorResponse(string? requestId, string? schemaVersion, string resultCode, string resultMessage)
    {
        return new Dictionary<string, object?>
        {
            ["requestId"] = string.IsNullOrWhiteSpace(requestId) ? string.Empty : requestId,
            ["schemaVersion"] = string.IsNullOrWhiteSpace(schemaVersion) ? "1.0" : schemaVersion,
            ["responseHeader"] = new Dictionary<string, object?>
            {
                ["timestamp"] = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture),
                ["resultCode"] = resultCode,
                ["resultMessage"] = resultMessage
            },
            ["billInfo"] = Array.Empty<object>(),
            ["PayInfo"] = null
        };
    }

    private static object[] BuildBillInfo(
        string billId,
        string? billTo,
        decimal billAmount,
        string? billNumber,
        DateTime dueDate,
        string status,
        string description)
    {
        return new[]
        {
            new
            {
                billId,
                billTo = (billTo ?? string.Empty).ToUpperInvariant(),
                billAmount = billAmount.ToString("0.00", CultureInfo.InvariantCulture),
                billCurrency = "USD",
                billNumber = billNumber ?? string.Empty,
                dueDate = AppTime.ToLocal(dueDate).ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture),
                status,
                partialPayAllowed = "0",
                description
            }
        };
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

    private static bool LooksLikeFullInvoiceNumber(string invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            return false;
        }

        invoiceNumber = invoiceNumber.Trim();
        return invoiceNumber.Length > 6 && invoiceNumber.All(char.IsDigit);
    }

    private static string ComputeMd5Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = MD5.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

public class GolisBillQueryRequest
{
    public string? RequestId { get; set; }
    public string? SchemaVersion { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? InvoiceId { get; set; }
    public string? BillNumber { get; set; }
    public string? PlateNumber { get; set; }
    public string? Movement { get; set; }
    public GolisBillRequestBody? RequestBody { get; set; }
    public GolisBillRequestHeader? RequestHeader { get; set; }
}

public class GolisBillRequestBody
{
    public string? InvoiceNumber { get; set; }
    public string? InvoiceId { get; set; }
    public string? BillNumber { get; set; }
    public string? PlateNumber { get; set; }
    public string? Movement { get; set; }
}

public class GolisBillRequestHeader
{
    public string? ApiPassword { get; set; }
    public string? ApiKey { get; set; }
    public string? Timestamp { get; set; }
}