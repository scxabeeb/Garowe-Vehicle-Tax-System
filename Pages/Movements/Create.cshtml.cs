using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Movements;

public class CreateModel : PageModel
{
    private readonly AppDbContext _context;

    public CreateModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public string MovementName { get; set; } = "";

    [BindProperty]
    public List<int> SelectedCarTypeIds { get; set; } = new();

    [BindProperty]
    public int RevenueAccountId { get; set; }

    public List<SelectListItem> CarTypes { get; set; } = new();
    public List<SelectListItem> RevenueAccounts { get; set; } = new();

    private bool HasPermission(string permission)
    {
        return User.IsInRole("Admin") || User.HasClaim("permission", permission);
    }

    public IActionResult OnGet()
    {
        if (!HasPermission("movement.create"))
            return Forbid();

        LoadCarTypes();
        LoadRevenueAccounts();
        return Page();
    }

    public IActionResult OnPost()
    {
        LoadCarTypes();
        LoadRevenueAccounts();

        if (!HasPermission("movement.create"))
            return Forbid();

        if (string.IsNullOrWhiteSpace(MovementName))
        {
            ModelState.AddModelError("MovementName", "Movement name is required.");
            return Page();
        }

        if (SelectedCarTypeIds.Count == 0)
        {
            ModelState.AddModelError("SelectedCarTypeIds", "Please select at least one car type.");
            return Page();
        }

        if (RevenueAccountId == 0)
        {
            ModelState.AddModelError("RevenueAccountId", "Please select a Revenue Account.");
            return Page();
        }

        // Validate that the selected Revenue Account exists and is active
        var account = _context.RevenueAccounts
            .AsNoTracking()
            .FirstOrDefault(r => r.Id == RevenueAccountId && r.IsActive);

        if (account == null)
        {
            ModelState.AddModelError("RevenueAccountId", "Invalid Revenue Account selected.");
            return Page();
        }

        foreach (var carTypeId in SelectedCarTypeIds)
        {
            bool exists = _context.Movements.Any(m =>
                m.Name.ToLower() == MovementName.ToLower() &&
                m.CarTypeId == carTypeId);

            if (!exists)
            {
                _context.Movements.Add(new Movement
                {
                    Name = MovementName.Trim(),
                    CarTypeId = carTypeId,
                    RevenueAccountId = RevenueAccountId
                });
            }
        }

        _context.SaveChanges();

        TempData["Success"] = "Movement created successfully for selected car types.";
        return RedirectToPage("Index");
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
}
