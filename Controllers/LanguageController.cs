using Microsoft.AspNetCore.Mvc;

namespace YourProjectName.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LanguageController : ControllerBase
    {
        [HttpGet("supported")]
        public IActionResult GetSupportedLanguages()
        {
            return Ok(new
            {
                message = "The following languages are supported:",
                languages = new[] { "English", "Arabic", "Somali", "French" },
                timestamp = DateTime.UtcNow
            });
        }
    }
}
