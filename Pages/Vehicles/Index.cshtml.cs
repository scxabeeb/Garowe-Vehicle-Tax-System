using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Vehicles;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public List<Vehicle> Vehicles { get; set; } = new();

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public void OnGet()
    {
        Vehicles = _context.Vehicles
            .Include(v => v.CarType)
            .OrderBy(v => v.PlateNumber)
            .ToList();
    }
}
