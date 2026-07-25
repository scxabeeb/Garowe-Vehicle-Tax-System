using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace VehicleTax.Web.Pages.Account
{
    [Authorize]
    public class ChangePasswordModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        public ChangePasswordModel(AppDbContext context, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
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

            var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.Password, Input.CurrentPassword);
            var isLegacyPlainText = verifyResult == PasswordVerificationResult.Failed && user.Password == Input.CurrentPassword;

            if (verifyResult == PasswordVerificationResult.Failed && !isLegacyPlainText)
            {
                ModelState.AddModelError("", "Current password is incorrect.");
                return Page();
            }

            // Update password in database
            user.Password = _passwordHasher.HashPassword(user, Input.NewPassword);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Password changed successfully.";
            return RedirectToPage();
        }
    }
}
