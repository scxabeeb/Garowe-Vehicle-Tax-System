using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
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

    [BindProperty]
    public TaxAmount Tax { get; set; } = new();

    public SelectList CarTypes { get; set; } = null!;
    public SelectList Movements { get; set; } = null!;

    // Read-only Revenue Account info for the selected movement (set on POST validation)
    public string? RevenueAccountDisplay { get; set; }

    public void OnGet()
    {
        LoadCarTypes();
        Movements = new SelectList(Enumerable.Empty<SelectListItem>());
    }

    public IActionResult OnPost()
    {
        if (Tax.CarTypeId == 0 || Tax.MovementId == 0)
        {
            ModelState.AddModelError("", "Please select both Car Type and Movement.");
            LoadCarTypes();
            LoadMovements(Tax.CarTypeId);
            return Page();
        }

        bool exists = _context.TaxAmounts.Any(t =>
            t.CarTypeId == Tax.CarTypeId &&
            t.MovementId == Tax.MovementId);

        if (exists)
        {
            ModelState.AddModelError("", "This tax amount already exists.");
            LoadCarTypes();
            LoadMovements(Tax.CarTypeId);
            return Page();
        }

        if (!ModelState.IsValid)
        {
            LoadCarTypes();
            LoadMovements(Tax.CarTypeId);
            return Page();
        }

        _context.TaxAmounts.Add(Tax);
        _context.SaveChanges();

        TempData["Success"] = "Tax amount created successfully.";
        return RedirectToPage("Index");
    }

    // Ajax endpoint - now includes Revenue Account info
    public JsonResult OnGetMovements(int carTypeId)
    {
        var data = _context.Movements
            .Include(m => m.RevenueAccount)
            .Where(m => m.CarTypeId == carTypeId)
            .OrderBy(m => m.Name)
            .Select(m => new
            {
                id = m.Id,
                name = m.Name,
                revenueAccountCode = m.RevenueAccount != null ? m.RevenueAccount.AccountCode : "",
                revenueAccountName = m.RevenueAccount != null ? m.RevenueAccount.AccountName : "",
                revenueAccountDisplay = m.RevenueAccount != null
                    ? $"{m.RevenueAccount.AccountCode} - {m.RevenueAccount.AccountName}"
                    : ""
            })
            .ToList();

        return new JsonResult(data);
    }

    private void LoadCarTypes()
    {
        CarTypes = new SelectList(
            _context.CarTypes.OrderBy(c => c.Name),
            "Id",
            "Name"
        );
    }

    private void LoadMovements(int carTypeId)
    {
        Movements = new SelectList(
            _context.Movements.AsNoTracking()
                .Where(m => m.CarTypeId == carTypeId)
                .OrderBy(m => m.Name),
            "Id",
            "Name"
        );
    }
}
