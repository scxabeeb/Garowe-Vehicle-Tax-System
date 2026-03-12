using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;   // ✅ REQUIRED FOR Include
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.TaxAmounts;

public class DeleteModel : PageModel
{
    private readonly AppDbContext _context;

    public DeleteModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public TaxAmount Tax { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet(int id)
    {
        Tax = _context.TaxAmounts
            .Include(t => t.CarType)
            .Include(t => t.Movement)
            .FirstOrDefault(t => t.Id == id)!;

        if (Tax == null)
            return NotFound();

        return Page();
    }

    public IActionResult OnPost()
    {
        var tax = _context.TaxAmounts.Find(Tax.Id);

        if (tax == null)
            return RedirectToPage("Index");

        // 🔒 Block delete if used in payments
        bool used = _context.Payments
            .Any(p => p.MovementId == tax.MovementId);

        if (used)
        {
            ErrorMessage = "This tax amount cannot be deleted because it is used in payments.";
            Tax = tax;
            return Page();
        }

        _context.TaxAmounts.Remove(tax);
        _context.SaveChanges();

        TempData["Success"] = "Tax amount deleted successfully.";
        return RedirectToPage("Index");
    }
}
