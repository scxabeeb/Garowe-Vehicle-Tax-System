using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.CarTypes;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    // 🔍 Search (GET)
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    // 📄 Pagination
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 10;
    public int TotalPages { get; set; }

    // 📋 Data
    public List<CarType> CarTypes { get; set; } = new();

    public void OnGet()
    {
        IQueryable<CarType> query = _context.CarTypes;

        if (!string.IsNullOrWhiteSpace(Search))
        {
            query = query.Where(c =>
                EF.Functions.Like(c.Name, $"%{Search}%"));
        }

        int totalCount = query.Count();
        TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

        CarTypes = query
            .OrderBy(c => c.Name)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }
}
