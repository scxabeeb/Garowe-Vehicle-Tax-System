using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.ReceiptReferences;

public class CreateModel : PageModel
{
    private readonly AppDbContext _context;

    public CreateModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public string ReferenceNumber { get; set; } = "";

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(ReferenceNumber))
        {
            TempData["Error"] = "Reference number is required.";
            return Page();
        }

        ReferenceNumber = ReferenceNumber.Trim();

        var exists = _context.ReceiptReferences
            .Any(x => x.ReferenceNumber == ReferenceNumber);

        if (exists)
        {
            TempData["Error"] = "This reference already exists.";
            return Page();
        }

        _context.ReceiptReferences.Add(new ReceiptReference
        {
            ReferenceNumber = ReferenceNumber
        });

        await _context.SaveChangesAsync();

        return RedirectToPage("Index");
    }
}
