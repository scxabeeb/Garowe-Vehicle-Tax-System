using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using System.Linq;

namespace VehicleTax.Web.Pages.Vehicles;

public class CreateModel : PageModel
{
    private readonly AppDbContext _context;

    public CreateModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Vehicle Vehicle { get; set; } = new();

    public SelectList CarTypes { get; set; } = null!;

    public void OnGet()
    {
        LoadCarTypes();
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
        {
            LoadCarTypes();
            return Page();
        }

        Vehicle.PlateNumber = Vehicle.PlateNumber.Trim().ToUpper();
        Vehicle.OwnerName = Vehicle.OwnerName.Trim();
        Vehicle.Mobile = Vehicle.Mobile?.Trim() ?? "";

        // Prevent duplicate plate numbers
        var exists = _context.Vehicles
            .Any(v => v.PlateNumber == Vehicle.PlateNumber);

        if (exists)
        {
            ModelState.AddModelError("Vehicle.PlateNumber", "Plate number is already registered.");
            LoadCarTypes();
            return Page();
        }

        _context.Vehicles.Add(Vehicle);
        _context.SaveChanges();

        return RedirectToPage("/Vehicles/Index");
    }

    private void LoadCarTypes()
    {
        CarTypes = new SelectList(
            _context.CarTypes.ToList(),
            "Id",
            "Name"
        );
    }
}
