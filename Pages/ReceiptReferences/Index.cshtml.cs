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
    public void OnGet()
    {
        var query = _context.ReceiptReferences.AsQueryable();

        // SEARCH
        if (!string.IsNullOrWhiteSpace(Search))
        {
            query = query.Where(r =>
                r.ReferenceNumber.Contains(Search));
        }

        int totalCount = query.Count();

        if (PageSize > 0)
        {
            TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

            Receipts = query
                .OrderBy(r => r.ReferenceNumber)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();
        }
        else
        {
            // show all
            TotalPages = 1;

            Receipts = query
                .OrderBy(r => r.ReferenceNumber)
                .ToList();
        }
    }
}
