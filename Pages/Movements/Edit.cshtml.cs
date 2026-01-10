using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Movements;

public class EditModel : PageModel
{
    private readonly AppDbContext _context;

    public EditModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Movement Movement { get; set; } = new();

    public IActionResult OnGet(int id)
    {
        Movement = _context.Movements
            .FirstOrDefault(m => m.Id == id)!;

        if (Movement == null)
            return NotFound();

        return Page();
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
            return Page();

        // ❌ Prevent duplicate name (excluding current record)
        bool exists = _context.Movements.Any(m =>
            m.Name.ToLower() == Movement.Name.ToLower() &&
            m.Id != Movement.Id);

        if (exists)
        {
            ModelState.AddModelError(
                "Movement.Name",
                "This movement name already exists."
            );
            return Page();
        }

        _context.Update(Movement);
        _context.SaveChanges();

        // ✅ Inline success alert
        TempData["Success"] = "Movement updated successfully.";
        return RedirectToPage("Index");
    }
}
