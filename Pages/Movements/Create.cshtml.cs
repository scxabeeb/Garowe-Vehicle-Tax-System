using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
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

    public List<SelectListItem> CarTypes { get; set; } = new();

    public void OnGet()
    {
        LoadCarTypes();
    }

    public IActionResult OnPost()
    {
        LoadCarTypes();

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
                    CarTypeId = carTypeId
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
}
