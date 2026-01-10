using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.TaxAmounts;

public class CreateModel : PageModel
{
    private readonly AppDbContext _context;

    public CreateModel(AppDbContext context)
    {
        _context = context;
    }

    // ======================
    // FORM MODEL
    // ======================
    [BindProperty]
    public TaxAmount Tax { get; set; } = new();

    // ======================
    // DROPDOWNS
    // ======================
    public SelectList CarTypes { get; set; } = null!;
    public SelectList Movements { get; set; } = null!;

    // ======================
    // GET
    // ======================
    public void OnGet()
    {
        LoadDropdowns();
    }

    // ======================
    // POST
    // ======================
    public IActionResult OnPost()
    {
        // 🔒 Validate dropdown selection
        if (Tax.CarTypeId == 0 || Tax.MovementId == 0)
        {
            ModelState.AddModelError(
                string.Empty,
                "Please select both Car Type and Movement."
            );
        }

        // 🔒 Prevent duplicate CarType + Movement
        bool exists = _context.TaxAmounts.Any(t =>
            t.CarTypeId == Tax.CarTypeId &&
            t.MovementId == Tax.MovementId
        );

        if (exists)
        {
            ModelState.AddModelError(
                string.Empty,
                "A tax amount for the selected Car Type and Movement already exists."
            );
        }

        if (!ModelState.IsValid)
        {
            LoadDropdowns();
            return Page();
        }

        _context.TaxAmounts.Add(Tax);
        _context.SaveChanges();

        // ✅ Inline success alert on Index
        TempData["Success"] = "Tax amount created successfully.";

        return RedirectToPage("Index");
    }

    // ======================
    // HELPERS
    // ======================
    private void LoadDropdowns()
    {
        CarTypes = new SelectList(
            _context.CarTypes.OrderBy(c => c.Name),
            "Id",
            "Name"
        );

        Movements = new SelectList(
            _context.Movements.OrderBy(m => m.Name),
            "Id",
            "Name"
        );
    }
}
