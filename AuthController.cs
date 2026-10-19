using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly JwtSettings _jwtSettings;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IActivityLogger _activity;

        public AuthController(
            AppDbContext context,
            IOptions<JwtSettings> jwtOptions,
            IPasswordHasher<User> passwordHasher,
            IActivityLogger activity)
        {
            _context = context;
            _jwtSettings = jwtOptions.Value;
            _passwordHasher = passwordHasher;
            _activity = activity;
        }

        // ================= LOGIN =================
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (dto == null ||
                string.IsNullOrWhiteSpace(dto.Username) ||
                string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new
                {
                    message = "Username and password are required"
                });
            }

            var username = dto.Username.Trim().ToLower();
            var password = dto.Password.Trim();

            var user = _context.Users.FirstOrDefault(u =>
                u.Username.Trim().ToLower() == username);

            if (user == null)
            {
                await _activity.LogAsync(ActivityActions.LoginFailed, dto.Username.Trim(), details: "App: unknown username");
                return Unauthorized(new
                {
                    message = "Invalid username"
                });
            }

            PasswordVerificationResult verifyResult;
            try
            {
                verifyResult = _passwordHasher.VerifyHashedPassword(
                    user, user.Password, password);
            }
            catch (FormatException)
            {
                // Stored value is not a valid Identity hash (legacy plain text).
                verifyResult = PasswordVerificationResult.Failed;
            }

            if (verifyResult == PasswordVerificationResult.Failed)
            {
                // Backward compatibility for legacy plain-text passwords.
                if (!string.Equals(user.Password.Trim(), password, StringComparison.Ordinal))
                {
                    await _activity.LogAsync(ActivityActions.LoginFailed, user.Username, entityId: user.Id.ToString(), details: "App: wrong password");
                    return Unauthorized(new
                    {
                        message = "Invalid password"
                    });
                }

                // Migrate to hashed password on successful legacy login.
                user.Password = _passwordHasher.HashPassword(user, password);
                _context.SaveChanges();
            }
            else if (verifyResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.Password = _passwordHasher.HashPassword(user, password);
                _context.SaveChanges();
            }

            if (user.IsLocked)
            {
                await _activity.LogAsync(ActivityActions.LoginFailed, user.Username, entityId: user.Id.ToString(), details: "App: account inactive");
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = InactiveMessage,
                    inactive = true
                });
            }

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_jwtSettings.Key));

            var creds = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
                signingCredentials: creds);

            await _activity.LogAsync(ActivityActions.Login, user.Username, entityId: user.Id.ToString(), details: $"Mobile app · {user.Role}");

            return Ok(new
            {
                accessToken = new JwtSecurityTokenHandler().WriteToken(token),
                expiresIn = (int)TimeSpan.FromMinutes(_jwtSettings.ExpiryMinutes).TotalSeconds,
                user = new
                {
                    id = user.Id,
                    username = user.Username,
                    role = user.Role
                }
            });
        }

        public const string InactiveMessage = "Your account is inactive. Please contact the administrator.";

        // ================= STATUS =================
        /// <summary>
        /// Lets the app check that its saved login still belongs to an active
        /// user. Inactive users are answered 401 by the inactive-user check in
        /// Program.cs before reaching here.
        /// </summary>
        [HttpGet("status")]
        public IActionResult Status()
        {
            var name = ActivityContext.AppUser(HttpContext);
            if (name == null)
                return Unauthorized(new { message = "Not signed in" });
            return Ok(new { active = true, username = name });
        }

        // ================= CHANGE PASSWORD =================
        [HttpPost("change-password")]
        public IActionResult ChangePassword([FromBody] ChangePasswordDto dto)
        {
            if (dto == null ||
                dto.UserId <= 0 ||
                string.IsNullOrWhiteSpace(dto.OldPassword) ||
                string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                return BadRequest(new
                {
                    message = "Invalid data"
                });
            }

            var user = _context.Users.FirstOrDefault(x => x.Id == dto.UserId);

            if (user == null)
            {
                return BadRequest(new
                {
                    message = "User not found"
                });
            }

            PasswordVerificationResult oldResult;
            try
            {
                oldResult = _passwordHasher.VerifyHashedPassword(
                    user, user.Password, dto.OldPassword.Trim());
            }
            catch (FormatException)
            {
                oldResult = PasswordVerificationResult.Failed;
            }

            var oldPasswordOk = oldResult != PasswordVerificationResult.Failed
                || string.Equals(user.Password.Trim(), dto.OldPassword.Trim(), StringComparison.Ordinal);

            if (!oldPasswordOk)
            {
                return BadRequest(new
                {
                    message = "Old password is incorrect"
                });
            }

            user.Password = _passwordHasher.HashPassword(user, dto.NewPassword.Trim());
            _context.SaveChanges();

            return Ok(new
            {
                message = "Password changed successfully"
            });
        }
    }

    public class LoginDto
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class ChangePasswordDto
    {
        public int UserId { get; set; }
        public string OldPassword { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }
}