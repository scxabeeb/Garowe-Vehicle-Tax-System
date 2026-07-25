using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace VehicleTax.Web.Pages.Account
{
    [Authorize(Roles = "Admin")]
    public class EditUserModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        public EditUserModel(AppDbContext context, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
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
                dbUser.Password = _passwordHasher.HashPassword(dbUser, EditUser.Password.Trim());

            _context.SaveChanges();
            return RedirectToPage("/Account/Users");
        }
    }
}
