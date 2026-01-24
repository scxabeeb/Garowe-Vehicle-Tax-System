using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PaymentsController(AppDbContext context)
        {
            _context = context;
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

            var reference = _context.ReceiptReferences
                .FirstOrDefault(r => r.ReferenceNumber == dto.ReferenceNumber);

            if (reference == null)
                return BadRequest(new { status = "error", message = "Invalid receipt reference" });

            if (reference.IsUsed)
                return BadRequest(new { status = "error", message = "Receipt reference already used" });

            var movement = _context.Movements
                .FirstOrDefault(m => m.Name == dto.Movement);

            if (movement == null)
                return BadRequest(new { status = "error", message = "Invalid movement" });

            var collector = _context.Users.FirstOrDefault(u => u.Id == dto.CollectorId);
            if (collector == null)
                return BadRequest(new { status = "error", message = "Collector not found" });

            // ==================================================
            // 🔐 DUPLICATE PAYMENT CHECK
            // Same Vehicle + Same Movement + Same Amount
            // ==================================================
            var now = DateTime.UtcNow;

            var lastPayment = _context.Payments
                .Where(p =>
                    p.VehicleId == dto.VehicleId &&
                    p.MovementId == movement.Id &&
                    p.Amount == dto.Amount &&
                    !p.IsReverted)
                .OrderByDescending(p => p.PaidAt)
                .FirstOrDefault();

            if (lastPayment != null)
            {
                var diffMinutes = (now - lastPayment.PaidAt).TotalMinutes;

                // 🚫 Block if less than 10 minutes
                if (diffMinutes < 10)
                {
                    return BadRequest(new
                    {
                        status = "duplicate",
                        type = "block",
                        message = "Duplicate payment detected within 10 minutes. Please wait before trying again."
                    });
                }

                // ⚠️ Warning if more than 10 minutes
                return Ok(new
                {
                    status = "duplicate",
                    type = "warning",
                    message = "A similar payment was made before. Do you want to continue?",
                    lastPaymentAt = lastPayment.PaidAt
                });
            }

            // ==================================================
            // SAVE PAYMENT
            // ==================================================
            var payment = new Payment
            {
                VehicleId = dto.VehicleId,
                MovementId = movement.Id,
                MovementType = dto.Movement,
                Amount = dto.Amount,
                PaidAt = now,
                ReceiptReferenceId = reference.Id,
                CollectorId = dto.CollectorId,
                IsReverted = false
            };

            _context.Payments.Add(payment);

            // Update receipt reference
            reference.IsUsed = true;
            reference.UsedAt = now;
            reference.VehicleId = dto.VehicleId;
            reference.UsedBy = collector.Username;

            _context.SaveChanges();

            return Ok(new
            {
                status = "success",
                message = "Payment saved successfully",
                collector = collector.Username
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
                    p.Amount,
                    p.PaidAt,
                    p.MovementType,
                    p.IsReverted,
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
    }
}
