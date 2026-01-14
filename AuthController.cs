using Microsoft.AspNetCore.Mvc;
using VehicleTax.Web.Data;
using System.Linq;

namespace VehicleTax.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new
                {
                    message = "Username and password are required"
                });
            }

            var user = _context.Users.FirstOrDefault(u =>
                u.Username == dto.Username
            );

            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "Invalid username"
                });
            }

            if (user.Password != dto.Password)
            {
                return Unauthorized(new
                {
                    message = "Invalid password"
                });
            }

            return Ok(new
            {
                id = user.Id,
                username = user.Username,
                role = user.Role,
                permissions = new[] { "View" }
            });
        }
    }

    public class LoginDto
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
