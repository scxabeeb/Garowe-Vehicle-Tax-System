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

    public IActionResult OnGet(int? paymentId)
    {
        if (paymentId == null)
        {
            return RedirectToPage("/Payments/Collect");
        }

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
}
