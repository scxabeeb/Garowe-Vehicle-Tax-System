using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Vehicles;

public class EditModel : PageModel
{
    private readonly AppDbContext _context;

    public EditModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Vehicle Vehicle { get; set; } = null!;

    public SelectList CarTypes { get; set; } = null!;

    public bool HasPayments { get; set; }

    public IActionResult OnGet(int id)
    {
        Vehicle = _context.Vehicles
            .AsNoTracking()
            .FirstOrDefault(v => v.Id == id)!;

        if (Vehicle == null)
            return NotFound();

        HasPayments = _context.Payments
            .Any(p => p.VehicleId == id);

        LoadCarTypes();
        return Page();
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
        {
            LoadCarTypes();
            return Page();
        }

        var existingVehicle = _context.Vehicles
            .AsNoTracking()
            .FirstOrDefault(v => v.Id == Vehicle.Id);

        if (existingVehicle == null)
            return NotFound();

        bool hasPayments = _context.Payments
            .Any(p => p.VehicleId == Vehicle.Id);

        bool plateChanged = !string.Equals(
            existingVehicle.PlateNumber,
            Vehicle.PlateNumber,
            StringComparison.OrdinalIgnoreCase
        );

        if (hasPayments && plateChanged)
        {
            ModelState.AddModelError(
                "Vehicle.PlateNumber",
                "You cannot change the plate number because this vehicle already has payments."
            );

            HasPayments = true;
            LoadCarTypes();
            return Page();
        }

        _context.Attach(Vehicle).State = EntityState.Modified;
        _context.SaveChanges();

        TempData["SuccessMessage"] = "Vehicle updated successfully";
        return RedirectToPage("Index");
    }

    private void LoadCarTypes()
    {
        CarTypes = new SelectList(
            _context.CarTypes.AsNoTracking(),
            "Id",
            "Name"
        );
    }
}
