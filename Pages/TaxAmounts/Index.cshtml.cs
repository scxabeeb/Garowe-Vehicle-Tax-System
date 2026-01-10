using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.TaxAmounts;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    // 🔍 Search
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    // 📄 Pagination
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 10; // 10 / 50 / 100 / 0(All)

    public int TotalPages { get; set; }

    public List<TaxAmount> Taxes { get; set; } = new();

    // ✅ SINGLE OnGet
    public void OnGet()
    {
        IQueryable<TaxAmount> query = _context.TaxAmounts
            .Include(t => t.CarType)
            .Include(t => t.Movement);

        // 🔍 Apply search
        if (!string.IsNullOrWhiteSpace(Search))
        {
            PageNumber = 1;
            query = query.Where(t =>
                t.CarType!.Name.Contains(Search) ||
                t.Movement!.Name.Contains(Search));
        }

        int totalCount = query.Count();

        // 🧮 Pagination logic
        if (PageSize > 0)
        {
            TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

            // 🌿 Never allow 0 pages
            if (TotalPages < 1)
                TotalPages = 1;

            // 🔒 Clamp page number
            if (PageNumber < 1)
                PageNumber = 1;

            if (PageNumber > TotalPages)
                PageNumber = TotalPages;

            // 🛡️ Guard Skip to avoid negative OFFSET
            var skip = Math.Max(0, (PageNumber - 1) * PageSize);

            Taxes = query
                .OrderBy(t => t.CarType!.Name)
                .Skip(skip)
                .Take(PageSize)
                .ToList();
        }
        else
        {
            // 📄 All records
            PageNumber = 1;
            TotalPages = 1;

            Taxes = query
                .OrderBy(t => t.CarType!.Name)
                .ToList();
        }
    }
}
