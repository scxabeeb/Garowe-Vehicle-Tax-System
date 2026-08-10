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

    private bool HasPermission(string permission)
    {
        return User.IsInRole("Admin") || User.HasClaim("permission", permission);
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

    public IActionResult OnGet(int id)
    {
        if (!HasPermission("movement.edit"))
            return Forbid();

        Movement = _context.Movements.FirstOrDefault(m => m.Id == id)!;
        if (Movement == null)
            return NotFound();

        LoadCarTypes();
        LoadRevenueAccounts();
        return Page();
    }

    public IActionResult OnPost()
    {
        if (!HasPermission("movement.edit"))
            return Forbid();

        LoadCarTypes();
        LoadRevenueAccounts();

        if (!ModelState.IsValid)
            return Page();

        // Validate Revenue Account
        if (Movement.RevenueAccountId == null || Movement.RevenueAccountId == 0)
        {
            ModelState.AddModelError("Movement.RevenueAccountId", "Please select a Revenue Account.");
            return Page();
        }

        // Block update if used in payments
        bool isUsed = _context.Payments.Any(p => p.MovementId == Movement.Id);
        if (isUsed)
        {
            ModelState.AddModelError("",
                "This movement is already used in payments and cannot be edited.");
            return Page();
        }

        // Prevent duplicate name
        bool exists = _context.Movements.Any(m =>
            m.Name.ToLower() == Movement.Name.ToLower() &&
            m.Id != Movement.Id);

        if (exists)
        {
            ModelState.AddModelError("Movement.Name",
                "This movement name already exists.");
            return Page();
        }

        _context.Update(Movement);
        _context.SaveChanges();

        TempData["Success"] = "Movement updated successfully.";
        return RedirectToPage("Index");
    }
}
