using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.TaxAmounts;

public class EditModel : PageModel
{
    private readonly AppDbContext _context;

    public EditModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public TaxAmount Tax { get; set; } = null!;

    public SelectList CarTypes { get; set; } = null!;
    public SelectList Movements { get; set; } = null!;

    // Read-only Revenue Account info for the selected movement
    public string? RevenueAccountDisplay { get; set; }

    public IActionResult OnGet(int id)
    {
        Tax = _context.TaxAmounts
            .Include(t => t.Movement)
                .ThenInclude(m => m.RevenueAccount)
            .AsNoTracking()
            .FirstOrDefault(t => t.Id == id)!;

        if (Tax == null)
            return NotFound();

        // Set the Revenue Account display info
        if (Tax.Movement?.RevenueAccount != null)
        {
            RevenueAccountDisplay = $"{Tax.Movement.RevenueAccount.AccountCode} - {Tax.Movement.RevenueAccount.AccountName}";
        }

        LoadLists();
        return Page();
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
        {
            LoadLists();
            LoadRevenueAccountDisplay();
            return Page();
        }

        // Reload lists for re-display on validation failure
        LoadLists();
        LoadRevenueAccountDisplay();

        _context.Attach(Tax).State = EntityState.Modified;
        _context.SaveChanges();

        TempData["SuccessMessage"] = "Tax amount updated successfully";
        return RedirectToPage("Index");
    }

    private void LoadLists()
    {
        CarTypes = new SelectList(
            _context.CarTypes.AsNoTracking(),
            "Id",
            "Name"
        );

        Movements = new SelectList(
            _context.Movements.AsNoTracking(),
            "Id",
            "Name"
        );
    }

    private void LoadRevenueAccountDisplay()
    {
        var movement = _context.Movements
            .Include(m => m.RevenueAccount)
            .AsNoTracking()
            .FirstOrDefault(m => m.Id == Tax.MovementId);

        if (movement?.RevenueAccount != null)
        {
            RevenueAccountDisplay = $"{movement.RevenueAccount.AccountCode} - {movement.RevenueAccount.AccountName}";
        }
        else
        {
            RevenueAccountDisplay = null;
        }
    }

    // Ajax endpoint - returns movements for a car type with Revenue Account info
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
                revenueAccountDisplay = m.RevenueAccount != null
                    ? $"{m.RevenueAccount.AccountCode} - {m.RevenueAccount.AccountName}"
                    : ""
            })
            .ToList();

        return new JsonResult(data);
    }
}
