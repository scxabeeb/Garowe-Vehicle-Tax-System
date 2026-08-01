using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public PaymentsController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // =======================
        // CREATE PAYMENT
        // =======================
        [HttpPost]
        public IActionResult Pay([FromBody] PaymentDto dto)
        {
            if (dto == null)
                return BadRequest(new { status = "error", message = "Invalid request data" });

            var vehicle = _context.Vehicles.Find(dto.VehicleId);
            if (vehicle == null)
                return BadRequest(new { status = "error", message = "Vehicle not found" });

            ReceiptReference? reference = null;
            if (!string.IsNullOrWhiteSpace(dto.ReferenceNumber))
            {
                reference = _context.ReceiptReferences
                    .FirstOrDefault(r => r.ReferenceNumber == dto.ReferenceNumber);

                if (reference == null)
                    return BadRequest(new { status = "error", message = "Invalid receipt reference" });

                if (reference.IsUsed)
                    return BadRequest(new { status = "error", message = "Receipt reference already used" });
            }

            var movement = _context.Movements
                .FirstOrDefault(m => m.Name == dto.Movement);

            if (movement == null)
                return BadRequest(new { status = "error", message = "Invalid movement" });

            var collector = _context.Users.FirstOrDefault(u => u.Id == dto.CollectorId);
            if (collector == null)
                return BadRequest(new { status = "error", message = "Collector not found" });

            var now = DateTime.UtcNow;

            // ==================================================
            // 🔍 DUPLICATE CHECK (WARNING ONLY, NO BLOCK)
            // ==================================================
            var lastPayment = _context.Payments
                .Where(p =>
                    p.VehicleId == dto.VehicleId &&
                    p.MovementId == movement.Id &&
                    p.Amount == dto.Amount &&
                    !p.IsReverted)
                .OrderByDescending(p => p.PaidAt)
                .FirstOrDefault();

            // If a similar payment exists and user has not confirmed yet → return warning
            if (lastPayment != null && !dto.Force)
            {
                return Ok(new
                {
                    status = "duplicate",
                    type = "warning",
                    message = "A similar payment already exists. Do you want to continue?",
                    lastPaymentAt = lastPayment.PaidAt
                });
            }

            // ==================================================
            // SAVE PAYMENT (Normal or Forced)
            // ==================================================
            var payment = new Payment
            {
                VehicleId = dto.VehicleId,
                MovementId = movement.Id,
                MovementType = dto.Movement,
                Amount = dto.Amount,
                PaidAt = now,
                ReceiptReferenceId = reference?.Id,
                CollectorId = dto.CollectorId,
                IsReverted = false
            };

            _context.Payments.Add(payment);

            if (reference != null)
            {
                reference.IsUsed = true;
                reference.UsedAt = now;
                reference.VehicleId = dto.VehicleId;
                reference.UsedBy = collector.Username;
            }

            _context.SaveChanges();

            return Ok(new
            {
                status = "success",
                message = "Payment saved successfully",
                collector = collector.Username,
                paymentId = payment.Id,
                invoiceId = payment.InvoiceNumber,
                golisBillNo = payment.TransactionId
            });
        }

        // =======================
        // GET PAYMENTS BY COLLECTOR
        // =======================
        [HttpGet("collector/{collectorId}")]
        public IActionResult GetByCollector(int collectorId)
        {
            var payments = _context.Payments
                .Include(p => p.Vehicle)
                .Include(p => p.ReceiptReference)
                .Where(p => p.CollectorId == collectorId && !p.IsReverted)
                .OrderByDescending(p => p.PaidAt)
                .Select(p => new
                {
                    p.Id,
                    invoiceId = p.InvoiceNumber,
                    p.Amount,
                    p.PaidAt,
                    p.MovementType,
                    p.IsReverted,
                    golisBillNo = p.TransactionId,
                    plate = p.Vehicle!.PlateNumber,
                    owner = p.Vehicle.OwnerName,
                    receipt = p.ReceiptReference != null
                        ? p.ReceiptReference.ReferenceNumber
                        : null,
                    collector = p.ReceiptReference != null
                        ? p.ReceiptReference.UsedBy
                        : null
                })
                .ToList();

            return Ok(new
            {
                status = "success",
                items = payments
            });
        }

        // =======================
        // GET RECEIPT PRINT DATA
        // =======================
        [HttpGet("{paymentId}/receipt")]
        public IActionResult GetReceipt(int paymentId)
        {
            var payment = _context.Payments
                .Include(p => p.Vehicle)
                .Include(p => p.Movement)
                .Include(p => p.Collector)
                .Include(p => p.ReceiptReference)
                .FirstOrDefault(p => p.Id == paymentId);

            if (payment == null)
            {
                return NotFound(new { status = "error", message = "Payment not found" });
            }

            return Ok(new
            {
                status = "success",
                receipt = new
                {
                    paymentId = payment.Id,
                    invoiceId = payment.InvoiceNumber,
                    invoiceNumber = payment.InvoiceNumber,
                    shortCode = payment.ShortCode,
                    golisBillNo = payment.TransactionId,
                    paidAt = payment.PaidAt,
                    plateNumber = payment.Vehicle?.PlateNumber,
                    ownerName = payment.Vehicle?.OwnerName,
                    movement = payment.Movement?.Name ?? payment.MovementType,
                    collector = payment.Collector?.Username,
                    amount = payment.Amount,
                    isReverted = payment.IsReverted,
                    referenceNumber = payment.ReceiptReference?.ReferenceNumber,
                    appName = "Garowe Vehicle Tax System"
                }
            });
        }

        // =======================
        // GET RECEIPT LINES (ESC/POS READY)
        // =======================
        [HttpGet("{paymentId}/receipt/lines")]
        public IActionResult GetReceiptLines(
            int paymentId,
            [FromQuery] int paperWidth = 32,
            [FromQuery] bool includeQr = false,
            [FromQuery] string qrFormat = "pipe",
            [FromQuery] bool includeChecksum = false)
        {
            var width = paperWidth == 48 ? 48 : 32;
            var normalizedQrFormat = string.Equals(qrFormat, "json", StringComparison.OrdinalIgnoreCase)
                ? "json"
                : string.Equals(qrFormat, "raw", StringComparison.OrdinalIgnoreCase)
                    ? "raw"
                    : "pipe";

            var payment = _context.Payments
                .Include(p => p.Vehicle)
                .Include(p => p.Movement)
                .Include(p => p.Collector)
                .Include(p => p.ReceiptReference)
                .FirstOrDefault(p => p.Id == paymentId);

            if (payment == null)
            {
                return NotFound(new { status = "error", message = "Payment not found" });
            }

            var movement = payment.Movement?.Name ?? payment.MovementType;
            var paidAt = payment.PaidAt.ToLocalTime().ToString("dd MMM yyyy HH:mm");
            var status = payment.IsReverted ? "REVERTED" : "PAID";
            var lines = new List<string>
            {
                Center("Garowe Vehicle Tax", width),
                Center("System", width),
                new string('-', width),
                KeyValue("Invoice", payment.InvoiceNumber, width),
                KeyValue("Date", paidAt, width),
                KeyValue("Plate", payment.Vehicle?.PlateNumber ?? "-", width),
                KeyValue("Owner", payment.Vehicle?.OwnerName ?? "-", width),
                KeyValue("Move", movement, width),
                KeyValue("Collector", payment.Collector?.Username ?? "-", width),
                KeyValue("Amount", payment.Amount.ToString("0.##"), width),
                KeyValue("Status", status, width),
                KeyValue("Ref", payment.ReceiptReference?.ReferenceNumber ?? "-", width),
                KeyValue("BillNo", payment.TransactionId ?? "-", width),
                new string('-', width),
                Center("Thank you", width)
            };

            var qrPayload = BuildQrPayloadObject(payment, movement, status);
            var qrData = normalizedQrFormat == "raw"
                ? null
                : BuildQrPayloadString(qrPayload, normalizedQrFormat);
            var signatureContent = BuildQrPayloadString(qrPayload, "pipe");
            string? checksum = null;
            string? checksumAlgorithm = null;

            if (includeQr && includeChecksum)
            {
                var signatureSecret = _configuration["Qr:SignatureSecret"];
                if (string.IsNullOrWhiteSpace(signatureSecret))
                {
                    return StatusCode(500, new
                    {
                        status = "error",
                        message = "QR signature secret is not configured. Set Qr:SignatureSecret before requesting includeChecksum=true."
                    });
                }

                checksum = ComputeHmacSha256(signatureContent, signatureSecret);
                checksumAlgorithm = "hmac-sha256";
            }

            return Ok(new
            {
                status = "success",
                receipt = new
                {
                    paymentId = payment.Id,
                    invoiceId = payment.InvoiceNumber,
                    golisBillNo = payment.TransactionId,
                    paperWidth = width,
                    encoding = "ascii",
                    lines,
                    qr = includeQr
                        ? new
                        {
                            data = qrData,
                            format = normalizedQrFormat,
                            payload = normalizedQrFormat == "raw" ? qrPayload : null,
                            model = "qrcode",
                            size = 6,
                            errorLevel = "M",
                            checksum,
                            checksumAlgorithm
                        }
                        : null
                }
            });
        }

        private static string Center(string text, int width)
        {
            var normalized = (text ?? string.Empty).Trim();
            if (normalized.Length >= width)
            {
                return normalized.Substring(0, width);
            }

            var leftPadding = (width - normalized.Length) / 2;
            return new string(' ', leftPadding) + normalized;
        }

        private static string KeyValue(string key, string value, int width)
        {
            var label = (key ?? string.Empty).Trim() + ": ";
            if (label.Length >= width)
            {
                return label.Substring(0, width);
            }

            var available = width - label.Length;
            var normalizedValue = (value ?? string.Empty).Trim();

            if (normalizedValue.Length > available)
            {
                normalizedValue = normalizedValue.Substring(0, available);
            }

            return label + normalizedValue.PadLeft(available);
        }

        private static string BuildQrPayloadString(Dictionary<string, object?> payload, string qrFormat)
        {
            if (qrFormat == "json")
            {
                return JsonSerializer.Serialize(payload);
            }

            return string.Join("|", new[]
            {
                payload["source"]?.ToString() ?? "GVT",
                payload["invoice"]?.ToString() ?? string.Empty,
                payload["plate"]?.ToString() ?? "-",
                payload["movement"]?.ToString() ?? string.Empty,
                payload["amount"]?.ToString() ?? "0",
                payload["status"]?.ToString() ?? string.Empty,
                payload["paidAtUtc"]?.ToString() ?? string.Empty
            });
        }

        private static Dictionary<string, object?> BuildQrPayloadObject(Payment payment, string movement, string status)
        {
            return new Dictionary<string, object?>
            {
                ["source"] = "GVT",
                ["invoice"] = payment.InvoiceNumber,
                ["paymentId"] = payment.Id,
                ["golisBillNo"] = payment.TransactionId,
                ["plate"] = payment.Vehicle?.PlateNumber ?? "-",
                ["owner"] = payment.Vehicle?.OwnerName ?? "-",
                ["movement"] = movement,
                ["amount"] = payment.Amount,
                ["status"] = status,
                ["paidAtUtc"] = payment.PaidAt.ToUniversalTime().ToString("O")
            };
        }

        private static string ComputeHmacSha256(string content, string secret)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secret);
            var contentBytes = Encoding.UTF8.GetBytes(content);

            using var hmac = new HMACSHA256(keyBytes);
            var hash = hmac.ComputeHash(contentBytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }

    // =======================
    // DTO
    // =======================
    public class PaymentDto
    {
        public int VehicleId { get; set; }
        public string Movement { get; set; } = "";
        public decimal Amount { get; set; }
        public string ReferenceNumber { get; set; } = "";
        public int CollectorId { get; set; }

        // When true → user accepted duplicate warning
        public bool Force { get; set; } = false;
    }
}
