using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Reports;

public class VehiclesModel : PageModel
{
    private readonly AppDbContext _context;

    public VehiclesModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)]
    public string? PlateNumber { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? OwnerName { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? CarTypeId { get; set; }

    public List<Vehicle> Vehicles { get; set; } = [];
    public SelectList CarTypes { get; set; } = null!;

    public void OnGet()
    {
        CarTypes = new SelectList(_context.CarTypes, "Id", "Name");

        var query = _context.Vehicles
            .Include(v => v.CarType)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(PlateNumber))
            query = query.Where(v => v.PlateNumber.Contains(PlateNumber));

        if (!string.IsNullOrWhiteSpace(OwnerName))
            query = query.Where(v => v.OwnerName.Contains(OwnerName));

        if (CarTypeId.HasValue)
            query = query.Where(v => v.CarTypeId == CarTypeId);

        Vehicles = query.ToList();
    }
}
