using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        public SelectList Checkpoints { get; set; } = null!;

        public IActionResult OnGet(int id)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == id);
            if (user == null)
                return RedirectToPage("/Account/Users");

            EditUser = user;
            EditUser.Password = "";
            LoadCheckpoints();
            return Page();
        }

        public IActionResult OnPost()
        {
            var dbUser = _context.Users.FirstOrDefault(u => u.Id == EditUser.Id);
            if (dbUser == null)
                return RedirectToPage("/Account/Users");

            LoadCheckpoints();

            if (EditUser.CheckpointId.HasValue && !_context.Checkpoints.Any(c => c.Id == EditUser.CheckpointId.Value))
            {
                ModelState.AddModelError("", "Selected checkpoint does not exist");
                EditUser.Password = "";
                return Page();
            }

            if (string.IsNullOrWhiteSpace(EditUser.Username))
            {
                ModelState.AddModelError("", "Username is required");
                EditUser.Password = "";
                return Page();
            }

            dbUser.Username = EditUser.Username.Trim();
            dbUser.FullName = EditUser.FullName?.Trim() ?? "";
            dbUser.Phone = EditUser.Phone?.Trim() ?? "";
            dbUser.Role = EditUser.Role;
            dbUser.CheckpointId = EditUser.CheckpointId;

            if (!string.IsNullOrWhiteSpace(EditUser.Password))
                dbUser.Password = _passwordHasher.HashPassword(dbUser, EditUser.Password.Trim());

            _context.SaveChanges();
            return RedirectToPage("/Account/Users");
        }

        private void LoadCheckpoints()
        {
            Checkpoints = new SelectList(
                _context.Checkpoints
                    .OrderBy(c => c.Name)
                    .ToList(),
                "Id",
                "Name"
            );
        }
    }
}
