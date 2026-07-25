using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using VehicleTax.Web.Data;

namespace VehicleTax.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly JwtSettings _jwtSettings;

        public AuthController(AppDbContext context, IOptions<JwtSettings> jwtOptions)
        {
            _context = context;
            _jwtSettings = jwtOptions.Value;
        }

        // ---------------- LOGIN ----------------
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginDto dto)
        {
            if (dto == null ||
                string.IsNullOrWhiteSpace(dto.Username) ||
                string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new { message = "Username and password are required" });
            }

            var username = dto.Username.Trim().ToLower();
            var password = dto.Password.Trim();

            var user = _context.Users.FirstOrDefault(u =>
                u.Username.Trim().ToLower() == username
            );

            if (user == null)
                return Unauthorized(new { message = "Invalid username" });

            if (user.Password.Trim() != password)
                return Unauthorized(new { message = "Invalid password" });

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
                signingCredentials: creds);

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

        // ---------------- CHANGE PASSWORD ----------------
        [HttpPost("change-password")]
        public IActionResult ChangePassword([FromBody] ChangePasswordDto dto)
        {
            if (dto == null ||
                dto.UserId <= 0 ||
                string.IsNullOrWhiteSpace(dto.OldPassword) ||
                string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                return BadRequest(new { message = "Invalid data" });
            }

            var user = _context.Users.FirstOrDefault(x => x.Id == dto.UserId);

            if (user == null)
                return BadRequest(new { message = "User not found" });

            // Check old password
            if (user.Password.Trim() != dto.OldPassword.Trim())
                return BadRequest(new { message = "Old password is incorrect" });

            // Save new password
            user.Password = dto.NewPassword.Trim();
            _context.SaveChanges();

            return Ok(new { message = "Password changed successfully" });
        }
    }

    // ---------------- DTOs ----------------
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
