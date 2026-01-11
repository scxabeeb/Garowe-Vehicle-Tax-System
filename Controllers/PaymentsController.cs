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
            var vehicle = _context.Vehicles.Find(dto.VehicleId);
            if (vehicle == null)
                return BadRequest(new { status = "error", message = "Vehicle not found" });

            // 🔴 Ignore reverted payments in duplicate check
            var lastPayment = _context.Payments
                .Where(p => p.VehicleId == dto.VehicleId && !p.IsReverted)
                .OrderByDescending(p => p.PaidAt)
                .FirstOrDefault();

            if (lastPayment != null &&
                (DateTime.UtcNow - lastPayment.PaidAt).TotalMinutes < 10)
            {
                return BadRequest(new
                {
                    status = "duplicate",
                    message = "Payment already made in the last 10 minutes."
                });
            }

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

            var payment = new Payment
            {
                VehicleId = dto.VehicleId,
                MovementId = movement.Id,
                MovementType = dto.Movement,
                Amount = dto.Amount,
                PaidAt = DateTime.UtcNow,
                ReceiptReferenceId = reference.Id,
                CollectorId = dto.CollectorId,
                IsReverted = false
            };

            // lock receipt
            reference.IsUsed = true;
            reference.UsedAt = DateTime.UtcNow;

            _context.Payments.Add(payment);
            _context.SaveChanges();

            return Ok(new
            {
                status = "success",
                message = "Payment saved successfully"
            });
        }

        // =======================
        // GET PAYMENTS BY COLLECTOR (ONLY VALID PAYMENTS)
        // =======================
        [HttpGet("collector/{collectorId}")]
        public IActionResult GetByCollector(int collectorId)
        {
            var payments = _context.Payments
                .Include(p => p.Vehicle)
                .Include(p => p.ReceiptReference)
                .Where(p => p.CollectorId == collectorId && !p.IsReverted)   // 🔴 DO NOT COUNT REVERTED
                .OrderByDescending(p => p.PaidAt)
                .Select(p => new
                {
                    p.Id,
                    p.Amount,
                    p.PaidAt,
                    p.MovementType,
                    p.IsReverted,    // 🔴 send flag to Flutter
                    plate = p.Vehicle!.PlateNumber,
                    owner = p.Vehicle.OwnerName,
                    receipt = p.ReceiptReference != null
                        ? p.ReceiptReference.ReferenceNumber
                        : null
                })
                .ToList();

            return Ok(new
            {
                status = "success",
                message = "Payments loaded",
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
