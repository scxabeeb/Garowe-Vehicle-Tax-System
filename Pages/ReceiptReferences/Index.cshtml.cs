using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.ReceiptReferences;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    // QUERY INPUTS
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 20;

    // DATA
    public int TotalPages { get; set; }
    public List<ReceiptReference> Receipts { get; set; } = new();

    // SINGLE OnGet
    public async Task OnGetAsync()
    {
        var query = _context.ReceiptReferences.AsNoTracking().AsQueryable();

        // SEARCH
        if (!string.IsNullOrWhiteSpace(Search))
        {
            query = query.Where(r => r.ReferenceNumber.Contains(Search));
        }

        int totalCount = await query.CountAsync();

        if (PageSize > 0)
        {
            TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

            Receipts = await query
                .OrderBy(r => r.IsCancelled)     // Available & Used first, Cancelled last
                .ThenBy(r => r.IsUsed)           // Available before Used
                .ThenBy(r => r.ReferenceNumber)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }
        else
        {
            // show all
            TotalPages = 1;

            Receipts = await query
                .OrderBy(r => r.IsCancelled)
                .ThenBy(r => r.IsUsed)
                .ThenBy(r => r.ReferenceNumber)
                .ToListAsync();
        }
    }
}
