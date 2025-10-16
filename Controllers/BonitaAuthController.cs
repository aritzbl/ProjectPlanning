using Microsoft.AspNetCore.Mvc;
using ProjectPlanning.Web.Services;

namespace ProjectPlanning.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BonitaAuthController : ControllerBase
    {
        private readonly BonitaLoginService _bonita;
        private readonly ILogger<BonitaAuthController> _logger;

        public BonitaAuthController(BonitaLoginService bonita, ILogger<BonitaAuthController> logger)
        {
            _bonita = bonita;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto body)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(body.Username) || string.IsNullOrWhiteSpace(body.Password))
                    return BadRequest(new { message = "Username and password are required" });

                var (session, token, userId, roles) = await _bonita.LoginAsync(body.Username, body.Password);

                return Ok(new
                {
                    sessionId = session,
                    apiToken = token,
                    userId,
                    roles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Bonita login");
                return StatusCode(500, new { message = "Error de autenticación con Bonita", error = ex.Message });
            }
        }
    }

    public record LoginDto(string Username, string Password);
}