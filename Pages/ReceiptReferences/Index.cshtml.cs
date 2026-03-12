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

<<<<<<< HEAD
    // ================= QUERY INPUTS =================

=======
    // QUERY INPUTS
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

<<<<<<< HEAD
    // 0 = Show All
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 20;

    // ================= DATA OUTPUT =================

    public int TotalPages { get; set; }

    public List<ReceiptReference> Receipts { get; set; } = new();

    // ================= MAIN GET =================

    public async Task OnGetAsync()
    {
        // Safety for negative page
        if (PageNumber < 1)
            PageNumber = 1;

        var query = _context.ReceiptReferences
                            .AsNoTracking()
                            .AsQueryable();

        // SEARCH FILTER
=======
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
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
        if (!string.IsNullOrWhiteSpace(Search))
        {
            query = query.Where(r => r.ReferenceNumber.Contains(Search));
        }

        int totalCount = await query.CountAsync();

<<<<<<< HEAD
        // ================= PAGINATION LOGIC =================
=======
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
        if (PageSize > 0)
        {
            TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

<<<<<<< HEAD
            // Safety if user enters page bigger than total
            if (TotalPages == 0)
                TotalPages = 1;

            if (PageNumber > TotalPages)
                PageNumber = TotalPages;

            Receipts = await query
                .OrderBy(r => r.IsCancelled)      // Cancelled last
                .ThenBy(r => r.IsUsed)           // Available first
=======
            Receipts = await query
                .OrderBy(r => r.IsCancelled)     // Available & Used first, Cancelled last
                .ThenBy(r => r.IsUsed)           // Available before Used
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
                .ThenBy(r => r.ReferenceNumber)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }
        else
        {
<<<<<<< HEAD
            // SHOW ALL MODE
=======
            // show all
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
            TotalPages = 1;

            Receipts = await query
                .OrderBy(r => r.IsCancelled)
                .ThenBy(r => r.IsUsed)
                .ThenBy(r => r.ReferenceNumber)
                .ToListAsync();
        }
    }
}
