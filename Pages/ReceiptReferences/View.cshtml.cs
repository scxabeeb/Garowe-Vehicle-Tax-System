using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.ReceiptReferences;

public class ViewModel : PageModel
{
    private readonly AppDbContext _context;

    public ViewModel(AppDbContext context)
    {
        _context = context;
    }

    public ReceiptReference? Item { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Item = await _context.ReceiptReferences
            .Include(r => r.Vehicle)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (Item == null)
            return NotFound();

        return Page();
    }

    // Cancel only when AVAILABLE
    public async Task<IActionResult> OnPostCancelAsync(int id, string reason)
    {
        var item = await _context.ReceiptReferences.FindAsync(id);
        if (item == null)
            return NotFound();

        if (item.IsUsed || item.IsCancelled)
            return RedirectToPage(new { id });

        item.IsCancelled = true;
        item.CancelledReason = reason;
        item.CancelledAt = DateTime.Now;
        item.CancelledBy = User.Identity?.Name ?? "System";

        await _context.SaveChangesAsync();
        return RedirectToPage(new { id });
    }
}
