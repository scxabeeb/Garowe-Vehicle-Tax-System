using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Payments;

[Authorize]
public class ReceiptModel : PageModel
{
    private readonly AppDbContext _context;

    public ReceiptModel(AppDbContext context)
    {
        _context = context;
    }

    public Payment? Payment { get; set; }
    public string? ErrorMessage { get; set; }
    [BindProperty(SupportsGet = true)]
    public string? PrintMode { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? VehicleId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? MovementId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Quantity { get; set; } = 1;

    public bool IsPreview => Payment != null && Payment.Id == 0;

    public IActionResult OnGet(int? paymentId)
    {
        if (paymentId.HasValue)
        {
            Payment = _context.Payments
                .Include(p => p.Vehicle)
                .Include(p => p.Movement)
                .Include(p => p.Collector)
                .Include(p => p.ReceiptReference)
                .FirstOrDefault(p => p.Id == paymentId.Value);

            if (Payment == null)
            {
                return NotFound();
            }

            return Page();
        }

        if (!VehicleId.HasValue || !MovementId.HasValue)
        {
            return RedirectToPage("/Payments/Collect");
        }

        var vehicle = _context.Vehicles
            .Include(v => v.CarType)
            .FirstOrDefault(v => v.Id == VehicleId.Value);

        var movement = _context.Movements
            .FirstOrDefault(m => m.Id == MovementId.Value);

        if (vehicle == null || movement == null)
        {
            return RedirectToPage("/Payments/Collect");
        }

        var tax = _context.TaxAmounts
            .FirstOrDefault(t => t.CarTypeId == vehicle.CarTypeId && t.MovementId == movement.Id);

        if (tax == null)
        {
            TempData["ErrorMessage"] = "Tax configuration not found for selected movement.";
            return RedirectToPage("/Payments/Collect");
        }

        if (Quantity < 1)
        {
            Quantity = 1;
        }

        Payment = new Payment
        {
            Id = 0,
            VehicleId = vehicle.Id,
            Vehicle = vehicle,
            MovementId = movement.Id,
            Movement = movement,
            MovementType = movement.Name,
            Amount = tax.Amount * Quantity,
            PaidAt = DateTime.UtcNow,
            IsPaid = false,
            IsReverted = false
        };

        return Page();
    }

    public IActionResult OnPostCollect(int paymentId)
    {
        if (!User.IsInRole("Admin"))
        {
            TempData["ErrorMessage"] = "Only admins can collect invoice payments.";
            return RedirectToPage(new { paymentId, printMode = PrintMode });
        }

        var payment = _context.Payments.FirstOrDefault(p => p.Id == paymentId);
        if (payment == null)
        {
            return NotFound();
        }

        if (payment.IsReverted)
        {
            TempData["ErrorMessage"] = "Reverted invoice cannot be collected.";
            return RedirectToPage(new { paymentId, printMode = PrintMode });
        }

        if (payment.IsPaid)
        {
            TempData["SuccessMessage"] = "Invoice is already collected.";
            return RedirectToPage(new { paymentId, printMode = PrintMode });
        }

        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
        {
            return RedirectToPage("/Account/Login", new { ReturnUrl = $"/Payments/Receipt?paymentId={paymentId}" });
        }

        var adminUser = _context.Users.FirstOrDefault(u => u.Username == username);
        if (adminUser == null)
        {
            TempData["ErrorMessage"] = "Admin user not found.";
            return RedirectToPage(new { paymentId, printMode = PrintMode });
        }

        payment.IsPaid = true;
        payment.PaidAt = DateTime.UtcNow;
        payment.CollectorId = adminUser.Id;

        _context.SaveChanges();

        TempData["SuccessMessage"] = "Invoice collected successfully. Receipt is ready to print.";
        return RedirectToPage(new { paymentId, printMode = PrintMode });
    }

    public IActionResult OnPostCollectPreview(int vehicleId, int movementId, int quantity)
    {
        if (!User.IsInRole("Admin"))
        {
            TempData["ErrorMessage"] = "Only admins can collect invoice payments.";
            return RedirectToPage(new { vehicleId, movementId, quantity, printMode = PrintMode });
        }

        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
        {
            return RedirectToPage("/Account/Login", new { ReturnUrl = $"/Payments/Receipt?vehicleId={vehicleId}&movementId={movementId}&quantity={quantity}" });
        }

        var adminUser = _context.Users.FirstOrDefault(u => u.Username == username);
        if (adminUser == null)
        {
            TempData["ErrorMessage"] = "Admin user not found.";
            return RedirectToPage(new { vehicleId, movementId, quantity, printMode = PrintMode });
        }

        var vehicle = _context.Vehicles
            .Include(v => v.CarType)
            .FirstOrDefault(v => v.Id == vehicleId);

        var movement = _context.Movements.FirstOrDefault(m => m.Id == movementId);
        if (vehicle == null || movement == null)
        {
            TempData["ErrorMessage"] = "Invalid vehicle or movement.";
            return RedirectToPage("/Payments/Collect");
        }

        var tax = _context.TaxAmounts
            .FirstOrDefault(t => t.CarTypeId == vehicle.CarTypeId && t.MovementId == movement.Id);
        if (tax == null)
        {
            TempData["ErrorMessage"] = "Tax configuration not found.";
            return RedirectToPage("/Payments/Collect");
        }

        var safeQty = quantity < 1 ? 1 : quantity;

        var payment = new Payment
        {
            VehicleId = vehicle.Id,
            MovementId = movement.Id,
            MovementType = movement.Name,
            Amount = tax.Amount * safeQty,
            PaidAt = DateTime.UtcNow,
            CollectorId = adminUser.Id,
            IsPaid = true,
            IsReverted = false
        };

        _context.Payments.Add(payment);
        _context.SaveChanges();

        TempData["SuccessMessage"] = "Payment collected successfully. Receipt is ready to print.";
        return RedirectToPage(new { paymentId = payment.Id, printMode = PrintMode });
    }
}
