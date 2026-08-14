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
        // COLLECT PAYMENT (Flutter-friendly)
        // =======================
        [HttpPost("collect")]
        public IActionResult Collect([FromBody] PaymentCollectDto dto)
        {
            if (dto == null)
                return BadRequest(new { status = "error", message = "Invalid request data" });

            var vehicle = ResolveVehicle(dto.VehicleId, dto.PlateNumber);
            if (vehicle == null)
                return BadRequest(new { status = "error", message = "Vehicle not found" });

            var movement = ResolveMovement(dto.MovementId, dto.Movement, dto.MovementName);
            if (movement == null)
                return BadRequest(new { status = "error", message = "Invalid movement" });

            var collector = dto.CollectorId.HasValue
                ? _context.Users.FirstOrDefault(u => u.Id == dto.CollectorId.Value)
                : null;

            if (dto.CollectorId.HasValue && collector == null)
                return BadRequest(new { status = "error", message = "Collector not found" });

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

            var quantity = dto.Quantity < 1 ? 1 : dto.Quantity;
            var paidAt = dto.PaidAt ?? DateTime.UtcNow;
            var amount = ResolveAmount(dto, vehicle, movement, quantity);

            var payment = new Payment
            {
                VehicleId = vehicle.Id,
                MovementId = movement.Id,
                MovementType = movement.Name,
                Amount = amount,
                PaidAt = paidAt,
                ReceiptReferenceId = reference?.Id,
                CollectorId = collector?.Id,
                // Snapshot the checkpoint at payment time so historical
                // payments stay attributed to the original checkpoint even
                // if the collector is later reassigned.
                CheckpointId = dto.CheckpointId.HasValue
                    ? dto.CheckpointId.Value
                    : collector?.CheckpointId,
                IsPaid = true,
                IsReverted = false,
                PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? null : dto.PaymentMethod,
                PaidBy = string.IsNullOrWhiteSpace(dto.PaidBy) ? null : dto.PaidBy,
                Remarks = string.IsNullOrWhiteSpace(dto.Remarks) ? null : dto.Remarks
            };

            _context.Payments.Add(payment);

            if (reference != null)
            {
                reference.IsUsed = true;
                reference.UsedAt = paidAt;
                reference.UsedBy = collector?.Username;
                reference.VehicleId = vehicle.Id;
            }

            _context.SaveChanges();

            return Ok(new
            {
                status = "success",
                message = "Payment recorded successfully",
                collector = collector?.Username,
                paymentId = payment.Id,
                invoiceId = payment.InvoiceNumber,
                golisBillNo = payment.TransactionId
            });
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
                // Snapshot the checkpoint at payment time so historical
                // payments stay attributed to the original checkpoint even
                // if the collector is later reassigned.
                CheckpointId = collector.CheckpointId,
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
        // CREATE INVOICE PREVIEW
        // =======================
        [HttpPost("preview")]
        public IActionResult CreatePreview([FromBody] PaymentPreviewDto dto)
        {
            if (dto == null)
                return BadRequest(new { status = "error", message = "Invalid request data" });

            var invoiceCollector = dto.CollectorId.HasValue
                ? _context.Users.FirstOrDefault(u => u.Id == dto.CollectorId.Value)
                : null;

            if (dto.CollectorId.HasValue && invoiceCollector == null)
                return BadRequest(new { status = "error", message = "Collector not found" });

            var vehicle = _context.Vehicles
                .Include(v => v.CarType)
                .FirstOrDefault(v => v.Id == dto.VehicleId);

            if (vehicle == null)
                return BadRequest(new { status = "error", message = "Vehicle not found" });

            var movement = _context.Movements
                .FirstOrDefault(m => m.Id == dto.MovementId);

            if (movement == null)
                return BadRequest(new { status = "error", message = "Invalid movement" });

            var tax = _context.TaxAmounts
                .FirstOrDefault(t => t.CarTypeId == vehicle.CarTypeId && t.MovementId == movement.Id);

            if (tax == null)
                return BadRequest(new { status = "error", message = "Tax configuration not found" });

            var quantity = dto.Quantity < 1 ? 1 : dto.Quantity;

            var payment = new Payment
            {
                VehicleId = vehicle.Id,
                MovementId = movement.Id,
                MovementType = movement.Name,
                Amount = tax.Amount * quantity,
                PaidAt = DateTime.UtcNow,
                IsPaid = false,
                IsReverted = false,
                CollectorId = invoiceCollector?.Id,
                // Snapshot the checkpoint at payment time so historical
                // payments stay attributed to the original checkpoint even
                // if the collector is later reassigned.
                CheckpointId = dto.CheckpointId.HasValue
                    ? dto.CheckpointId.Value
                    : invoiceCollector?.CheckpointId,
                ReceiptReferenceId = null
            };

            _context.Payments.Add(payment);
            _context.SaveChanges();

            payment = _context.Payments
                .Include(p => p.Vehicle)
                .Include(p => p.Movement)
                .Include(p => p.Collector)
                .Include(p => p.ReceiptReference)
                .First(p => p.Id == payment.Id);

            return Ok(new
            {
                status = "success",
                message = "Invoice preview created.",
                receipt = BuildReceiptPayload(payment)
            });
        }

        // =======================
        // COLLECT PREVIEW INVOICE
        // =======================
        [HttpPost("{paymentId}/collect")]
        public IActionResult CollectPreviewInvoice(int paymentId, [FromBody] CollectInvoiceDto dto)
        {
            if (dto == null)
                return BadRequest(new { status = "error", message = "Invalid request data" });

            var payment = _context.Payments
                .Include(p => p.Vehicle)
                .Include(p => p.Movement)
                .Include(p => p.Collector)
                .Include(p => p.ReceiptReference)
                .FirstOrDefault(p => p.Id == paymentId);

            if (payment == null)
                return NotFound(new { status = "error", message = "Payment not found" });

            if (payment.IsReverted)
                return BadRequest(new { status = "error", message = "Reverted invoice cannot be collected" });

            if (payment.IsPaid)
            {
                return Ok(new
                {
                    status = "success",
                    message = "Invoice already collected.",
                    paymentId = payment.Id,
                    invoiceId = payment.InvoiceNumber,
                    golisBillNo = payment.TransactionId,
                    receipt = BuildReceiptPayload(payment)
                });
            }

            var collector = _context.Users.FirstOrDefault(u => u.Id == dto.CollectorId);
            if (collector == null)
                return BadRequest(new { status = "error", message = "Collector not found" });

            ReceiptReference? reference = null;
            if (!string.IsNullOrWhiteSpace(dto.ReferenceNumber))
            {
                reference = _context.ReceiptReferences
                    .FirstOrDefault(r => r.ReferenceNumber == dto.ReferenceNumber);

                if (reference == null)
                    return BadRequest(new { status = "error", message = "Invalid receipt reference" });

                if (reference.IsUsed && reference.Id != payment.ReceiptReferenceId)
                    return BadRequest(new { status = "error", message = "Receipt reference already used" });
            }

            var paidAt = dto.PaidAt ?? DateTime.UtcNow;

            payment.IsPaid = true;
            payment.PaidAt = paidAt;
            payment.CollectorId = collector.Id;
            // Snapshot the checkpoint at payment time so historical
            // payments stay attributed to the original checkpoint even
            // if the collector is later reassigned.
            payment.CheckpointId = dto.CheckpointId.HasValue
                ? dto.CheckpointId.Value
                : collector.CheckpointId;
            payment.PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? payment.PaymentMethod : dto.PaymentMethod;
            payment.PaidBy = string.IsNullOrWhiteSpace(dto.PaidBy) ? payment.PaidBy : dto.PaidBy;
            payment.Remarks = string.IsNullOrWhiteSpace(dto.Remarks) ? payment.Remarks : dto.Remarks;

            if (reference != null)
            {
                payment.ReceiptReferenceId = reference.Id;
                reference.IsUsed = true;
                reference.UsedAt = paidAt;
                reference.UsedBy = collector.Username;
                reference.VehicleId = payment.VehicleId;
            }

            _context.SaveChanges();

            payment = _context.Payments
                .Include(p => p.Vehicle)
                .Include(p => p.Movement)
                .Include(p => p.Collector)
                .Include(p => p.ReceiptReference)
                .First(p => p.Id == payment.Id);

            return Ok(new
            {
                status = "success",
                message = "Invoice collected successfully.",
                paymentId = payment.Id,
                invoiceId = payment.InvoiceNumber,
                golisBillNo = payment.TransactionId,
                receipt = BuildReceiptPayload(payment)
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
        // GET PAYMENTS BY CHECKPOINT
        // =======================
        // Uses the Payment.CheckpointId snapshot so that payments remain
        // attributed to the original checkpoint even after the collector
        // is reassigned to a different checkpoint.
        // =======================
        [HttpGet("checkpoint/{checkpointId}")]
        public IActionResult GetByCheckpoint(int checkpointId, [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
        {
            var checkpoint = _context.Checkpoints.FirstOrDefault(c => c.Id == checkpointId);
            if (checkpoint == null)
            {
                return NotFound(new { status = "error", message = "Checkpoint not found" });
            }

            var query = _context.Payments
                .Include(p => p.Vehicle)
                .Include(p => p.Collector)
                    .ThenInclude(c => c!.Checkpoint)
                .Include(p => p.Checkpoint)
                .Where(p => p.IsPaid && !p.IsReverted &&
                    (p.CheckpointId == checkpointId ||
                     (p.CheckpointId == null && p.Collector != null && p.Collector.CheckpointId == checkpointId)))
                .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(p => p.PaidAt >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                query = query.Where(p => p.PaidAt < toDate.Value.Date.AddDays(1));
            }

            var items = query
                .OrderByDescending(p => p.PaidAt)
                .Select(p => new
                {
                    p.Id,
                    invoiceId = p.InvoiceNumber,
                    p.Amount,
                    p.PaidAt,
                    p.MovementType,
                    plate = p.Vehicle != null ? p.Vehicle.PlateNumber : null,
                    owner = p.Vehicle != null ? p.Vehicle.OwnerName : null,
                    collector = p.Collector != null ? p.Collector.Username : "Unassigned",
                    checkpointName = p.Checkpoint != null ? p.Checkpoint.Name : "Unassigned"
                })
                .ToList();

            return Ok(new
            {
                status = "success",
                checkpoint = checkpoint.Name,
                checkpointId,
                totalPayments = items.Count,
                totalAmount = items.Sum(x => x.Amount),
                items
            });
        }

        // =======================
        // CHECKPOINT COLLECTION RANKING
        // =======================
        // Groups by the Payment.CheckpointId snapshot so that each
        // payment is attributed to the checkpoint it was collected under.
        // =======================
        [HttpGet("checkpoint-summary")]
        public IActionResult GetCheckpointSummary([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
        {
            var query = _context.Payments
                .Include(p => p.Checkpoint)
                .Where(p => p.IsPaid && !p.IsReverted)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(p => p.PaidAt >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                query = query.Where(p => p.PaidAt < toDate.Value.Date.AddDays(1));
            }

            var items = query
                .GroupBy(p => p.Checkpoint != null
                    ? p.Checkpoint.Name
                    : "Unassigned")
                .Select(g => new
                {
                    checkpoint = g.Key,
                    totalPayments = g.Count(),
                    totalAmount = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.totalAmount)
                .ToList();

            return Ok(new
            {
                status = "success",
                totalCheckpoints = items.Count,
                grandTotalPayments = items.Sum(x => x.totalPayments),
                grandTotalAmount = items.Sum(x => x.totalAmount),
                items
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

        private object BuildReceiptPayload(Payment payment)
        {
            return new
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
                isPaid = payment.IsPaid,
                isReverted = payment.IsReverted,
                referenceNumber = payment.ReceiptReference?.ReferenceNumber,
                appName = "Garowe Vehicle Tax System"
            };
        }

        private Vehicle? ResolveVehicle(int? vehicleId, string? plateNumber)
        {
            if (vehicleId.HasValue)
            {
                return _context.Vehicles.FirstOrDefault(v => v.Id == vehicleId.Value);
            }

            if (!string.IsNullOrWhiteSpace(plateNumber))
            {
                var normalizedPlate = plateNumber.Trim();
                return _context.Vehicles.FirstOrDefault(v => v.PlateNumber == normalizedPlate);
            }

            return null;
        }

        private Movement? ResolveMovement(int? movementId, string? movement, string? movementName)
        {
            if (movementId.HasValue)
            {
                return _context.Movements.FirstOrDefault(m => m.Id == movementId.Value);
            }

            var candidate = movementName ?? movement;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return null;
            }

            return _context.Movements.FirstOrDefault(m => m.Name == candidate);
        }

        private decimal ResolveAmount(PaymentCollectDto dto, Vehicle vehicle, Movement movement, int quantity)
        {
            if (dto.Amount.HasValue)
            {
                return dto.Amount.Value;
            }

            if (dto.UnitAmount.HasValue && dto.UnitAmount.Value > 0)
            {
                return dto.UnitAmount.Value * quantity;
            }

            var tax = _context.TaxAmounts
                .FirstOrDefault(t => t.CarTypeId == vehicle.CarTypeId && t.MovementId == movement.Id);

            return tax?.Amount ?? 0m;
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

    public class PaymentPreviewDto
    {
        public int VehicleId { get; set; }
        public int MovementId { get; set; }
        public int Quantity { get; set; } = 1;
        public int? CollectorId { get; set; }
        public int? CheckpointId { get; set; }
    }

    public class CollectInvoiceDto
    {
        public int CollectorId { get; set; }
        public int? CheckpointId { get; set; }
        public string? ReferenceNumber { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PaidBy { get; set; }
        public string? Remarks { get; set; }
    }

    public class PaymentCollectDto
    {
        public int? VehicleId { get; set; }
        public int? MovementId { get; set; }
        public string? Movement { get; set; }
        public string? MovementName { get; set; }
        public string? PlateNumber { get; set; }
        public decimal? Amount { get; set; }
        public decimal? UnitAmount { get; set; }
        public int Quantity { get; set; } = 1;
        public string? ReferenceNumber { get; set; }
        public int? CollectorId { get; set; }
        public int? CheckpointId { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PaidBy { get; set; }
        public string? Remarks { get; set; }
        public DateTime? PaidAt { get; set; }
    }
}
