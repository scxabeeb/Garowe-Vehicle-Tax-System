using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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

    public IActionResult OnGet(int id)
    {
        Item = _context.ReceiptReferences.FirstOrDefault(x => x.Id == id);

        if (Item == null)
            return RedirectToPage("Index");

        return Page();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var r = _context.ReceiptReferences.FirstOrDefault(x => x.Id == id);

        if (r == null)
            return RedirectToPage("Index");

        if (r.IsUsed)
        {
            r.IsUsed = false;
            r.UsedAt = null;
            r.UsedBy = null;
        }
        else
        {
            r.IsUsed = true;
            r.UsedAt = DateTime.Now;
            r.UsedBy = User.Identity?.Name ?? "System";
        }

        await _context.SaveChangesAsync();

        return RedirectToPage(new { id });
    }
}
