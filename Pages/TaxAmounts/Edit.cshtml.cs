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

    public IActionResult OnGet(int id)
    {
        Tax = _context.TaxAmounts
            .AsNoTracking()
            .FirstOrDefault(t => t.Id == id)!;

        if (Tax == null)
            return NotFound();

        LoadLists();
        return Page();
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
        {
            LoadLists();
            return Page();
        }

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
}
