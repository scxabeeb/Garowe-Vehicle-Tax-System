using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Movements;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    // Search
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    // Sorting: asc / desc
    [BindProperty(SupportsGet = true)]
    public string Sort { get; set; } = "asc";

    // Pagination
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 10;
    public int TotalPages { get; set; }

    public List<Movement> Movements { get; set; } = new();

    public void OnGet()
    {
        IQueryable<Movement> query = _context.Movements;

        if (!string.IsNullOrWhiteSpace(Search))
        {
            query = query.Where(m =>
                EF.Functions.Like(m.Name, $"%{Search}%"));
        }

        query = Sort == "desc"
            ? query.OrderByDescending(m => m.Name)
            : query.OrderBy(m => m.Name);

        int totalCount = query.Count();
        TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

        Movements = query
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }
}
