using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Movements;

public class DeleteModel : PageModel
{
    private readonly AppDbContext _context;

    public DeleteModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Movement Movement { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet(int id)
    {
        Movement = _context.Movements.Find(id)!;

        if (Movement == null)
            return NotFound();

        return Page();
    }

    public IActionResult OnPost()
    {
        var movement = _context.Movements.Find(Movement.Id);

        if (movement == null)
            return RedirectToPage("Index");

        // ❌ BLOCK DELETE IF USED IN TAX AMOUNTS
        bool usedInTaxAmounts = _context.TaxAmounts
            .Any(t => t.MovementId == movement.Id);

        if (usedInTaxAmounts)
        {
            ErrorMessage = "This movement cannot be deleted because it is used in tax amounts.";
            Movement = movement;
            return Page();
        }

        _context.Movements.Remove(movement);
        _context.SaveChanges();

        TempData["Success"] = "Movement deleted successfully.";
        return RedirectToPage("Index");
    }
}
