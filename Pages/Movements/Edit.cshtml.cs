using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Movements;

[Authorize]
public class EditModel : PageModel
{
    private readonly AppDbContext _context;

    public EditModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Movement Movement { get; set; } = new();

    // For dropdown
    public List<SelectListItem> CarTypes { get; set; } = new();
    public List<SelectListItem> RevenueAccounts { get; set; } = new();

    /// <summary>Also set the revenue account on this tariff's other car types.</summary>
    [BindProperty] public bool ApplyToAll { get; set; } = true;

    /// <summary>Rows with the same tariff name (one per car type).</summary>
    public int SiblingCount { get; set; }
    public int PaymentCount { get; set; }

    private void LoadInfo(Movement m)
    {
        var name = m.Name.ToLower();
        SiblingCount = _context.Movements.Count(x => x.Name.ToLower() == name);
        PaymentCount = _context.Payments.Count(p => p.MovementId == m.Id);
    }

    private void LoadCarTypes()
    {
        CarTypes = _context.CarTypes
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            })
            .ToList();
    }

    private void LoadRevenueAccounts()
    {
        RevenueAccounts = _context.RevenueAccounts
            .Where(r => r.IsActive)
            .OrderBy(r => r.AccountCode)
            .Select(r => new SelectListItem
            {
                Value = r.Id.ToString(),
                Text = $"{r.AccountCode} - {r.AccountName}"
            })
            .ToList();
    }

    // Access: Settings.View to open, Settings.Edit to save (Security/PagePermissionFilter).
    public IActionResult OnGet(int id)
    {
        var movement = _context.Movements.FirstOrDefault(m => m.Id == id);
        if (movement == null)
            return NotFound();

        Movement = movement;
        LoadCarTypes();
        LoadRevenueAccounts();
        LoadInfo(movement);
        return Page();
    }

    public IActionResult OnPost()
    {
        LoadCarTypes();
        LoadRevenueAccounts();

        var db = _context.Movements.FirstOrDefault(m => m.Id == Movement.Id);
        if (db == null)
            return NotFound();
        LoadInfo(db);

        var name = (Movement.Name ?? "").Trim();
        if (string.IsNullOrEmpty(name))
            ModelState.AddModelError("Movement.Name", "Tariff name is required.");
        if (Movement.RevenueAccountId == null || Movement.RevenueAccountId == 0 ||
            !_context.RevenueAccounts.Any(r => r.Id == Movement.RevenueAccountId && r.IsActive))
            ModelState.AddModelError("Movement.RevenueAccountId", "Please select a Revenue Account.");

        // Once payments use this tariff, its name and car type stay as they
        // were collected; the revenue account can still be corrected.
        var renamed = !string.Equals(name, db.Name, StringComparison.Ordinal);
        var carTypeChanged = Movement.CarTypeId != db.CarTypeId;
        if (PaymentCount > 0 && (renamed || carTypeChanged))
            ModelState.AddModelError("",
                "This tariff already has payments, so its name and car type cannot be changed. You can still change its revenue account.");

        // A duplicate is the same tariff name for the same car type.
        if (_context.Movements.Any(m => m.Id != db.Id &&
                                        m.Name.ToLower() == name.ToLower() &&
                                        m.CarTypeId == Movement.CarTypeId))
            ModelState.AddModelError("Movement.Name", "This tariff already exists for this car type.");

        if (ModelState.ErrorCount > 0)
            return Page();

        var oldName = db.Name;
        db.Name = name;
        db.CarTypeId = Movement.CarTypeId;
        db.RevenueAccountId = Movement.RevenueAccountId;

        var others = 0;
        if (ApplyToAll)
        {
            var lower = oldName.ToLower();
            foreach (var m in _context.Movements.Where(m => m.Id != db.Id && m.Name.ToLower() == lower).ToList())
            {
                m.RevenueAccountId = Movement.RevenueAccountId;
                others++;
            }
        }

        _context.SaveChanges();

        TempData["Success"] = others > 0
            ? $"Tariff '{db.Name}' updated, and its revenue account was also set on {others} other car type(s)."
            : $"Tariff '{db.Name}' updated.";
        return RedirectToPage("Index");
    }
}
