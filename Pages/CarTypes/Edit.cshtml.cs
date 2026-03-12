using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.CarTypes;

public class EditModel : PageModel
{
    private readonly AppDbContext _context;

    public EditModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public CarType CarType { get; set; } = default!;

    // Load data
    public IActionResult OnGet(int id)
    {
        var carType = _context.CarTypes.Find(id);

        if (carType == null)
            return NotFound();

        CarType = carType;
        return Page();
    }

    // Save update
    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
            return Page();

        var existing = _context.CarTypes
            .AsNoTracking()
            .FirstOrDefault(c => c.Id == CarType.Id);

        if (existing == null)
            return NotFound();

        // Check if used anywhere (Vehicles table)
        bool isUsed = _context.Vehicles
            .Any(v => v.CarTypeId == CarType.Id);

        if (isUsed || existing.IsLocked)
        {
            ModelState.AddModelError("", "This car type is already in use and cannot be edited.");
            return Page();
        }

        _context.CarTypes.Update(CarType);
        _context.SaveChanges();

        return RedirectToPage("Index");
    }
}
