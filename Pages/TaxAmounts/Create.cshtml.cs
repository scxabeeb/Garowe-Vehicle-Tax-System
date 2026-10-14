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
            ModelState.AddModelError("", "This tariff already has a tax amount for this car type. Edit the existing amount instead.");
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

    /// <summary>
    /// Tariffs that can still get an amount for this car type: active, and
    /// without a tax amount for it yet.
    /// </summary>
    private IQueryable<Movement> Available(int carTypeId) =>
        _context.Movements.AsNoTracking()
            .Where(m => m.CarTypeId == carTypeId && m.IsActive)
            .Where(m => !_context.TaxAmounts.Any(t => t.CarTypeId == carTypeId && t.MovementId == m.Id));

    // Ajax endpoint - includes Revenue Account info
    public JsonResult OnGetMovements(int carTypeId)
    {
        var data = Available(carTypeId)
            .Include(m => m.RevenueAccount)
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
            Available(carTypeId).OrderBy(m => m.Name),
            "Id",
            "Name"
        );
    }
}
