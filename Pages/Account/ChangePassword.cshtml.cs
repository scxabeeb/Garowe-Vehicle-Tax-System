using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using VehicleTax.Web.Data;

namespace VehicleTax.Web.Pages.Account
{
    [Authorize]
    public class ChangePasswordModel : PageModel
    {
        private readonly AppDbContext _context;

        public ChangePasswordModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            [DataType(DataType.Password)]
            public string CurrentPassword { get; set; } = "";

            [Required]
            [DataType(DataType.Password)]
            [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
            public string NewPassword { get; set; } = "";

            [Required]
            [DataType(DataType.Password)]
            [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
            public string ConfirmPassword { get; set; } = "";
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var username = User.Identity!.Name;

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                return Page();
            }

            // Must match login logic (plain text)
            if (user.Password != Input.CurrentPassword)
            {
                ModelState.AddModelError("", "Current password is incorrect.");
                return Page();
            }

            // Update password in database
            user.Password = Input.NewPassword;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Password changed successfully.";
            return RedirectToPage();
        }
    }
}
