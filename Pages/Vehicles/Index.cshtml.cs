using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Vehicles;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public List<Vehicle> Vehicles { get; set; } = new();

    // Pagination
    public int PageIndex { get; set; } = 1;
    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 10; // default 10

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public async Task OnGetAsync(int pageIndex = 1, int pageSize = 10)
    {
        PageIndex = pageIndex;
        PageSize = pageSize;

        var totalRecords = await _context.Vehicles.CountAsync();

        if (PageSize == -1) // -1 means ALL
        {
            TotalPages = 1;
            Vehicles = await _context.Vehicles
                .Include(v => v.CarType)
                .OrderBy(v => v.PlateNumber)
                .ToListAsync();
        }
        else
        {
            TotalPages = (int)Math.Ceiling(totalRecords / (double)PageSize);

            Vehicles = await _context.Vehicles
                .Include(v => v.CarType)
                .OrderBy(v => v.PlateNumber)
                .Skip((PageIndex - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }
    }
}
