using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.CarTypes;

public class DeleteModel : PageModel
{
    private readonly AppDbContext _context;

    public DeleteModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public CarType CarType { get; set; } = default!;

    // Show confirmation
    public IActionResult OnGet(int id)
    {
        var carType = _context.CarTypes.Find(id);

        if (carType == null)
            return NotFound();

        CarType = carType;
        return Page();
    }

    // Confirm delete
    public IActionResult OnPost()
    {
        var carType = _context.CarTypes.Find(CarType.Id);

        if (carType == null)
            return NotFound();

        // Check if used in Vehicles
        bool isUsed = _context.Vehicles
            .Any(v => v.CarTypeId == carType.Id);

        if (isUsed || carType.IsLocked)
        {
            ModelState.AddModelError(
                "",
                "This car type is already in use and cannot be deleted."
            );

            CarType = carType;
            return Page();
        }

        _context.CarTypes.Remove(carType);
        _context.SaveChanges();

        return RedirectToPage("Index");
    }
}
