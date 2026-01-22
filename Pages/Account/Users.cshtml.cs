using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

[Authorize(Roles = "Admin")]
public class UsersModel : PageModel
{
    private readonly AppDbContext _context;

    public UsersModel(AppDbContext context)
    {
        _context = context;
    }

    public List<User> Users { get; set; } = new();

    public void OnGet()
    {
        Users = _context.Users
                        .OrderBy(u => u.Username)
                        .ToList();
    }

    public IActionResult OnPostDelete(int id)
    {
        var user = _context.Users.FirstOrDefault(u => u.Id == id);
        if (user == null)
            return RedirectToPage();

        // Check if user is used as Collector in any Payment
        bool hasPayments = _context.Payments.Any(p => p.CollectorId == id);

        if (hasPayments)
        {
            TempData["Error"] = "This user cannot be deleted because they have payments recorded.";
            return RedirectToPage();
        }

        _context.Users.Remove(user);
        _context.SaveChanges();

        return RedirectToPage();
    }
}
