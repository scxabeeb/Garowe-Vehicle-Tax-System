using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Account
{
    [Authorize(Roles = "Admin")]
    public class EditUserModel : PageModel
    {
        private readonly AppDbContext _context;

        public EditUserModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public User EditUser { get; set; } = new();

        public IActionResult OnGet(int id)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == id);
            if (user == null)
                return RedirectToPage("/Account/Users");

            EditUser = user;
            EditUser.Password = "";
            return Page();
        }

        public IActionResult OnPost()
        {
            var dbUser = _context.Users.FirstOrDefault(u => u.Id == EditUser.Id);
            if (dbUser == null)
                return RedirectToPage("/Account/Users");

            dbUser.Username = EditUser.Username.Trim();
            dbUser.Role = EditUser.Role;

            if (!string.IsNullOrWhiteSpace(EditUser.Password))
                dbUser.Password = EditUser.Password.Trim();

            _context.SaveChanges();
            return RedirectToPage("/Account/Users");
        }
    }
}
