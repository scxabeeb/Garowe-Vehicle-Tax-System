using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Movements;

public class CreateModel : PageModel
{
    private readonly AppDbContext _context;

    public CreateModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Movement Movement { get; set; } = new();

    public IActionResult OnGet()
    {
        return Page();
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
            return Page();

        // ❌ Prevent duplicate movement name
        bool exists = _context.Movements
            .Any(m => m.Name.ToLower() == Movement.Name.ToLower());

        if (exists)
        {
            ModelState.AddModelError(
                "Movement.Name",
                "This movement already exists."
            );
            return Page();
        }

        _context.Movements.Add(Movement);
        _context.SaveChanges();

        // ✅ Inline success alert
        TempData["Success"] = "Movement created successfully.";
        return RedirectToPage("Index");
    }
}
