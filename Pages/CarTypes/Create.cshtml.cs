using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.CarTypes;

public class CreateModel : PageModel
{
    private readonly AppDbContext _context;

    [BindProperty]
    public CarType CarType { get; set; } = new();

    public CreateModel(AppDbContext context)
    {
        _context = context;
    }

    public IActionResult OnPost()
    {
        _context.CarTypes.Add(CarType);
        _context.SaveChanges();
        return RedirectToPage("Index");
    }
}
