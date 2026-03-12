using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Vehicles;

public class DeleteModel : PageModel
{
    private readonly AppDbContext _context;

    public DeleteModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Vehicle Vehicle { get; set; } = null!;

    public IActionResult OnGet(int id)
    {
        Vehicle = _context.Vehicles
            .Include(v => v.CarType)
            .FirstOrDefault(v => v.Id == id)!;

        if (Vehicle == null)
            return NotFound();

        return Page();
    }

    public IActionResult OnPost(int id)
    {
        var vehicle = _context.Vehicles
            .FirstOrDefault(v => v.Id == id);

        if (vehicle == null)
            return NotFound();

        // Block deletion if payments exist
        bool hasPayments = _context.Payments
            .Any(p => p.VehicleId == id);

        if (hasPayments)
        {
            TempData["ErrorMessage"] =
                "This vehicle has payments. Deletion is not allowed.";

            return RedirectToPage("Delete", new { id });
        }

        _context.Vehicles.Remove(vehicle);
        _context.SaveChanges();

        TempData["SuccessMessage"] = "Vehicle deleted successfully";
        return RedirectToPage("Index");
    }
}
