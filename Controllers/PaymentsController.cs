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

            // 🔍 Get collector user so we can store the NAME, not just the ID
            var collector = _context.Users.FirstOrDefault(u => u.Id == dto.CollectorId);
            if (collector == null)
                return BadRequest(new { status = "error", message = "Collector not found" });

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

            _context.Payments.Add(payment);

            // 🔴 Save complete receipt info for reporting & Flutter
            reference.IsUsed = true;
            reference.UsedAt = DateTime.UtcNow;
            reference.VehicleId = dto.VehicleId;
            reference.UsedBy = collector.Username;   // 👈 human name, not ID

            _context.SaveChanges();

            return Ok(new
            {
                status = "success",
                message = "Payment saved successfully",
                collector = collector.Username   // Flutter can display directly
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
                        ? p.ReceiptReference.UsedBy   // already the name
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

        // CollectorId stays INT (machine identity)
        public int CollectorId { get; set; }
    }
}
